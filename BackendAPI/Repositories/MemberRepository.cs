using BackendAPI.Data;
using BackendAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Repositories;

public class MemberRepository : BaseRepository<Member>, IMemberRepository
{
    public MemberRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>Deactivate rather than delete — loan history references this row.</summary>
    public override async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var member = await DbSet.FindAsync(new object?[] { id }, ct);
        if (member is null) return;

        member.IsActive = false;
        await Context.SaveChangesAsync(ct);
    }

    public async Task<Member?> GetWithUserAsync(int id, CancellationToken ct = default)
        => await DbSet.AsNoTracking()
                      .Include(m => m.User)
                      .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<IReadOnlyList<Member>> SearchAsync(
        string? search,
        bool? onlyActive,
        CancellationToken ct = default)
    {
        IQueryable<Member> query = DbSet.AsNoTracking().Include(m => m.User);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(m =>
                m.FirstName.Contains(term) ||
                m.LastName.Contains(term) ||
                m.User.Email.Contains(term));
        }

        if (onlyActive == true)
        {
            query = query.Where(m => m.IsActive);
        }

        return await query.OrderBy(m => m.FirstName).ThenBy(m => m.LastName).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<IssuedBook>> GetLoanHistoryAsync(int memberId, CancellationToken ct = default)
        => await Context.IssuedBooks
                        .AsNoTracking()
                        .Include(i => i.Book)
                        .Where(i => i.MemberId == memberId)
                        .OrderByDescending(i => i.IssuedDate)
                        .ToListAsync(ct);
}
