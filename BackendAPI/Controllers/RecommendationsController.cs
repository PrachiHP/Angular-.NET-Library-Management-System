using BackendAPI.Dtos;
using BackendAPI.Models;
using BackendAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/recommendations")]
[Produces("application/json")]
[Authorize(Roles = nameof(UserRole.Member))]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _recommendations;
    private readonly ICurrentUser _currentUser;

    public RecommendationsController(IRecommendationService recommendations, ICurrentUser currentUser)
    {
        _recommendations = recommendations;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Books suggested for the calling member, with the reason for each.
    /// The member id comes from the token, never the query string — otherwise
    /// one member could read another's taste profile.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(RecommendationResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationResponseDto>> GetMine(
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        // Clamp rather than reject: a caller asking for 10,000 gets a sane page
        // instead of an error, and cannot use the endpoint to scan the catalogue.
        var safeLimit = Math.Clamp(limit, 1, 50);

        return Ok(await _recommendations.GetForMemberAsync(_currentUser.RequireMemberId(), safeLimit, ct));
    }
}
