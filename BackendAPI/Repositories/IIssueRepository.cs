using BackendAPI.Models;

namespace BackendAPI.Repositories;

/// <summary>
/// Queries backing the issue/return flow. Kept together because the two
/// entities are only ever meaningful as a pair — a request becomes a loan.
/// </summary>
public interface IIssueRepository
{
    // ---- requests ----

    Task<IssueRequest?> GetRequestAsync(int id, bool tracked = false, CancellationToken ct = default);

    Task<IReadOnlyList<IssueRequest>> GetRequestsAsync(
        RequestStatus? status,
        int? memberId,
        CancellationToken ct = default);

    Task<bool> HasPendingRequestAsync(int memberId, int bookId, CancellationToken ct = default);

    // ---- loans ----

    Task<IssuedBook?> GetLoanAsync(int id, bool tracked = false, CancellationToken ct = default);

    Task<IReadOnlyList<IssuedBook>> GetLoansAsync(
        int? memberId,
        bool? onlyOutstanding,
        CancellationToken ct = default);

    /// <summary>Unreturned loans past their due date.</summary>
    Task<IReadOnlyList<IssuedBook>> GetOverdueAsync(CancellationToken ct = default);

    /// <summary>True if the member is holding an unreturned copy of this book.</summary>
    Task<bool> IsCurrentlyHeldAsync(int memberId, int bookId, CancellationToken ct = default);

    /// <summary>
    /// The member's most recent loan of this book, returned or not. Used to
    /// evaluate the cooldown window.
    /// </summary>
    Task<IssuedBook?> GetLastLoanAsync(int memberId, int bookId, CancellationToken ct = default);

    /// <summary>
    /// Streams every loan for CSV export. IAsyncEnumerable so a large export
    /// does not materialise the whole table in memory at once.
    /// </summary>
    IAsyncEnumerable<IssuedBook> StreamAllLoansAsync(CancellationToken ct = default);
}
