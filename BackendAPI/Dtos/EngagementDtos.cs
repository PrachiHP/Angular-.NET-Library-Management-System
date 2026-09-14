using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Dtos;

// ---- Feedback ----

public class CreateFeedbackDto
{
    [Required, Range(1, int.MaxValue)]
    public int BookId { get; set; }

    [Required, Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Rating { get; set; }

    [StringLength(2000)]
    public string? Comment { get; set; }
}

public record FeedbackDto(
    int Id,
    int BookId,
    string BookTitle,
    int MemberId,
    string MemberName,
    int Rating,
    string? Comment,
    DateTime CreatedAt);

// ---- Quotes ----

public class CreateQuoteDto
{
    [Required, Range(1, int.MaxValue)]
    public int BookId { get; set; }

    /// <summary>
    /// Either Text or an uploaded Image must be supplied. Not both required,
    /// so neither is [Required] — the service enforces the either/or rule.
    /// </summary>
    [StringLength(2000)]
    public string? Text { get; set; }

    public IFormFile? Image { get; set; }
}

public record QuoteDto(
    int Id,
    int BookId,
    string BookTitle,
    int MemberId,
    string MemberName,
    string? Text,
    string? ImagePath,
    int Likes,
    DateTime CreatedAt);

// ---- Problems ----

public class CreateProblemDto
{
    [Required, Range(1, int.MaxValue)]
    public int BookId { get; set; }

    [Required]
    public Models.ProblemType ProblemType { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }
}

public record ProblemDto(
    int Id,
    int BookId,
    string BookTitle,
    int MemberId,
    string MemberName,
    string ProblemType,
    string ProblemLabel,
    string? Description,
    bool IsResolved,
    DateTime ReportedOn,
    DateTime? ResolvedOn);

// ---- Dashboard ----

public record DashboardDto(
    int TotalBooks,
    int TotalCopies,
    int AvailableCopies,
    int TotalMembers,
    int ActiveMembers,
    int PendingRequests,
    int OutstandingLoans,
    int OverdueLoans,
    decimal OutstandingFines,
    int UnresolvedProblems,
    IReadOnlyList<PopularBookDto> PopularBooks);

public record PopularBookDto(int BookId, string Title, int TimesIssued, double? AverageRating);
