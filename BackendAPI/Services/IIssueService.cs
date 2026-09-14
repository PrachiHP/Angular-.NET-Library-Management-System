using BackendAPI.Dtos;
using BackendAPI.Models;

namespace BackendAPI.Services;

public interface IIssueService
{
    // ---- requests ----

    Task<IssueRequestDto> CreateRequestAsync(int memberId, CreateIssueRequestDto dto, CancellationToken ct = default);

    Task<IReadOnlyList<IssueRequestDto>> GetRequestsAsync(RequestStatus? status, int? memberId, CancellationToken ct = default);

    Task<IssueRequestDto> ApproveAsync(int requestId, CancellationToken ct = default);

    Task<IssueRequestDto> RejectAsync(int requestId, string reason, CancellationToken ct = default);

    // ---- loans ----

    Task<IReadOnlyList<IssuedBookDto>> GetLoansAsync(int? memberId, bool? onlyOutstanding, CancellationToken ct = default);

    Task<IReadOnlyList<IssuedBookDto>> GetOverdueAsync(CancellationToken ct = default);

    Task<IssuedBookDto> ReturnAsync(int loanId, CancellationToken ct = default);

    Task<IssuedBookDto> ReIssueAsync(int loanId, int memberId, CancellationToken ct = default);

    /// <summary>Whether the given member may request the given book, and why not.</summary>
    Task<BookMemberStatusDto> GetMemberStatusAsync(int memberId, int bookId, CancellationToken ct = default);

    /// <summary>Writes every loan as CSV. TextWriter so the caller picks the destination.</summary>
    Task ExportLoansAsync(TextWriter writer, CancellationToken ct = default);
}
