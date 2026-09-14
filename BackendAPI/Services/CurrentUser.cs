using System.Security.Claims;
using BackendAPI.Exceptions;
using BackendAPI.Models;

namespace BackendAPI.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public int? UserId =>
        int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public int? MemberId =>
        int.TryParse(Principal?.FindFirst("memberId")?.Value, out var id) ? id : null;

    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);

    public bool IsLibrarian => Role == nameof(UserRole.Librarian);

    public int RequireMemberId() =>
        MemberId ?? throw new UnauthorizedException("This action requires a member account.");
}
