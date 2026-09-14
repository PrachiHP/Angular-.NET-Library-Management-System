using BackendAPI.Data;
using BackendAPI.Dtos;
using BackendAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BackendAPI.Services;

public interface IRecommendationService
{
    Task<RecommendationResponseDto> GetForMemberAsync(
        int memberId,
        int limit = 10,
        CancellationToken ct = default);
}

/// <summary>
/// Content-based recommender.
///
/// The idea: a member's borrowing history describes their taste as a weighted
/// set of categories and authors. Every book they have not read is then scored
/// by how well it matches that profile. No other member's data is used — which
/// is why it works from the very first loan, where a collaborative approach
/// would need hundreds of users before returning anything.
/// </summary>
public class RecommendationService : IRecommendationService
{
    // ---- Scoring weights -------------------------------------------------
    // An author is a far more specific signal than a category: sharing "Fiction"
    // with a book says little, sharing an author says a lot. Hence the gap.
    private const double AuthorWeight = 3.0;
    private const double CategoryWeight = 1.0;

    // A book the member rated highly should shape their profile more than one
    // they merely borrowed, and a badly rated one should count against it.
    private const double LikedMultiplier = 1.5;   // rating >= 4
    private const double DislikedMultiplier = 0.25; // rating <= 2

    // Small nudges so that, among books with identical affinity, the better
    // regarded and more borrowed one surfaces first. Deliberately far smaller
    // than the affinity weights — this breaks ties, it does not drive ranking.
    private const double RatingTieBreak = 0.10;
    private const double PopularityTieBreak = 0.05;

    private readonly AppDbContext _context;
    private readonly LoanOptions _loan;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        AppDbContext context,
        IOptions<LoanOptions> loan,
        ILogger<RecommendationService> logger)
    {
        _context = context;
        _loan = loan.Value;
        _logger = logger;
    }

    public async Task<RecommendationResponseDto> GetForMemberAsync(
        int memberId,
        int limit = 10,
        CancellationToken ct = default)
    {
        // ---- 1. What has this member read? ----
        var history = await _context.IssuedBooks
            .AsNoTracking()
            .Where(i => i.MemberId == memberId)
            .Include(i => i.Book).ThenInclude(b => b.Category)
            .Include(i => i.Book).ThenInclude(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .ToListAsync(ct);

        // Their own ratings, so a 5-star read counts for more than a 1-star one.
        var ownRatings = await _context.Feedbacks
            .AsNoTracking()
            .Where(f => f.MemberId == memberId)
            .ToDictionaryAsync(f => f.BookId, f => f.Rating, ct);

        // ---- 2. Books to exclude ----
        // HashSet, not List: this is checked once per candidate book, and
        // Contains is O(1) here versus O(n) on a list.
        var excluded = history.Select(i => i.BookId).ToHashSet();

        // A pending request is effectively "already decided" — recommending it
        // again would be noise.
        var pending = await _context.IssueRequests
            .AsNoTracking()
            .Where(r => r.MemberId == memberId && r.Status == RequestStatus.Pending)
            .Select(r => r.BookId)
            .ToListAsync(ct);

        excluded.UnionWith(pending);

        // ---- 3. Build the taste profile ----
        var categoryAffinity = new Dictionary<int, (string Name, double Weight, int Count)>();
        var authorAffinity = new Dictionary<int, (string Name, double Weight, int Count)>();

        foreach (var loan in history)
        {
            var book = loan.Book;
            if (book is null) continue;

            var multiplier = RatingMultiplier(ownRatings, book.Id);

            // Category signal
            if (book.Category is not null)
            {
                Accumulate(categoryAffinity, book.CategoryId, book.Category.Name, CategoryWeight * multiplier);
            }

            // Author signal
            foreach (var link in book.BookAuthors)
            {
                if (link.Author is null) continue;
                Accumulate(authorAffinity, link.AuthorId, link.Author.Name, AuthorWeight * multiplier);
            }
        }

        var profile = BuildProfile(history.Count, ownRatings.Count, categoryAffinity, authorAffinity);

        // ---- 4. Candidate books ----
        var candidates = await _context.Books
            .AsNoTracking()
            .Where(b => b.IsActive && !excluded.Contains(b.Id))
            .Include(b => b.Category)
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return new RecommendationResponseDto(history.Count == 0, profile, Array.Empty<RecommendationDto>());
        }

        // Aggregate stats for tie-breaking, fetched once rather than per book.
        var candidateIds = candidates.Select(b => b.Id).ToList();

        var ratingStats = await _context.Feedbacks
            .AsNoTracking()
            .Where(f => candidateIds.Contains(f.BookId))
            .GroupBy(f => f.BookId)
            .Select(g => new { BookId = g.Key, Avg = g.Average(x => (double)x.Rating) })
            .ToDictionaryAsync(x => x.BookId, x => x.Avg, ct);

        var issueCounts = await _context.IssuedBooks
            .AsNoTracking()
            .Where(i => candidateIds.Contains(i.BookId))
            .GroupBy(i => i.BookId)
            .Select(g => new { BookId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BookId, x => x.Count, ct);

        var maxIssues = issueCounts.Count == 0 ? 1 : Math.Max(1, issueCounts.Values.Max());

        // ---- 5. Cold start ----
        // A member with no history has no profile to match against. Falling back
        // to what the library as a whole reads is the honest answer; returning an
        // empty list would be a worse experience than a generic suggestion.
        var isColdStart = history.Count == 0;

        var scored = candidates
            .Select(book => ScoreBook(
                book,
                categoryAffinity,
                authorAffinity,
                ratingStats,
                issueCounts,
                maxIssues,
                isColdStart))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.AverageRating ?? 0)
            .ThenByDescending(r => r.TimesIssued)
            .ThenBy(r => r.Title)
            .Take(limit)
            .ToList();

        _logger.LogInformation(
            "Recommended {Count} books for member {MemberId} (coldStart={ColdStart}, history={History})",
            scored.Count, memberId, isColdStart, history.Count);

        return new RecommendationResponseDto(isColdStart, profile, scored);
    }

    // -----------------------------------------------------------------------
    // Scoring
    // -----------------------------------------------------------------------

    private RecommendationDto ScoreBook(
        Book book,
        IReadOnlyDictionary<int, (string Name, double Weight, int Count)> categoryAffinity,
        IReadOnlyDictionary<int, (string Name, double Weight, int Count)> authorAffinity,
        IReadOnlyDictionary<int, double> ratingStats,
        IReadOnlyDictionary<int, int> issueCounts,
        int maxIssues,
        bool isColdStart)
    {
        double score = 0;
        string? reason = null;

        // --- Author match: the strongest content signal ---
        var matchedAuthor = book.BookAuthors
            .Where(ba => authorAffinity.ContainsKey(ba.AuthorId))
            .Select(ba => new { ba.AuthorId, Affinity = authorAffinity[ba.AuthorId] })
            .OrderByDescending(x => x.Affinity.Weight)
            .FirstOrDefault();

        if (matchedAuthor is not null)
        {
            score += matchedAuthor.Affinity.Weight;
            reason = "You have read " + matchedAuthor.Affinity.Count + " book(s) by "
                     + matchedAuthor.Affinity.Name + ".";
        }

        // --- Category match ---
        if (categoryAffinity.TryGetValue(book.CategoryId, out var cat))
        {
            score += cat.Weight;

            // Only describe the category when no author matched — the author is
            // the more compelling explanation when both apply.
            reason ??= "More " + cat.Name + ", which you have borrowed "
                       + cat.Count + " time(s).";
        }

        var averageRating = ratingStats.TryGetValue(book.Id, out var avg) ? avg : (double?)null;
        var timesIssued = issueCounts.TryGetValue(book.Id, out var issued) ? issued : 0;

        if (isColdStart)
        {
            // No profile to match: rank purely on what the library reads and
            // rates well, normalised so popularity cannot dwarf rating.
            score = (timesIssued / (double)maxIssues) + ((averageRating ?? 0) / 5.0);

            // Give every book a floor so a brand-new library still returns
            // something rather than an empty page.
            score += 0.01;

            reason = timesIssued > 0
                ? "Popular with other members — borrowed " + timesIssued + " time(s)."
                : "New in the library.";
        }
        else if (score > 0)
        {
            // Tie-breakers, applied only to books that already matched the
            // profile. A well-rated book the member has no affinity for should
            // not outrank a genuine match.
            score += (averageRating ?? 0) * RatingTieBreak;
            score += (timesIssued / (double)maxIssues) * PopularityTieBreak;
        }

        return new RecommendationDto(
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
            book.AvailableCopies > 0,
            averageRating,
            timesIssued,
            reason ?? "Suggested for you.",
            Math.Round(score, 3));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// How much a book in the member's history should shape their profile.
    /// A book they rated highly counts for more; one they disliked counts for
    /// much less, so the engine does not recommend more of what they rejected.
    /// </summary>
    private static double RatingMultiplier(IReadOnlyDictionary<int, int> ownRatings, int bookId)
    {
        if (!ownRatings.TryGetValue(bookId, out var rating))
        {
            // Borrowed but never rated: neutral. Borrowing is a weaker signal
            // than rating, but it is still a signal.
            return 1.0;
        }

        return rating switch
        {
            >= 4 => LikedMultiplier,
            <= 2 => DislikedMultiplier,
            _ => 1.0,
        };
    }

    private static void Accumulate(
        Dictionary<int, (string Name, double Weight, int Count)> map,
        int key,
        string name,
        double weight)
    {
        if (map.TryGetValue(key, out var existing))
        {
            map[key] = (existing.Name, existing.Weight + weight, existing.Count + 1);
        }
        else
        {
            map[key] = (name, weight, 1);
        }
    }

    private static TasteProfileDto BuildProfile(
        int booksBorrowed,
        int booksRated,
        Dictionary<int, (string Name, double Weight, int Count)> categories,
        Dictionary<int, (string Name, double Weight, int Count)> authors)
        => new(
            booksBorrowed,
            booksRated,
            categories.Values
                      .OrderByDescending(c => c.Weight)
                      .Take(5)
                      .Select(c => new AffinityDto(c.Name, Math.Round(c.Weight, 2), c.Count))
                      .ToList(),
            authors.Values
                   .OrderByDescending(a => a.Weight)
                   .Take(5)
                   .Select(a => new AffinityDto(a.Name, Math.Round(a.Weight, 2), a.Count))
                   .ToList());
}
