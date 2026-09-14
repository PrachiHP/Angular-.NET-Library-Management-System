using BackendAPI.Dtos;
using BackendAPI.Models;
using BackendAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/feedback")]
[Produces("application/json")]
public class FeedbackController : ControllerBase
{
    private readonly IEngagementService _engagement;
    private readonly ICurrentUser _currentUser;

    public FeedbackController(IEngagementService engagement, ICurrentUser currentUser)
    {
        _engagement = engagement;
        _currentUser = currentUser;
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Member))]
    public async Task<ActionResult<FeedbackDto>> Add([FromBody] CreateFeedbackDto dto, CancellationToken ct)
        => Ok(await _engagement.AddFeedbackAsync(_currentUser.RequireMemberId(), dto, ct));

    [HttpGet("book/{bookId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<FeedbackDto>>> ForBook(
        [FromRoute] int bookId,
        CancellationToken ct)
        => Ok(await _engagement.GetFeedbackForBookAsync(bookId, ct));
}

[ApiController]
[Route("api/quotes")]
[Produces("application/json")]
public class QuotesController : ControllerBase
{
    private readonly IEngagementService _engagement;
    private readonly ICurrentUser _currentUser;

    public QuotesController(IEngagementService engagement, ICurrentUser currentUser)
    {
        _engagement = engagement;
        _currentUser = currentUser;
    }

    /// <summary>
    /// [FromForm], not [FromBody]: this endpoint accepts multipart/form-data
    /// because a quote may carry an uploaded image alongside its fields.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Member))]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<QuoteDto>> Add([FromForm] CreateQuoteDto dto, CancellationToken ct)
        => Ok(await _engagement.AddQuoteAsync(_currentUser.RequireMemberId(), dto, ct));

    [HttpGet("book/{bookId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<QuoteDto>>> ForBook(
        [FromRoute] int bookId,
        CancellationToken ct)
        => Ok(await _engagement.GetQuotesForBookAsync(bookId, ct));

    [HttpPatch("{id:int}/like")]
    [Authorize(Roles = nameof(UserRole.Member))]
    public async Task<ActionResult<QuoteDto>> Like([FromRoute] int id, CancellationToken ct)
        => Ok(await _engagement.LikeQuoteAsync(id, ct));
}

[ApiController]
[Route("api/book-problems")]
[Produces("application/json")]
public class BookProblemsController : ControllerBase
{
    private readonly IEngagementService _engagement;
    private readonly ICurrentUser _currentUser;

    public BookProblemsController(IEngagementService engagement, ICurrentUser currentUser)
    {
        _engagement = engagement;
        _currentUser = currentUser;
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Member))]
    public async Task<ActionResult<ProblemDto>> Report([FromBody] CreateProblemDto dto, CancellationToken ct)
        => Ok(await _engagement.ReportProblemAsync(_currentUser.RequireMemberId(), dto, ct));

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    public async Task<ActionResult<IReadOnlyList<ProblemDto>>> GetAll(
        [FromQuery] bool? onlyUnresolved,
        CancellationToken ct)
        => Ok(await _engagement.GetProblemsAsync(onlyUnresolved, ct));

    [HttpPatch("{id:int}/resolve")]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    public async Task<ActionResult<ProblemDto>> Resolve([FromRoute] int id, CancellationToken ct)
        => Ok(await _engagement.ResolveProblemAsync(id, ct));
}

[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
[Authorize(Roles = nameof(UserRole.Librarian))]
public class DashboardController : ControllerBase
{
    private readonly IEngagementService _engagement;

    public DashboardController(IEngagementService engagement)
    {
        _engagement = engagement;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct)
        => Ok(await _engagement.GetDashboardAsync(ct));
}
