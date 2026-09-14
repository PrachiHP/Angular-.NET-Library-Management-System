using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Dtos;

public class RegisterDto
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    // Minimum length is the single most effective password rule. Complexity
    // rules push users toward predictable substitutions (P@ssw0rd).
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }
}

public class LoginDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// What a successful login returns. Deliberately does NOT include the password
/// hash, the user's id, or anything else the client has no business knowing —
/// the identity travels inside the signed token instead.
/// </summary>
public record AuthResponseDto(
    string Token,
    DateTime ExpiresAt,
    string Role,
    string Name,
    string Email,
    int? MemberId);
