using BackendAPI.Extensions;
using BackendAPI.Models;

namespace BackendAPI.Services;

/// <summary>
/// Payload carried by the issue/return events. A class rather than loose
/// parameters so new facts can be added without changing every subscriber.
/// </summary>
public class BookIssuedEventArgs : EventArgs
{
    public required IssuedBook Loan { get; init; }
    public required string MemberName { get; init; }
    public required string MemberEmail { get; init; }
    public required string BookTitle { get; init; }
}

/// <summary>
/// Handlers subscribed to the issue/return events.
///
/// The point of routing through events rather than calling these directly from
/// IssueService: adding a new notification channel (SMS, WhatsApp) means adding
/// a subscriber at startup and changing IssueService not at all.
/// </summary>
public class IssueNotificationHandlers
{
    private readonly ILogger<IssueNotificationHandlers> _logger;

    /// <summary>
    /// Outbound email queue. A Queue&lt;T&gt; because notifications are processed
    /// strictly first-in-first-out — the member who borrowed first is emailed
    /// first. Drained by the background service.
    /// </summary>
    private static readonly Queue<string> EmailQueue = new();

    public IssueNotificationHandlers(ILogger<IssueNotificationHandlers> logger)
    {
        _logger = logger;
    }

    /// <summary>Simulates the confirmation email. Real SMTP is out of scope.</summary>
    public void SendIssueConfirmation(object? sender, BookIssuedEventArgs e)
    {
        var message =
            "To " + e.MemberEmail + ": you borrowed \"" + e.BookTitle +
            "\". Due " + e.Loan.DueDate.ToString("dd MMM yyyy") + ".";

        lock (EmailQueue)
        {
            EmailQueue.Enqueue(message);
        }

        _logger.LogInformation("Queued issue confirmation for {Email}", e.MemberEmail);
    }

    /// <summary>Audit trail, independent of whether email succeeds.</summary>
    public void LogIssuance(object? sender, BookIssuedEventArgs e)
        => _logger.LogInformation(
            "ISSUED book {BookId} to member {MemberId}, due {DueDate:yyyy-MM-dd}",
            e.Loan.BookId, e.Loan.MemberId, e.Loan.DueDate);

    public void LogReturn(object? sender, BookIssuedEventArgs e)
        => _logger.LogInformation(
            "RETURNED book {BookId} from member {MemberId}, {OverdueDays} days overdue, fine {Fine}",
            e.Loan.BookId, e.Loan.MemberId, e.Loan.OverdueDays(), e.Loan.Fine);

    /// <summary>Only fires when money is actually owed.</summary>
    public void NotifyFine(object? sender, BookIssuedEventArgs e)
    {
        if (e.Loan.Fine <= 0) return;

        var message =
            "To " + e.MemberEmail + ": a fine of Rs " + e.Loan.Fine +
            " is due for \"" + e.BookTitle + "\".";

        lock (EmailQueue)
        {
            EmailQueue.Enqueue(message);
        }

        _logger.LogInformation("Queued fine notice for {Email}, amount {Fine}", e.MemberEmail, e.Loan.Fine);
    }

    /// <summary>Drains the queue. Called by the reminder background service.</summary>
    public static IReadOnlyList<string> DrainQueue()
    {
        lock (EmailQueue)
        {
            var drained = new List<string>(EmailQueue.Count);
            while (EmailQueue.Count > 0)
            {
                drained.Add(EmailQueue.Dequeue());
            }
            return drained;
        }
    }

    public static int PendingCount
    {
        get { lock (EmailQueue) { return EmailQueue.Count; } }
    }
}
