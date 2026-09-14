namespace BackendAPI.Dtos;

/// <summary>
/// One recommended book plus the reason it was chosen. The reason is not
/// decoration: a recommendation a member cannot understand is one they will
/// not trust, and it makes the feature demonstrable rather than a black box.
/// </summary>
public record RecommendationDto(
    int BookId,
    string Title,
    string Isbn,
    string CategoryName,
    IReadOnlyList<string> Authors,
    string? CoverImagePath,
    int AvailableCopies,
    bool IsAvailable,
    double? AverageRating,
    int TimesIssued,
    /// <summary>Human-readable explanation, e.g. "More History, which you have borrowed twice".</summary>
    string Reason,
    /// <summary>The computed affinity score. Exposed so the ranking can be inspected and defended.</summary>
    double Score);

/// <summary>
/// What the member's history says about their taste. Returned alongside the
/// recommendations so the UI can show what the engine is reasoning from.
/// </summary>
public record TasteProfileDto(
    int BooksBorrowed,
    int BooksRated,
    IReadOnlyList<AffinityDto> TopCategories,
    IReadOnlyList<AffinityDto> TopAuthors);

public record AffinityDto(string Name, double Weight, int Count);

public record RecommendationResponseDto(
    /// <summary>True when the member has no borrowing history and popularity was used instead.</summary>
    bool IsColdStart,
    TasteProfileDto Profile,
    IReadOnlyList<RecommendationDto> Recommendations);
