using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Dtos;

public record MemberDto(
    int Id,
    string FullName,
    string Email,
    string? Phone,
    DateTime JoinedOn,
    bool IsActive,
    int ActiveLoans);

public record MemberDetailDto(
    int Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? Phone,
    string? Address,
    DateTime JoinedOn,
    bool IsActive,
    IReadOnlyList<LoanHistoryDto> History);

public record LoanHistoryDto(
    int IssuedBookId,
    int BookId,
    string BookTitle,
    DateTime IssuedDate,
    DateTime DueDate,
    DateTime? ReturnDate,
    bool IsReturned,
    int ReIssueCount,
    decimal Fine,
    int OverdueDays);

/// <summary>Librarian-created member. Includes credentials, unlike the update DTO.</summary>
public class CreateMemberDto
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

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

/// <summary>
/// Update carries no Email or Password. Changing credentials is a different
/// operation with different rules, and bundling it here would mean an ordinary
/// profile edit could silently reset someone's login.
/// </summary>
public class UpdateMemberDto
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }
}
