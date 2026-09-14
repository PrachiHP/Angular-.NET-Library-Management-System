using BackendAPI.Data;
using BackendAPI.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackendAPI.Services;

/// <summary>
/// Replaces Hangfire. Runs for the lifetime of the application, waking once a
/// day to flag loans due soon, loans already overdue, and cooldowns that have
/// just expired.
///
/// BackgroundService is the built-in base class for a long-running IHostedService;
/// it gives you ExecuteAsync plus a CancellationToken that is signalled on shutdown.
/// </summary>
public class DueDateReminderService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>
    /// IServiceScopeFactory, NOT AppDbContext.
    ///
    /// A BackgroundService is registered as a SINGLETON. AppDbContext is SCOPED.
    /// Injecting the context directly throws at startup:
    ///   "Cannot consume scoped service 'AppDbContext' from singleton ..."
    /// So the service creates its own scope per iteration instead.
    /// </summary>
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LoanOptions _loan;
    private readonly ILogger<DueDateReminderService> _logger;

    public DueDateReminderService(
        IServiceScopeFactory scopeFactory,
        IOptions<LoanOptions> loan,
        ILogger<DueDateReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _loan = loan.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Due-date reminder service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown, not an error.
                break;
            }
            catch (Exception ex)
            {
                // One bad night must not kill the service for the rest of the
                // process lifetime.
                _logger.LogError(ex, "Reminder sweep failed; will retry next interval.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Due-date reminder service stopping.");
    }

    /// <summary>Internal so a test could invoke a single sweep without waiting a day.</summary>
    internal async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var soonCutoff = now.AddDays(2);

        var dueSoon = await context.IssuedBooks
            .AsNoTracking()
            .Include(i => i.Book)
            .Include(i => i.Member).ThenInclude(m => m.User)
            .Where(i => !i.IsReturned && i.DueDate > now && i.DueDate <= soonCutoff)
            .ToListAsync(ct);

        var overdue = await context.IssuedBooks
            .AsNoTracking()
            .Include(i => i.Book)
            .Include(i => i.Member).ThenInclude(m => m.User)
            .Where(i => !i.IsReturned && i.DueDate < now)
            .ToListAsync(ct);

        // Task.WhenAll runs the notifications concurrently rather than one after
        // another, so the sweep finishes in about the time of the slowest send
        // instead of the sum of all of them.
        await Task.WhenAll(dueSoon.Select(loan => NotifyDueSoonAsync(loan.Id, loan.Member.User.Email, loan.DueDate, ct)));

        foreach (var loan in overdue)
        {
            _logger.LogWarning(
                "OVERDUE: loan {LoanId} ({Title}) is {Days} days late, accrued fine {Fine}",
                loan.Id,
                loan.Book.Title,
                loan.OverdueDays(now),
                FineCalculator.Calculate(loan.OverdueDays(now)));
        }

        // Cooldowns that ended in the last 24 hours — the member may now
        // re-request that title.
        var windowStart = now.AddMonths(-_loan.CooldownMonths).AddDays(-1);
        var windowEnd = now.AddMonths(-_loan.CooldownMonths);

        var released = await context.IssuedBooks
            .AsNoTracking()
            .CountAsync(i => i.DueDate > windowStart && i.DueDate <= windowEnd, ct);

        var queued = IssueNotificationHandlers.DrainQueue();

        _logger.LogInformation(
            "Reminder sweep: {DueSoon} due soon, {Overdue} overdue, {Released} cooldowns released, {Queued} queued messages sent.",
            dueSoon.Count, overdue.Count, released, queued.Count);
    }

    private Task NotifyDueSoonAsync(int loanId, string email, DateTime dueDate, CancellationToken ct)
    {
        _logger.LogInformation(
            "Reminder to {Email}: loan {LoanId} is due {DueDate:dd MMM yyyy}", email, loanId, dueDate);

        // Stands in for a real SMTP send. Returning a Task keeps the signature
        // honest for Task.WhenAll above.
        return Task.CompletedTask;
    }
}
