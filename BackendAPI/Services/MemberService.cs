using BackendAPI.Data;
using BackendAPI.Dtos;
using BackendAPI.Exceptions;
using BackendAPI.Extensions;
using BackendAPI.Models;
using BackendAPI.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Services;

public class MemberService : IMemberService
{
    private readonly IMemberRepository _members;
    private readonly AppDbContext _context;
    private readonly ILogger<MemberService> _logger;

    public MemberService(IMemberRepository members, AppDbContext context, ILogger<MemberService> logger)
    {
        _members = members;
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MemberDto>> GetMembersAsync(
        string? search,
        bool? onlyActive,
        CancellationToken ct = default)
    {
        var members = await _members.SearchAsync(search, onlyActive, ct);
        var ids = members.Select(m => m.Id).ToList();

        // One grouped query for the loan counts rather than one per member —
        // this is the N+1 problem avoided deliberately.
        var activeLoans = await _context.IssuedBooks
            .Where(i => ids.Contains(i.MemberId) && !i.IsReturned)
            .GroupBy(i => i.MemberId)
            .Select(g => new { MemberId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MemberId, x => x.Count, ct);

        return members.Select(m => new MemberDto(
            m.Id,
            m.FirstName + " " + m.LastName,
            m.User.Email,
            m.Phone,
            m.JoinedOn,
            m.IsActive,
            activeLoans.GetValueOrDefault(m.Id))).ToList();
    }

    public async Task<MemberDetailDto> GetMemberAsync(int id, CancellationToken ct = default)
    {
        var member = await _members.GetWithUserAsync(id, ct)
                     ?? throw new NotFoundException(nameof(Member), id);

        var history = await _members.GetLoanHistoryAsync(id, ct);

        return new MemberDetailDto(
            member.Id,
            member.FirstName,
            member.LastName,
            member.FirstName + " " + member.LastName,
            member.User.Email,
            member.Phone,
            member.Address,
            member.JoinedOn,
            member.IsActive,
            history.Select(ToHistoryDto).ToList());
    }

    public async Task<MemberDetailDto> CreateMemberAsync(CreateMemberDto dto, CancellationToken ct = default)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new BusinessException("An account with that email already exists.");
        }

        var member = new Member
        {
            User = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = UserRole.Member,
            },
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Phone = dto.Phone?.Trim(),
            Address = dto.Address?.Trim(),
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Librarian created member {MemberId}", member.Id);
        return await GetMemberAsync(member.Id, ct);
    }

    public async Task<MemberDetailDto> UpdateMemberAsync(int id, UpdateMemberDto dto, CancellationToken ct = default)
    {
        var member = await _context.Members.FirstOrDefaultAsync(m => m.Id == id, ct)
                     ?? throw new NotFoundException(nameof(Member), id);

        member.FirstName = dto.FirstName.Trim();
        member.LastName = dto.LastName.Trim();
        member.Phone = dto.Phone?.Trim();
        member.Address = dto.Address?.Trim();

        await _context.SaveChangesAsync(ct);
        return await GetMemberAsync(id, ct);
    }

    public async Task DeactivateMemberAsync(int id, CancellationToken ct = default)
    {
        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException(nameof(Member), id);

        // Refusing to deactivate someone holding books is a deliberate rule:
        // otherwise the loan becomes unreturnable through the member screens.
        var outstanding = await _context.IssuedBooks
            .CountAsync(i => i.MemberId == id && !i.IsReturned, ct);

        if (outstanding > 0)
        {
            throw new BusinessException(
                "Cannot deactivate: " + outstanding + " book(s) are still issued to this member.");
        }

        member.IsActive = false;
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Deactivated member {MemberId}", id);
    }

    private static LoanHistoryDto ToHistoryDto(IssuedBook i) => new(
        i.Id,
        i.BookId,
        i.Book?.Title ?? string.Empty,
        i.IssuedDate,
        i.DueDate,
        i.ReturnDate,
        i.IsReturned,
        i.ReIssueCount,
        i.Fine,
        i.OverdueDays());
}
