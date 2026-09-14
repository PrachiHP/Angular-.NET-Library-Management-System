namespace BackendAPI.Services;

/// <summary>
/// Reads the caller's identity from the validated JWT. Wrapping IHttpContextAccessor
/// behind this interface keeps HttpContext out of the service layer — services
/// depend on "who is calling", not on ASP.NET Core.
/// </summary>
public interface ICurrentUser
{
    int? UserId { get; }

    /// <summary>Member profile id, or null for a Librarian.</summary>
    int? MemberId { get; }

    string? Role { get; }

    bool IsLibrarian { get; }

    /// <summary>MemberId, or throws if the caller has no member profile.</summary>
    int RequireMemberId();
}
