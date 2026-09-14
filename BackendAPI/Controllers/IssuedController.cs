using System.Text;
using BackendAPI.Dtos;
using BackendAPI.Models;
using BackendAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/issued")]
[Authorize]
public class IssuedController : ControllerBase
{
    private readonly IIssueService _issues;
    private readonly ICurrentUser _currentUser;

    public IssuedController(IIssueService issues, ICurrentUser currentUser)
    {
        _issues = issues;
        _currentUser = currentUser;
    }

    /// <summary>Every loan. Librarian only.</summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    [Produces("application/json")]
    public async Task<ActionResult<IReadOnlyList<IssuedBookDto>>> GetAll(
        [FromQuery] bool? onlyOutstanding,
        CancellationToken ct)
        => Ok(await _issues.GetLoansAsync(null, onlyOutstanding, ct));

    /// <summary>Overdue loans, oldest first.</summary>
    [HttpGet("overdue")]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    [Produces("application/json")]
    public async Task<ActionResult<IReadOnlyList<IssuedBookDto>>> GetOverdue(CancellationToken ct)
        => Ok(await _issues.GetOverdueAsync(ct));

    /// <summary>The calling member's own loans.</summary>
    [HttpGet("my")]
    [Authorize(Roles = nameof(UserRole.Member))]
    [Produces("application/json")]
    public async Task<ActionResult<IReadOnlyList<IssuedBookDto>>> GetMine(
        [FromQuery] bool? onlyOutstanding,
        CancellationToken ct)
        => Ok(await _issues.GetLoansAsync(_currentUser.RequireMemberId(), onlyOutstanding, ct));

    /// <summary>Return a book and settle the fine. Librarian only.</summary>
    [HttpPatch("{id:int}/return")]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    [Produces("application/json")]
    public async Task<ActionResult<IssuedBookDto>> Return([FromRoute] int id, CancellationToken ct)
        => Ok(await _issues.ReturnAsync(id, ct));

    /// <summary>Extend the due date. Member only, and only their own loan.</summary>
    [HttpPatch("{id:int}/reissue")]
    [Authorize(Roles = nameof(UserRole.Member))]
    [Produces("application/json")]
    public async Task<ActionResult<IssuedBookDto>> ReIssue([FromRoute] int id, CancellationToken ct)
        => Ok(await _issues.ReIssueAsync(id, _currentUser.RequireMemberId(), ct));

    /// <summary>
    /// CSV download. A StringWriter is passed to the same ExportLoansAsync that
    /// would accept a StreamWriter for a file — the polymorphism is the point.
    /// </summary>
    [HttpGet("export")]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    [Produces("text/csv")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        // 'using' guarantees Dispose runs even if the export throws.
        await using var buffer = new StringWriter();

        await _issues.ExportLoansAsync(buffer, ct);

        var fileName = "issued-books-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".csv";
        return File(Encoding.UTF8.GetBytes(buffer.ToString()), "text/csv", fileName);
    }
}
