using BackendAPI.Data;
using BackendAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Repositories;

public class IssueRepository : IIssueRepository
{
    private readonly AppDbContext _context;

    public IssueRepository(AppDbContext context)
    {
        _context = context;
    }

    // A read that will be written back must be tracked; a read for display
    // should not be. The flag makes the caller state which it needs.
    private IQueryable<IssueRequest> Requests(bool tracked) =>
        (tracked ? _context.IssueRequests : _context.IssueRequests.AsNoTracking())
            .Include(r => r.Book)
            .Include(r => r.Member)
            .Include(r => r.IssuedBook);

    private IQueryable<IssuedBook> Loans(bool tracked) =>
        (tracked ? _context.IssuedBooks : _context.IssuedBooks.AsNoTracking())
            .Include(i => i.Book)
            .Include(i => i.Member)
                .ThenInclude(m => m.User);

    public async Task<IssueRequest?> GetRequestAsync(int id, bool tracked = false, CancellationToken ct = default)
        => await Requests(tracked).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<IssueRequest>> GetRequestsAsync(
        RequestStatus? status,
        int? memberId,
        CancellationToken ct = default)
    {
        var query = Requests(tracked: false);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (memberId.HasValue)
        {
            query = query.Where(r => r.MemberId == memberId.Value);
        }

        return await query.OrderByDescending(r => r.RequestedOn).ToListAsync(ct);
    }

    public async Task<bool> HasPendingRequestAsync(int memberId, int bookId, CancellationToken ct = default)
        => await _context.IssueRequests.AnyAsync(
            r => r.MemberId == memberId && r.BookId == bookId && r.Status == RequestStatus.Pending,
            ct);

    public async Task<IssuedBook?> GetLoanAsync(int id, bool tracked = false, CancellationToken ct = default)
        => await Loans(tracked).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<IssuedBook>> GetLoansAsync(
        int? memberId,
        bool? onlyOutstanding,
        CancellationToken ct = default)
    {
        var query = Loans(tracked: false);

        if (memberId.HasValue)
        {
            query = query.Where(i => i.MemberId == memberId.Value);
        }

        if (onlyOutstanding == true)
        {
            query = query.Where(i => !i.IsReturned);
        }

        return await query.OrderByDescending(i => i.IssuedDate).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<IssuedBook>> GetOverdueAsync(CancellationToken ct = default)
    {
        // Compared in SQL against today's date, so the filtering happens in the
        // database rather than by pulling every loan into memory.
        var today = DateTime.UtcNow.Date;

        return await Loans(tracked: false)
            .Where(i => !i.IsReturned && i.DueDate < today)
            .OrderBy(i => i.DueDate)
            .ToListAsync(ct);
    }

    public async Task<bool> IsCurrentlyHeldAsync(int memberId, int bookId, CancellationToken ct = default)
        => await _context.IssuedBooks.AnyAsync(
            i => i.MemberId == memberId && i.BookId == bookId && !i.IsReturned,
            ct);

    public async Task<IssuedBook?> GetLastLoanAsync(int memberId, int bookId, CancellationToken ct = default)
        => await _context.IssuedBooks
            .AsNoTracking()
            .Where(i => i.MemberId == memberId && i.BookId == bookId)
            // Ordered by DueDate because the cooldown is measured from the
            // final due date, so the latest due date is the relevant one.
            .OrderByDescending(i => i.DueDate)
            .FirstOrDefaultAsync(ct);

    public IAsyncEnumerable<IssuedBook> StreamAllLoansAsync(CancellationToken ct = default)
        => _context.IssuedBooks
            .AsNoTracking()
            .Include(i => i.Book)
            .Include(i => i.Member)
            .OrderByDescending(i => i.IssuedDate)
            // AsAsyncEnumerable streams rows as the reader yields them, keeping
            // memory flat no matter how many loans exist.
            .AsAsyncEnumerable();
}
