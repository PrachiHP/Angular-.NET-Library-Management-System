using BackendAPI.Dtos;
using BackendAPI.Models;
using BackendAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/issue-requests")]
[Produces("application/json")]
[Authorize]
public class IssueRequestsController : ControllerBase
{
    private readonly IIssueService _issues;
    private readonly ICurrentUser _currentUser;

    public IssueRequestsController(IIssueService issues, ICurrentUser currentUser)
    {
        _issues = issues;
        _currentUser = currentUser;
    }

    /// <summary>Member requests a book.</summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Member))]
    [ProducesResponseType(typeof(IssueRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IssueRequestDto>> Create(
        [FromBody] CreateIssueRequestDto dto,
        CancellationToken ct)
    {
        // MemberId comes from the token, never from the request body — otherwise
        // one member could raise requests in another member's name.
        var created = await _issues.CreateRequestAsync(_currentUser.RequireMemberId(), dto, ct);
        return CreatedAtAction(nameof(GetAll), new { }, created);
    }

    /// <summary>All requests. Librarian only; filter by status.</summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    public async Task<ActionResult<IReadOnlyList<IssueRequestDto>>> GetAll(
        [FromQuery] RequestStatus? status,
        CancellationToken ct)
        => Ok(await _issues.GetRequestsAsync(status, null, ct));

    /// <summary>The calling member's own requests.</summary>
    [HttpGet("my")]
    [Authorize(Roles = nameof(UserRole.Member))]
    public async Task<ActionResult<IReadOnlyList<IssueRequestDto>>> GetMine(
        [FromQuery] RequestStatus? status,
        CancellationToken ct)
        => Ok(await _issues.GetRequestsAsync(status, _currentUser.RequireMemberId(), ct));

    /// <summary>Approve — creates the loan and takes a copy off the shelf.</summary>
    [HttpPatch("{id:int}/approve")]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    public async Task<ActionResult<IssueRequestDto>> Approve([FromRoute] int id, CancellationToken ct)
        => Ok(await _issues.ApproveAsync(id, ct));

    /// <summary>Reject, with a reason the member will see.</summary>
    [HttpPatch("{id:int}/reject")]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    public async Task<ActionResult<IssueRequestDto>> Reject(
        [FromRoute] int id,
        [FromBody] RejectRequestDto dto,
        CancellationToken ct)
        => Ok(await _issues.RejectAsync(id, dto.Reason, ct));
}
