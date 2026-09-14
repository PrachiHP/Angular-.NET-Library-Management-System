using BackendAPI.Data;
using BackendAPI.Dtos;
using BackendAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Services;

public interface ITrendingService
{
    Task<TrendingResponseDto> GetTrendingAsync(int windowDays = 30, int limit = 10, CancellationToken ct = default);
}

/// <summary>
/// "Trending" is deliberately NOT "most issued" — the dashboard already shows
/// that. Trending measures MOMENTUM: activity in a recent window against the
/// equally long window before it, so a book borrowed four times this month
/// outranks one borrowed forty times over three years but twice lately.
/// </summary>
public class TrendingService : ITrendingService
{
    // Recent volume is the main signal; the change against the previous window
    // is what separates "trending" from "steadily popular".
    private const double VolumeWeight = 1.0;
    private const double MomentumWeight = 1.5;

    // Two members borrowing once each is broader demand than one member
    // borrowing twice, so distinct readers earn a small bonus.
    private const double ReachWeight = 0.5;

    // A request is demand even when it never became a loan — often because no
    // copy was free, which is exactly what a librarian wants to spot.
    private const double RequestWeight = 0.75;

    private readonly AppDbContext _context;
    private readonly ILogger<TrendingService> _logger;

    public TrendingService(AppDbContext context, ILogger<TrendingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TrendingResponseDto> GetTrendingAsync(
        int windowDays = 30,
        int limit = 10,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddDays(-windowDays);
        var previousStart = now.AddDays(-windowDays * 2);

        // Both windows are fetched in one pass rather than two round trips;
        // each loan is then bucketed in memory by its IssuedDate.
        var loans = await _context.IssuedBooks
            .AsNoTracking()
            .Where(i => i.IssuedDate >= previousStart)
            .Include(i => i.Book).ThenInclude(b => b.Category)
            .Include(i => i.Book).ThenInclude(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .ToListAsync(ct);

        var requests = await _context.IssueRequests
            .AsNoTracking()
            .Where(r => r.RequestedOn >= windowStart)
            .Select(r => r.BookId)
            .ToListAsync(ct);

        var requestCounts = requests
            .GroupBy(bookId => bookId)
            .ToDictionary(g => g.Key, g => g.Count());

        var current = loans.Where(i => i.IssuedDate >= windowStart).ToList();
        var previous = loans.Where(i => i.IssuedDate < windowStart).ToList();

        _logger.LogInformation(
            "Trending over {Days} days: {Current} issues now vs {Previous} before",
            windowDays, current.Count, previous.Count);

        return new TrendingResponseDto(
            windowDays,
            windowStart,
            previousStart,
            current.Count,
            previous.Count,
            BuildBooks(current, previous, requestCounts, limit),
            BuildCategories(current, previous, limit),
            BuildAuthors(current, previous, limit));
    }

    // -----------------------------------------------------------------------
    // Books
    // -----------------------------------------------------------------------

    private IReadOnlyList<TrendingBookDto> BuildBooks(
        IReadOnlyList<IssuedBook> current,
        IReadOnlyList<IssuedBook> previous,
        IReadOnlyDictionary<int, int> requestCounts,
        int limit)
    {
        var previousByBook = previous
            .GroupBy(i => i.BookId)
            .ToDictionary(g => g.Key, g => g.Count());

        var results = current
            .Where(i => i.Book is not null)
            .GroupBy(i => i.BookId)
            .Select(group =>
            {
                var book = group.First().Book;
                var nowCount = group.Count();
                var beforeCount = previousByBook.GetValueOrDefault(group.Key);
                var distinctMembers = group.Select(i => i.MemberId).Distinct().Count();
                var requestsInWindow = requestCounts.GetValueOrDefault(group.Key);

                return new TrendingBookDto(
                    book.Id,
                    book.Title,
                    book.Isbn,
                    book.Category?.Name ?? string.Empty,
                    book.BookAuthors.Where(ba => ba.Author is not null)
                                    .Select(ba => ba.Author.Name)
                                    .OrderBy(n => n)
                                    .ToList(),
                    book.CoverImagePath,
                    book.AvailableCopies,
                    book.TotalCopies,
                    nowCount,
                    beforeCount,
                    nowCount - beforeCount,
                    PercentChange(nowCount, beforeCount),
                    distinctMembers,
                    requestsInWindow,
                    Classify(nowCount, beforeCount),
                    Score(nowCount, beforeCount, distinctMembers, requestsInWindow));
            })
            .OrderByDescending(b => b.Score)
            .ThenByDescending(b => b.IssuesInWindow)
            .ThenBy(b => b.Title)
            .Take(limit)
            .ToList();

        return results;
    }

    // -----------------------------------------------------------------------
    // Categories and authors
    // -----------------------------------------------------------------------

    private IReadOnlyList<TrendingGroupDto> BuildCategories(
        IReadOnlyList<IssuedBook> current,
        IReadOnlyList<IssuedBook> previous,
        int limit)
    {
        var previousCounts = previous
            .Where(i => i.Book is not null)
            .GroupBy(i => i.Book.CategoryId)
            .ToDictionary(g => g.Key, g => g.Count());

        return current
            .Where(i => i.Book?.Category is not null)
            .GroupBy(i => new { i.Book.CategoryId, i.Book.Category.Name })
            .Select(group => BuildGroup(
                group.Key.CategoryId,
                group.Key.Name,
                group.ToList(),
                previousCounts.GetValueOrDefault(group.Key.CategoryId)))
            .OrderByDescending(g => g.Score)
            .ThenByDescending(g => g.IssuesInWindow)
            .ThenBy(g => g.Name)
            .Take(limit)
            .ToList();
    }

    private IReadOnlyList<TrendingGroupDto> BuildAuthors(
        IReadOnlyList<IssuedBook> current,
        IReadOnlyList<IssuedBook> previous,
        int limit)
    {
        // A book can have several authors, so one loan contributes to each of
        // them. SelectMany flattens loan -> (loan, author) pairs first.
        var previousCounts = previous
            .Where(i => i.Book is not null)
            .SelectMany(i => i.Book.BookAuthors.Select(ba => ba.AuthorId))
            .GroupBy(authorId => authorId)
            .ToDictionary(g => g.Key, g => g.Count());

        return current
            .Where(i => i.Book is not null)
            .SelectMany(i => i.Book.BookAuthors
                .Where(ba => ba.Author is not null)
                .Select(ba => new { Loan = i, ba.AuthorId, AuthorName = ba.Author.Name }))
            .GroupBy(x => new { x.AuthorId, x.AuthorName })
            .Select(group => BuildGroup(
                group.Key.AuthorId,
                group.Key.AuthorName,
                group.Select(x => x.Loan).ToList(),
                previousCounts.GetValueOrDefault(group.Key.AuthorId)))
            .OrderByDescending(g => g.Score)
            .ThenByDescending(g => g.IssuesInWindow)
            .ThenBy(g => g.Name)
            .Take(limit)
            .ToList();
    }

    /// <summary>Shared shape for a category or author group — the maths is identical.</summary>
    private TrendingGroupDto BuildGroup(int id, string name, IReadOnlyList<IssuedBook> loans, int beforeCount)
    {
        var nowCount = loans.Count;
        var distinctMembers = loans.Select(i => i.MemberId).Distinct().Count();
        var titles = loans.Where(i => i.Book is not null).Select(i => i.Book).ToList();

        var topTitle = titles
            .GroupBy(b => b.Title)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault();

        return new TrendingGroupDto(
            id,
            name,
            nowCount,
            beforeCount,
            nowCount - beforeCount,
            PercentChange(nowCount, beforeCount),
            distinctMembers,
            titles.Select(b => b.Id).Distinct().Count(),
            topTitle,
            Classify(nowCount, beforeCount),
            Score(nowCount, beforeCount, distinctMembers, 0));
    }

    // -----------------------------------------------------------------------
    // Shared maths
    // -----------------------------------------------------------------------

    /// <summary>
    /// Null when there is no previous activity to compare against. Returning 0
    /// or 100 there would be a lie — "infinite growth from zero" is not a
    /// percentage, which is what the New classification is for.
    /// </summary>
    private static double? PercentChange(int now, int before)
    {
        if (before == 0) return null;
        return Math.Round((now - before) / (double)before * 100.0, 1);
    }

    private static TrendDirection Classify(int now, int before) => (now, before) switch
    {
        (> 0, 0) => TrendDirection.New,
        var (n, b) when n > b => TrendDirection.Rising,
        var (n, b) when n < b => TrendDirection.Falling,
        _ => TrendDirection.Steady,
    };

    /// <summary>
    /// Volume says how much is happening; momentum says whether it is
    /// accelerating. Momentum is weighted higher so a genuinely rising title
    /// beats a steadily popular one — otherwise this list would simply repeat
    /// the dashboard's "most issued".
    /// </summary>
    private static double Score(int now, int before, int distinctMembers, int requests)
        => Math.Round(
            (now * VolumeWeight)
            + ((now - before) * MomentumWeight)
            + (distinctMembers * ReachWeight)
            + (requests * RequestWeight),
            2);
}
