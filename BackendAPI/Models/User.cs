namespace BackendAPI.Models;

/// <summary>
/// Authentication record. Deliberately holds nothing but credentials and role —
/// profile data lives on <see cref="Member"/> so this table stays small and
/// PasswordHash is easy to keep out of API responses.
/// </summary>
public class User
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation: optional because a Librarian User has no Member profile.
    public Member? Member { get; set; }
}
