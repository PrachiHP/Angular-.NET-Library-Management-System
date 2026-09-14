using BackendAPI.Dtos;
using BackendAPI.Models;
using BackendAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/trending")]
[Produces("application/json")]
[Authorize(Roles = nameof(UserRole.Librarian))]
public class TrendingController : ControllerBase
{
    private readonly ITrendingService _trending;

    public TrendingController(ITrendingService trending)
    {
        _trending = trending;
    }

    /// <summary>
    /// Books, categories and authors gaining momentum, comparing the last
    /// <paramref name="days"/> against the equally long window before it.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TrendingResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrendingResponseDto>> Get(
        [FromQuery] int days = 30,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        // Clamped rather than rejected: an out-of-range value gets a sensible
        // window instead of a 400, and the query cost stays bounded.
        var safeDays = Math.Clamp(days, 1, 365);
        var safeLimit = Math.Clamp(limit, 1, 50);

        return Ok(await _trending.GetTrendingAsync(safeDays, safeLimit, ct));
    }
}
