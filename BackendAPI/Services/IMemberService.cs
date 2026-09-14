using BackendAPI.Dtos;

namespace BackendAPI.Services;

public interface IMemberService
{
    Task<IReadOnlyList<MemberDto>> GetMembersAsync(string? search, bool? onlyActive, CancellationToken ct = default);

    Task<MemberDetailDto> GetMemberAsync(int id, CancellationToken ct = default);

    Task<MemberDetailDto> CreateMemberAsync(CreateMemberDto dto, CancellationToken ct = default);

    Task<MemberDetailDto> UpdateMemberAsync(int id, UpdateMemberDto dto, CancellationToken ct = default);

    Task DeactivateMemberAsync(int id, CancellationToken ct = default);
}
