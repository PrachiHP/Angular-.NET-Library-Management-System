using BackendAPI.Models;

namespace BackendAPI.Repositories;

public interface IMemberRepository : IRepository<Member>
{
    Task<Member?> GetWithUserAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Member>> SearchAsync(string? search, bool? onlyActive, CancellationToken ct = default);

    /// <summary>Loans that are still out, for the member history view.</summary>
    Task<IReadOnlyList<IssuedBook>> GetLoanHistoryAsync(int memberId, CancellationToken ct = default);
}
