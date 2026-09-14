using BackendAPI.Dtos;
using BackendAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    /// <summary>Member self-registration.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterDto dto,
        CancellationToken ct)
        => Ok(await _auth.RegisterAsync(dto, ct));

    /// <summary>Exchange credentials for a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginDto dto,
        CancellationToken ct)
        => Ok(await _auth.LoginAsync(dto, ct));

    /// <summary>Echoes back the claims in the caller's token. Handy for debugging.</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> Me([FromServices] ICurrentUser currentUser)
        => Ok(new
        {
            userId = currentUser.UserId,
            memberId = currentUser.MemberId,
            role = currentUser.Role,
            isLibrarian = currentUser.IsLibrarian,
        });
}
