using BackendAPI.Dtos;
using BackendAPI.Models;
using BackendAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
// Class-level [Authorize] WITHOUT a role: every action requires a signed-in
// user by default, so a newly added action is never accidentally public.
//
// The role is declared per action instead. Multiple [Authorize] attributes
// combine with AND, never OR — so a class-level Roles="Librarian" could NOT be
// widened by an action-level [Authorize], and "me" would return 403 to the very
// members it exists to serve.
[Authorize]
public class MembersController : ControllerBase
{
    private readonly IMemberService _members;
    private readonly ICurrentUser _currentUser;

    public MembersController(IMemberService members, ICurrentUser currentUser)
    {
        _members = members;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> GetMembers(
        [FromQuery] string? search,
        [FromQuery] bool? onlyActive,
        CancellationToken ct)
        => Ok(await _members.GetMembersAsync(search, onlyActive, ct));

    /// <summary>
    /// A member's own profile. Declared BEFORE the {id:int} route so "me" is
    /// never mistaken for an id — though the :int constraint would also prevent it.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MemberDetailDto>> GetOwnProfile(CancellationToken ct)
        => Ok(await _members.GetMemberAsync(_currentUser.RequireMemberId(), ct));

    [HttpGet("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    public async Task<ActionResult<MemberDetailDto>> GetMember([FromRoute] int id, CancellationToken ct)
        => Ok(await _members.GetMemberAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    public async Task<ActionResult<MemberDetailDto>> CreateMember(
        [FromBody] CreateMemberDto dto,
        CancellationToken ct)
    {
        var created = await _members.CreateMemberAsync(dto, ct);
        return CreatedAtAction(nameof(GetMember), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    public async Task<ActionResult<MemberDetailDto>> UpdateMember(
        [FromRoute] int id,
        [FromBody] UpdateMemberDto dto,
        CancellationToken ct)
        => Ok(await _members.UpdateMemberAsync(id, dto, ct));

    [HttpPatch("{id:int}/deactivate")]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    public async Task<IActionResult> Deactivate([FromRoute] int id, CancellationToken ct)
    {
        await _members.DeactivateMemberAsync(id, ct);
        return NoContent();
    }
}
