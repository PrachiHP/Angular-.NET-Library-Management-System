namespace BackendAPI.Dtos;

/// <summary>
/// Which way demand is moving, derived from this window versus the previous one.
/// An enum rather than a raw number so the UI does not re-implement the
/// thresholds and risk disagreeing with the API.
/// </summary>
public enum TrendDirection
{
    /// <summary>Activity now, none in the previous window.</summary>
    New,
    Rising,
    Steady,
    Falling,
}

public record TrendingBookDto(
    int BookId,
    string Title,
    string Isbn,
    string CategoryName,
    IReadOnlyList<string> Authors,
    string? CoverImagePath,
    int AvailableCopies,
    int TotalCopies,
    /// <summary>Issues inside the requested window.</summary>
    int IssuesInWindow,
    /// <summary>Issues in the equally long window immediately before it.</summary>
    int IssuesInPreviousWindow,
    int Change,
    double? PercentChange,
    /// <summary>Distinct members, so one member re-borrowing does not look like broad demand.</summary>
    int DistinctMembers,
    /// <summary>Requests raised in the window — demand that may not have become a loan.</summary>
    int RequestsInWindow,
    TrendDirection Trend,
    double Score);

/// <summary>
/// A trending category or author. Same shape for both, because the question
/// being answered is identical — only the grouping key differs.
/// </summary>
public record TrendingGroupDto(
    int Id,
    string Name,
    int IssuesInWindow,
    int IssuesInPreviousWindow,
    int Change,
    double? PercentChange,
    int DistinctMembers,
    int TitleCount,
    string? TopTitle,
    TrendDirection Trend,
    double Score);

public record TrendingResponseDto(
    int WindowDays,
    DateTime WindowStart,
    DateTime PreviousWindowStart,
    int TotalIssuesInWindow,
    int TotalIssuesInPreviousWindow,
    IReadOnlyList<TrendingBookDto> Books,
    IReadOnlyList<TrendingGroupDto> Categories,
    IReadOnlyList<TrendingGroupDto> Authors);
