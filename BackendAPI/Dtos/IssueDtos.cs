using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Dtos;

// ---------------------------------------------------------------------------
// Issue requests
// ---------------------------------------------------------------------------

public class CreateIssueRequestDto
{
    [Required, Range(1, int.MaxValue)]
    public int BookId { get; set; }
}

public class RejectRequestDto
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public record IssueRequestDto(
    int Id,
    int BookId,
    string BookTitle,
    int MemberId,
    string MemberName,
    DateTime RequestedOn,
    string Status,
    string? RejectionReason,
    DateTime? DecidedOn,
    int? IssuedBookId);

// ---------------------------------------------------------------------------
// Issued books
// ---------------------------------------------------------------------------

public record IssuedBookDto(
    int Id,
    int BookId,
    string BookTitle,
    string BookIsbn,
    int MemberId,
    string MemberName,
    DateTime IssuedDate,
    DateTime DueDate,
    DateTime? ReturnDate,
    bool IsReturned,
    int ReIssueCount,
    int ReIssuesRemaining,
    decimal Fine,
    int OverdueDays,
    bool IsOverdue,
    DateTime CooldownUntil);

/// <summary>
/// Answers "may I request this book?" for the current member, so the UI can
/// disable the button and explain why rather than letting the request fail.
/// </summary>
public record BookMemberStatusDto(
    int BookId,
    bool CanRequest,
    string? Reason,
    DateTime? CooldownUntil,
    bool CurrentlyHeld,
    bool HasPendingRequest,
    // Populated when the member is holding this book, so the detail page can
    // show the due date without a second request.
    int? IssuedBookId = null,
    DateTime? DueDate = null,
    int? DaysUntilDue = null,
    bool IsOverdue = false,
    int ReIssuesRemaining = 0,
    // Feedback and quotes are limited to books the member has actually
    // borrowed, so the UI needs to know before offering the form.
    bool HasEverBorrowed = false);
