using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Dtos;

/// <summary>
/// What the API returns for a book in a list. A record because responses are
/// immutable once built — there is no reason for a controller to mutate one.
/// </summary>
public record BookDto(
    int Id,
    string Title,
    string Isbn,
    int CategoryId,
    string CategoryName,
    int TotalCopies,
    int AvailableCopies,
    bool IsAvailable,
    string? CoverImagePath);

/// <summary>Single-book response, with the extra detail a list does not need.</summary>
public record BookDetailDto(
    int Id,
    string Title,
    string Isbn,
    string? Description,
    int? PublishedYear,
    int CategoryId,
    string CategoryName,
    int TotalCopies,
    int AvailableCopies,
    bool IsAvailable,
    string? CoverImagePath,
    IReadOnlyList<string> Authors);

/// <summary>
/// Incoming payload for creating a book. A class, not a record, because
/// ASP.NET Core's model binder and the validation attributes below work most
/// naturally with settable properties.
/// Note what is absent: no Id (the database assigns it) and no AvailableCopies
/// (derived from TotalCopies on create). A DTO exposes only what a caller may set.
/// </summary>
public class CreateBookDto
{
    [Required, StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 10)]
    public string Isbn { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(1400, 2100)]
    public int? PublishedYear { get; set; }

    [Required, Range(1, int.MaxValue, ErrorMessage = "A valid category is required.")]
    public int CategoryId { get; set; }

    [Range(1, 10000)]
    public int TotalCopies { get; set; } = 1;

    /// <summary>Ids of existing authors to link via the BookAuthors junction.</summary>
    public List<int> AuthorIds { get; set; } = new();
}

/// <summary>
/// Update payload. Separate from CreateBookDto on purpose: the two diverge over
/// time (a create may require an ISBN that an update forbids changing), and
/// sharing one DTO forces awkward optional fields on both.
/// </summary>
public class UpdateBookDto
{
    [Required, StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 10)]
    public string Isbn { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(1400, 2100)]
    public int? PublishedYear { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    [Range(0, 10000)]
    public int TotalCopies { get; set; }

    public List<int> AuthorIds { get; set; } = new();
}
