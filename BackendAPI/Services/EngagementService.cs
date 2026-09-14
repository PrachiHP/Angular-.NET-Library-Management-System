using BackendAPI.Data;
using BackendAPI.Dtos;
using BackendAPI.Exceptions;
using BackendAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Services;

public interface IEngagementService
{
    // feedback
    Task<FeedbackDto> AddFeedbackAsync(int memberId, CreateFeedbackDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<FeedbackDto>> GetFeedbackForBookAsync(int bookId, CancellationToken ct = default);

    // quotes
    Task<QuoteDto> AddQuoteAsync(int memberId, CreateQuoteDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<QuoteDto>> GetQuotesForBookAsync(int bookId, CancellationToken ct = default);
    Task<QuoteDto> LikeQuoteAsync(int quoteId, CancellationToken ct = default);

    // problems
    Task<ProblemDto> ReportProblemAsync(int memberId, CreateProblemDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<ProblemDto>> GetProblemsAsync(bool? onlyUnresolved, CancellationToken ct = default);
    Task<ProblemDto> ResolveProblemAsync(int problemId, CancellationToken ct = default);

    // dashboard
    Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default);
}

public class EngagementService : IEngagementService
{
    private const long MaxQuoteImageBytes = 5 * 1024 * 1024;

    private readonly AppDbContext _context;
    private readonly IFileStorageService _files;
    private readonly ILogger<EngagementService> _logger;

    public EngagementService(
        AppDbContext context,
        IFileStorageService files,
        ILogger<EngagementService> logger)
    {
        _context = context;
        _files = files;
        _logger = logger;
    }

    // -----------------------------------------------------------------------
    // Feedback
    // -----------------------------------------------------------------------

    public async Task<FeedbackDto> AddFeedbackAsync(
        int memberId,
        CreateFeedbackDto dto,
        CancellationToken ct = default)
    {
        await EnsureBookExistsAsync(dto.BookId, ct);

        // Only members who actually borrowed the book may review it. Without
        // this, ratings could be gamed by anyone with an account.
        var hasBorrowed = await _context.IssuedBooks
            .AnyAsync(i => i.MemberId == memberId && i.BookId == dto.BookId, ct);

        if (!hasBorrowed)
        {
            throw new BusinessException("You can only review a book you have borrowed.");
        }

        var existing = await _context.Feedbacks
            .FirstOrDefaultAsync(f => f.MemberId == memberId && f.BookId == dto.BookId, ct);

        if (existing is not null)
        {
            // Update rather than reject — a member revising their opinion is
            // legitimate, and one review per member per book keeps the average honest.
            existing.Rating = dto.Rating;
            existing.Comment = dto.Comment?.Trim();
            existing.CreatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return await LoadFeedbackAsync(existing.Id, ct);
        }

        var feedback = new Feedback
        {
            BookId = dto.BookId,
            MemberId = memberId,
            Rating = dto.Rating,
            Comment = dto.Comment?.Trim(),
        };

        _context.Feedbacks.Add(feedback);
        await _context.SaveChangesAsync(ct);

        return await LoadFeedbackAsync(feedback.Id, ct);
    }

    public async Task<IReadOnlyList<FeedbackDto>> GetFeedbackForBookAsync(int bookId, CancellationToken ct = default)
        => await _context.Feedbacks
            .AsNoTracking()
            .Include(f => f.Book)
            .Include(f => f.Member)
            .Where(f => f.BookId == bookId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FeedbackDto(
                f.Id, f.BookId, f.Book.Title, f.MemberId,
                f.Member.FirstName + " " + f.Member.LastName,
                f.Rating, f.Comment, f.CreatedAt))
            .ToListAsync(ct);

    // -----------------------------------------------------------------------
    // Quotes
    // -----------------------------------------------------------------------

    public async Task<QuoteDto> AddQuoteAsync(int memberId, CreateQuoteDto dto, CancellationToken ct = default)
    {
        await EnsureBookExistsAsync(dto.BookId, ct);

        // Same rule as feedback: a quote is a passage you read, so only members
        // who actually borrowed the book may post one.
        var hasBorrowed = await _context.IssuedBooks
            .AnyAsync(i => i.MemberId == memberId && i.BookId == dto.BookId, ct);

        if (!hasBorrowed)
        {
            throw new BusinessException("You can only post a quote from a book you have borrowed.");
        }

        var hasText = !string.IsNullOrWhiteSpace(dto.Text);
        var hasImage = dto.Image is { Length: > 0 };

        // Exactly one of the two, per the specification: typed text OR a photo
        // of the page.
        if (!hasText && !hasImage)
        {
            throw new ValidationException("Provide either quote text or an image.");
        }

        string? imagePath = null;
        if (hasImage)
        {
            imagePath = await _files.SaveAsync(dto.Image!, "quotes", MaxQuoteImageBytes, ct);
        }

        var quote = new BookQuote
        {
            BookId = dto.BookId,
            MemberId = memberId,
            Text = hasText ? dto.Text!.Trim() : null,
            ImagePath = imagePath,
        };

        try
        {
            _context.BookQuotes.Add(quote);
            await _context.SaveChangesAsync(ct);
        }
        catch
        {
            // The file is already on disk but the row failed. Clean up rather
            // than leaving an orphaned upload.
            _files.Delete(imagePath);
            throw;
        }

        return await LoadQuoteAsync(quote.Id, ct);
    }

    public async Task<IReadOnlyList<QuoteDto>> GetQuotesForBookAsync(int bookId, CancellationToken ct = default)
        => await _context.BookQuotes
            .AsNoTracking()
            .Include(q => q.Book)
            .Include(q => q.Member)
            .Where(q => q.BookId == bookId)
            .OrderByDescending(q => q.Likes).ThenByDescending(q => q.CreatedAt)
            .Select(q => new QuoteDto(
                q.Id, q.BookId, q.Book.Title, q.MemberId,
                q.Member.FirstName + " " + q.Member.LastName,
                q.Text, q.ImagePath, q.Likes, q.CreatedAt))
            .ToListAsync(ct);

    public async Task<QuoteDto> LikeQuoteAsync(int quoteId, CancellationToken ct = default)
    {
        var quote = await _context.BookQuotes.FirstOrDefaultAsync(q => q.Id == quoteId, ct)
                    ?? throw new NotFoundException(nameof(BookQuote), quoteId);

        quote.Likes++;
        await _context.SaveChangesAsync(ct);

        return await LoadQuoteAsync(quoteId, ct);
    }

    // -----------------------------------------------------------------------
    // Problems
    // -----------------------------------------------------------------------

    public async Task<ProblemDto> ReportProblemAsync(
        int memberId,
        CreateProblemDto dto,
        CancellationToken ct = default)
    {
        await EnsureBookExistsAsync(dto.BookId, ct);

        var problem = new BookProblem
        {
            BookId = dto.BookId,
            MemberId = memberId,
            ProblemType = dto.ProblemType,
            Description = dto.Description?.Trim(),
        };

        _context.BookProblems.Add(problem);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Member {MemberId} reported {Type} on book {BookId}", memberId, dto.ProblemType, dto.BookId);

        return await LoadProblemAsync(problem.Id, ct);
    }

    public async Task<IReadOnlyList<ProblemDto>> GetProblemsAsync(
        bool? onlyUnresolved,
        CancellationToken ct = default)
    {
        var query = _context.BookProblems
            .AsNoTracking()
            .Include(p => p.Book)
            .Include(p => p.Member)
            .AsQueryable();

        if (onlyUnresolved == true)
        {
            query = query.Where(p => !p.IsResolved);
        }

        var problems = await query.OrderByDescending(p => p.ReportedOn).ToListAsync(ct);
        return problems.Select(ToProblemDto).ToList();
    }

    public async Task<ProblemDto> ResolveProblemAsync(int problemId, CancellationToken ct = default)
    {
        var problem = await _context.BookProblems.FirstOrDefaultAsync(p => p.Id == problemId, ct)
                      ?? throw new NotFoundException(nameof(BookProblem), problemId);

        if (problem.IsResolved)
        {
            throw new BusinessException("This report is already resolved.");
        }

        problem.IsResolved = true;
        problem.ResolvedOn = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return await LoadProblemAsync(problemId, ct);
    }

    // -----------------------------------------------------------------------
    // Dashboard
    // -----------------------------------------------------------------------

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        var popular = await _context.IssuedBooks
            .AsNoTracking()
            .GroupBy(i => new { i.BookId, i.Book.Title })
            .Select(g => new { g.Key.BookId, g.Key.Title, TimesIssued = g.Count() })
            .OrderByDescending(x => x.TimesIssued)
            .Take(5)
            .ToListAsync(ct);

        var popularIds = popular.Select(p => p.BookId).ToList();

        var ratings = await _context.Feedbacks
            .AsNoTracking()
            .Where(f => popularIds.Contains(f.BookId))
            .GroupBy(f => f.BookId)
            .Select(g => new { BookId = g.Key, Avg = g.Average(x => (double)x.Rating) })
            .ToDictionaryAsync(x => x.BookId, x => x.Avg, ct);

        return new DashboardDto(
            TotalBooks: await _context.Books.CountAsync(b => b.IsActive, ct),
            TotalCopies: await _context.Books.Where(b => b.IsActive).SumAsync(b => b.TotalCopies, ct),
            AvailableCopies: await _context.Books.Where(b => b.IsActive).SumAsync(b => b.AvailableCopies, ct),
            TotalMembers: await _context.Members.CountAsync(ct),
            ActiveMembers: await _context.Members.CountAsync(m => m.IsActive, ct),
            PendingRequests: await _context.IssueRequests.CountAsync(r => r.Status == RequestStatus.Pending, ct),
            OutstandingLoans: await _context.IssuedBooks.CountAsync(i => !i.IsReturned, ct),
            OverdueLoans: await _context.IssuedBooks.CountAsync(i => !i.IsReturned && i.DueDate < today, ct),
            OutstandingFines: await _context.IssuedBooks.SumAsync(i => i.Fine, ct),
            UnresolvedProblems: await _context.BookProblems.CountAsync(p => !p.IsResolved, ct),
            PopularBooks: popular
                .Select(p => new PopularBookDto(
                    p.BookId,
                    p.Title,
                    p.TimesIssued,
                    // TryGetValue, not GetValueOrDefault: an unrated book must be
                    // null, not 0.0, or the UI shows "rated zero stars".
                    ratings.TryGetValue(p.BookId, out var avg) ? avg : null))
                .ToList());
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task EnsureBookExistsAsync(int bookId, CancellationToken ct)
    {
        if (!await _context.Books.AnyAsync(b => b.Id == bookId && b.IsActive, ct))
        {
            throw new NotFoundException(nameof(Book), bookId);
        }
    }

    private async Task<FeedbackDto> LoadFeedbackAsync(int id, CancellationToken ct)
        => await _context.Feedbacks.AsNoTracking()
            .Include(f => f.Book).Include(f => f.Member)
            .Where(f => f.Id == id)
            .Select(f => new FeedbackDto(
                f.Id, f.BookId, f.Book.Title, f.MemberId,
                f.Member.FirstName + " " + f.Member.LastName,
                f.Rating, f.Comment, f.CreatedAt))
            .FirstAsync(ct);

    private async Task<QuoteDto> LoadQuoteAsync(int id, CancellationToken ct)
        => await _context.BookQuotes.AsNoTracking()
            .Include(q => q.Book).Include(q => q.Member)
            .Where(q => q.Id == id)
            .Select(q => new QuoteDto(
                q.Id, q.BookId, q.Book.Title, q.MemberId,
                q.Member.FirstName + " " + q.Member.LastName,
                q.Text, q.ImagePath, q.Likes, q.CreatedAt))
            .FirstAsync(ct);

    private async Task<ProblemDto> LoadProblemAsync(int id, CancellationToken ct)
    {
        var problem = await _context.BookProblems.AsNoTracking()
            .Include(p => p.Book).Include(p => p.Member)
            .FirstAsync(p => p.Id == id, ct);

        return ToProblemDto(problem);
    }

    private static ProblemDto ToProblemDto(BookProblem p) => new(
        p.Id,
        p.BookId,
        p.Book?.Title ?? string.Empty,
        p.MemberId,
        p.Member is null ? string.Empty : p.Member.FirstName + " " + p.Member.LastName,
        p.ProblemType.ToString(),
        DisplayLabel(p.ProblemType),
        p.Description,
        p.IsResolved,
        p.ReportedOn,
        p.ResolvedOn);

    /// <summary>
    /// Enum to human-readable label. A switch expression rather than a chain of
    /// ifs — and the compiler warns if a new ProblemType is added without a case.
    /// </summary>
    private static string DisplayLabel(ProblemType type) => type switch
    {
        ProblemType.TornPage => "Torn page",
        ProblemType.MissingPage => "Missing page",
        ProblemType.WaterDamage => "Water damage",
        ProblemType.BrokenSpine => "Broken spine",
        ProblemType.Other => "Other",
        _ => type.ToString(),
    };
}
