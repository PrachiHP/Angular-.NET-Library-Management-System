using BackendAPI.Data;
using BackendAPI.Dtos;
using BackendAPI.Exceptions;
using BackendAPI.Extensions;
using BackendAPI.Models;
using BackendAPI.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackendAPI.Services;

/// <summary>
/// Every rule in README section 1 lives here, and only here. Controllers call
/// these methods; nothing else decides whether a loan is legal.
/// </summary>
public class IssueService : IIssueService
{
    private readonly IIssueRepository _issues;
    private readonly AppDbContext _context;
    private readonly LoanOptions _loan;
    private readonly FineOptions _fine;
    private readonly IssueNotificationHandlers _handlers;
    private readonly ILogger<IssueService> _logger;

    /// <summary>
    /// Multicast events. '+=' adds a subscriber; raising the event invokes
    /// every subscriber in registration order. IssueService does not know or
    /// care who is listening — that is the decoupling the pattern buys.
    /// </summary>
    public event EventHandler<BookIssuedEventArgs>? OnBookIssued;
    public event EventHandler<BookIssuedEventArgs>? OnBookReturned;

    public IssueService(
        IIssueRepository issues,
        AppDbContext context,
        IOptions<LoanOptions> loan,
        IOptions<FineOptions> fine,
        IssueNotificationHandlers handlers,
        ILogger<IssueService> logger)
    {
        _issues = issues;
        _context = context;
        _loan = loan.Value;
        _fine = fine.Value;
        _handlers = handlers;
        _logger = logger;

        // Wiring the subscribers. Both handlers run on issue; three on return.
        // Adding a fourth channel means one more line here and no change below.
        OnBookIssued += _handlers.SendIssueConfirmation;
        OnBookIssued += _handlers.LogIssuance;

        OnBookReturned += _handlers.LogReturn;
        OnBookReturned += _handlers.NotifyFine;
    }

    // -----------------------------------------------------------------------
    // Requests
    // -----------------------------------------------------------------------

    public async Task<IssueRequestDto> CreateRequestAsync(
        int memberId,
        CreateIssueRequestDto dto,
        CancellationToken ct = default)
    {
        var book = await _context.Books.FirstOrDefaultAsync(b => b.Id == dto.BookId && b.IsActive, ct)
                   ?? throw new NotFoundException(nameof(Book), dto.BookId);

        // The three rules from the specification, checked in the order that
        // gives the most useful message first.
        var status = await GetMemberStatusAsync(memberId, book.Id, ct);
        if (!status.CanRequest)
        {
            throw new BusinessException(status.Reason ?? "This book cannot be requested right now.");
        }

        var request = new IssueRequest
        {
            BookId = book.Id,
            MemberId = memberId,
            Status = RequestStatus.Pending,
            RequestedOn = DateTime.UtcNow,
        };

        _context.IssueRequests.Add(request);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Member {MemberId} requested book {BookId}", memberId, book.Id);

        return ToRequestDto((await _issues.GetRequestAsync(request.Id, false, ct))!);
    }

    public async Task<IReadOnlyList<IssueRequestDto>> GetRequestsAsync(
        RequestStatus? status,
        int? memberId,
        CancellationToken ct = default)
    {
        var requests = await _issues.GetRequestsAsync(status, memberId, ct);
        return requests.Select(ToRequestDto).ToList();
    }

    /// <summary>
    /// Approving turns a request into a physical loan: a due date is set and a
    /// copy leaves the shelf. Both changes plus the status update are saved in
    /// ONE SaveChangesAsync, so a failure cannot decrement the copy count
    /// without producing the matching loan record.
    /// </summary>
    public async Task<IssueRequestDto> ApproveAsync(int requestId, CancellationToken ct = default)
    {
        var request = await _issues.GetRequestAsync(requestId, tracked: true, ct)
                      ?? throw new NotFoundException(nameof(IssueRequest), requestId);

        if (request.Status != RequestStatus.Pending)
        {
            throw new BusinessException(
                "This request has already been " + request.Status.ToString().ToLowerInvariant() + ".");
        }

        // Availability is checked HERE, not at request time: a copy may have
        // been returned or taken in between.
        var book = await _context.Books.FirstOrDefaultAsync(b => b.Id == request.BookId, ct)
                   ?? throw new NotFoundException(nameof(Book), request.BookId);

        if (!book.IsAvailable())
        {
            throw new BusinessException("No copies of \"" + book.Title + "\" are currently available.");
        }

        var now = DateTime.UtcNow;

        var loan = new IssuedBook
        {
            IssueRequestId = request.Id,
            BookId = request.BookId,
            MemberId = request.MemberId,
            IssuedDate = now,
            DueDate = now.AddDays(_loan.LoanDays),
            IsReturned = false,
            ReIssueCount = 0,
            Fine = 0m,
        };

        request.Status = RequestStatus.Approved;
        request.DecidedOn = now;

        book.AvailableCopies--;

        _context.IssuedBooks.Add(loan);
        await _context.SaveChangesAsync(ct);

        await RaiseAsync(OnBookIssued, loan, ct);

        return ToRequestDto((await _issues.GetRequestAsync(requestId, false, ct))!);
    }

    public async Task<IssueRequestDto> RejectAsync(int requestId, string reason, CancellationToken ct = default)
    {
        var request = await _issues.GetRequestAsync(requestId, tracked: true, ct)
                      ?? throw new NotFoundException(nameof(IssueRequest), requestId);

        if (request.Status != RequestStatus.Pending)
        {
            throw new BusinessException(
                "This request has already been " + request.Status.ToString().ToLowerInvariant() + ".");
        }

        request.Status = RequestStatus.Rejected;
        request.RejectionReason = reason.Trim();
        request.DecidedOn = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return ToRequestDto((await _issues.GetRequestAsync(requestId, false, ct))!);
    }

    // -----------------------------------------------------------------------
    // Loans
    // -----------------------------------------------------------------------

    public async Task<IReadOnlyList<IssuedBookDto>> GetLoansAsync(
        int? memberId,
        bool? onlyOutstanding,
        CancellationToken ct = default)
    {
        var loans = await _issues.GetLoansAsync(memberId, onlyOutstanding, ct);
        return loans.Select(ToLoanDto).ToList();
    }

    public async Task<IReadOnlyList<IssuedBookDto>> GetOverdueAsync(CancellationToken ct = default)
    {
        var loans = await _issues.GetOverdueAsync(ct);
        return loans.Select(ToLoanDto).ToList();
    }

    /// <summary>
    /// Return: stamp the date, compute the fine, put the copy back on the shelf.
    /// </summary>
    public async Task<IssuedBookDto> ReturnAsync(int loanId, CancellationToken ct = default)
    {
        var loan = await _issues.GetLoanAsync(loanId, tracked: true, ct)
                   ?? throw new NotFoundException(nameof(IssuedBook), loanId);

        if (loan.IsReturned)
        {
            throw new BusinessException("This book has already been returned.");
        }

        loan.ReturnDate = DateTime.UtcNow;
        loan.IsReturned = true;

        // OverdueDays reads ReturnDate, which was just set — so the fine is
        // measured at the moment of return, not "now" at some later read.
        loan.Fine = FineCalculator.Calculate(loan.OverdueDays(), _fine.RatePerDay);

        var book = await _context.Books.FirstOrDefaultAsync(b => b.Id == loan.BookId, ct);
        if (book is not null)
        {
            // Never exceed TotalCopies, which would happen if a return were
            // somehow processed twice.
            book.AvailableCopies = Math.Min(book.AvailableCopies + 1, book.TotalCopies);
        }

        await _context.SaveChangesAsync(ct);

        await RaiseAsync(OnBookReturned, loan, ct);

        return ToLoanDto((await _issues.GetLoanAsync(loanId, false, ct))!);
    }

    /// <summary>
    /// Re-issue extends the due date. Capped at MaxReIssues per cycle.
    /// Note it does NOT reset ReIssueCount or touch AvailableCopies — the copy
    /// never went back on the shelf.
    /// </summary>
    public async Task<IssuedBookDto> ReIssueAsync(int loanId, int memberId, CancellationToken ct = default)
    {
        var loan = await _issues.GetLoanAsync(loanId, tracked: true, ct)
                   ?? throw new NotFoundException(nameof(IssuedBook), loanId);

        // A member may only extend their own loan.
        if (loan.MemberId != memberId)
        {
            throw new UnauthorizedException("This loan belongs to another member.");
        }

        if (loan.IsReturned)
        {
            throw new BusinessException("This book has already been returned.");
        }

        if (loan.ReIssueCount >= _loan.MaxReIssues)
        {
            throw new BusinessException(
                "Re-issue limit reached (" + _loan.MaxReIssues + " maximum). Please return the book.");
        }

        loan.DueDate = loan.DueDate.AddDays(_loan.LoanDays);
        loan.ReIssueCount++;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Loan {LoanId} re-issued ({Count}/{Max}), now due {DueDate:yyyy-MM-dd}",
            loanId, loan.ReIssueCount, _loan.MaxReIssues, loan.DueDate);

        return ToLoanDto((await _issues.GetLoanAsync(loanId, false, ct))!);
    }

    // -----------------------------------------------------------------------
    // The rules, in one place
    // -----------------------------------------------------------------------

    public async Task<BookMemberStatusDto> GetMemberStatusAsync(
        int memberId,
        int bookId,
        CancellationToken ct = default)
    {
        // Has this member ever borrowed this book? Gates the feedback and quote
        // forms, which are only open to readers who actually had the book.
        var everBorrowed = await _context.IssuedBooks
            .AnyAsync(i => i.MemberId == memberId && i.BookId == bookId, ct);

        // Rule 1: cannot borrow a book you are already holding.
        var activeLoan = await _context.IssuedBooks
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.MemberId == memberId && i.BookId == bookId && !i.IsReturned, ct);

        if (activeLoan is not null)
        {
            // Days remaining, negative once overdue. Date-only so a loan due
            // later today reads as "1 day", not zero.
            var daysUntilDue = (activeLoan.DueDate.Date - DateTime.UtcNow.Date).Days;

            return new BookMemberStatusDto(
                bookId,
                false,
                "You are currently holding this book.",
                null,
                CurrentlyHeld: true,
                HasPendingRequest: false,
                IssuedBookId: activeLoan.Id,
                DueDate: activeLoan.DueDate,
                DaysUntilDue: daysUntilDue,
                IsOverdue: activeLoan.IsOverdue(),
                ReIssuesRemaining: Math.Max(0, _loan.MaxReIssues - activeLoan.ReIssueCount),
                HasEverBorrowed: true);
        }

        // Rule 2: one pending request at a time per book.
        var pending = await _issues.HasPendingRequestAsync(memberId, bookId, ct);
        if (pending)
        {
            return new BookMemberStatusDto(
                bookId,
                false,
                "You already have a pending request for this book.",
                null,
                CurrentlyHeld: false,
                HasPendingRequest: true,
                HasEverBorrowed: everBorrowed);
        }

        // Rule 3: the cooldown. Measured from the FINAL due date of the last
        // loan — after any re-issues — and deliberately NOT from the return
        // date, so returning early cannot reset the clock.
        var lastLoan = await _issues.GetLastLoanAsync(memberId, bookId, ct);
        if (lastLoan is not null && !lastLoan.DueDate.IsCooldownExpired(_loan.CooldownMonths))
        {
            var until = lastLoan.DueDate.CooldownEnd(_loan.CooldownMonths);

            return new BookMemberStatusDto(
                bookId,
                false,
                "You may request this book again after " + until.ToString("dd MMM yyyy") + ".",
                until,
                CurrentlyHeld: false,
                HasPendingRequest: false,
                HasEverBorrowed: everBorrowed);
        }

        return new BookMemberStatusDto(
            bookId,
            true,
            null,
            null,
            CurrentlyHeld: false,
            HasPendingRequest: false,
            HasEverBorrowed: everBorrowed);
    }

    // -----------------------------------------------------------------------
    // CSV export
    // -----------------------------------------------------------------------

    /// <summary>
    /// Takes the BASE type TextWriter, so the same method serves a StreamWriter
    /// (file on disk), a StringWriter (in-memory for an HTTP response), or
    /// Console.Out. The caller chooses the destination; this code never changes.
    /// </summary>
    public async Task ExportLoansAsync(TextWriter writer, CancellationToken ct = default)
    {
        await writer.WriteLineAsync(
            "LoanId,BookTitle,ISBN,Member,IssuedDate,DueDate,ReturnDate,Returned,ReIssues,OverdueDays,Fine");

        // await foreach over IAsyncEnumerable: rows are written as they arrive
        // from SQL Server rather than buffering the whole table first.
        await foreach (var loan in _issues.StreamAllLoansAsync(ct))
        {
            ct.ThrowIfCancellationRequested();

            var fields = new[]
            {
                loan.Id.ToString(),
                Csv(loan.Book?.Title),
                Csv(loan.Book?.Isbn),
                Csv(loan.Member is null ? null : loan.Member.FirstName + " " + loan.Member.LastName),
                loan.IssuedDate.ToString("yyyy-MM-dd"),
                loan.DueDate.ToString("yyyy-MM-dd"),
                loan.ReturnDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                loan.IsReturned ? "Yes" : "No",
                loan.ReIssueCount.ToString(),
                loan.OverdueDays().ToString(),
                loan.Fine.ToString("0.00"),
            };

            await writer.WriteLineAsync(string.Join(',', fields));
        }

        await writer.FlushAsync();
    }

    /// <summary>
    /// Escapes a CSV field. A title containing a comma or quote would otherwise
    /// shift every following column — the classic CSV export bug.
    /// </summary>
    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Raises an event after reloading the names the handlers need. Wrapped in
    /// try/catch because a failing notification must never roll back a loan
    /// that has already been committed.
    /// </summary>
    private async Task RaiseAsync(
        EventHandler<BookIssuedEventArgs>? handler,
        IssuedBook loan,
        CancellationToken ct)
    {
        if (handler is null) return;

        try
        {
            var details = await _context.IssuedBooks
                .AsNoTracking()
                .Include(i => i.Book)
                .Include(i => i.Member)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(i => i.Id == loan.Id, ct);

            if (details is null) return;

            handler.Invoke(this, new BookIssuedEventArgs
            {
                Loan = details,
                MemberName = details.Member.FirstName + " " + details.Member.LastName,
                MemberEmail = details.Member.User.Email,
                BookTitle = details.Book.Title,
            });
        }
        catch (Exception ex)
        {
            // Inner exception preserved so the original stack survives in logs.
            _logger.LogError(ex, "Notification handlers failed for loan {LoanId}", loan.Id);
        }
    }

    private static IssueRequestDto ToRequestDto(IssueRequest r) => new(
        r.Id,
        r.BookId,
        r.Book?.Title ?? string.Empty,
        r.MemberId,
        r.Member is null ? string.Empty : r.Member.FirstName + " " + r.Member.LastName,
        r.RequestedOn,
        r.Status.ToString(),
        r.RejectionReason,
        r.DecidedOn,
        r.IssuedBook?.Id);

    private IssuedBookDto ToLoanDto(IssuedBook i) => new(
        i.Id,
        i.BookId,
        i.Book?.Title ?? string.Empty,
        i.Book?.Isbn ?? string.Empty,
        i.MemberId,
        i.Member is null ? string.Empty : i.Member.FirstName + " " + i.Member.LastName,
        i.IssuedDate,
        i.DueDate,
        i.ReturnDate,
        i.IsReturned,
        i.ReIssueCount,
        Math.Max(0, _loan.MaxReIssues - i.ReIssueCount),
        i.Fine,
        i.OverdueDays(),
        i.IsOverdue(),
        i.DueDate.CooldownEnd(_loan.CooldownMonths));
}
