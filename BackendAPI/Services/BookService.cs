using BackendAPI.Data;
using BackendAPI.Dtos;
using BackendAPI.Exceptions;
using BackendAPI.Models;
using BackendAPI.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Services;

public class BookService : IBookService
{
    /// <summary>Cover images are small; 2 MB per the specification.</summary>
    private const long MaxCoverBytes = 2 * 1024 * 1024;

    private readonly IBookRepository _books;
    private readonly AppDbContext _context;
    private readonly IFileStorageService _files;
    private readonly ILogger<BookService> _logger;

    public BookService(
        IBookRepository books,
        AppDbContext context,
        IFileStorageService files,
        ILogger<BookService> logger)
    {
        _books = books;
        _context = context;
        _files = files;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BookDto>> GetBooksAsync(
        string? search,
        int? categoryId,
        bool? onlyAvailable,
        CancellationToken ct = default)
    {
        var books = await _books.SearchAsync(search, categoryId, onlyAvailable, ct);
        return books.Select(ToDto).ToList();
    }

    public async Task<BookDetailDto> GetBookAsync(int id, CancellationToken ct = default)
    {
        var book = await _books.GetWithDetailsAsync(id, ct)
                   ?? throw new NotFoundException(nameof(Book), id);

        return ToDetailDto(book);
    }

    public async Task<BookDetailDto> CreateBookAsync(CreateBookDto dto, CancellationToken ct = default)
    {
        await ValidateCategoryAsync(dto.CategoryId, ct);
        await ValidateAuthorsAsync(dto.AuthorIds, ct);

        if (await _books.IsbnExistsAsync(dto.Isbn, null, ct))
        {
            throw new BusinessException($"A book with ISBN {dto.Isbn} already exists.");
        }

        var book = new Book
        {
            Title = dto.Title.Trim(),
            Isbn = dto.Isbn.Trim(),
            Description = dto.Description?.Trim(),
            PublishedYear = dto.PublishedYear,
            CategoryId = dto.CategoryId,
            TotalCopies = dto.TotalCopies,
            // A new book has every copy on the shelf.
            AvailableCopies = dto.TotalCopies,
            IsActive = true,
            BookAuthors = dto.AuthorIds
                             .Distinct()
                             .Select(authorId => new BookAuthor { AuthorId = authorId })
                             .ToList()
        };

        await _books.AddAsync(book, ct);
        _logger.LogInformation("Created book {BookId} ({Title})", book.Id, book.Title);

        return await GetBookAsync(book.Id, ct);
    }

    public async Task<BookDetailDto> UpdateBookAsync(int id, UpdateBookDto dto, CancellationToken ct = default)
    {
        var book = await _books.GetForUpdateAsync(id, ct)
                   ?? throw new NotFoundException(nameof(Book), id);

        await ValidateCategoryAsync(dto.CategoryId, ct);
        await ValidateAuthorsAsync(dto.AuthorIds, ct);

        if (await _books.IsbnExistsAsync(dto.Isbn, id, ct))
        {
            throw new BusinessException($"Another book already uses ISBN {dto.Isbn}.");
        }

        // How many copies are currently out on loan. Total cannot drop below it,
        // or AvailableCopies would go negative and the catalogue would lie.
        var onLoan = book.TotalCopies - book.AvailableCopies;
        if (dto.TotalCopies < onLoan)
        {
            throw new BusinessException(
                $"Cannot reduce total copies to {dto.TotalCopies}: {onLoan} are currently issued.");
        }

        book.Title = dto.Title.Trim();
        book.Isbn = dto.Isbn.Trim();
        book.Description = dto.Description?.Trim();
        book.PublishedYear = dto.PublishedYear;
        book.CategoryId = dto.CategoryId;
        book.TotalCopies = dto.TotalCopies;
        // Preserve the loan count while changing the total.
        book.AvailableCopies = dto.TotalCopies - onLoan;

        // Replace the author links: clear the junction rows, then re-add.
        book.BookAuthors.Clear();
        foreach (var authorId in dto.AuthorIds.Distinct())
        {
            book.BookAuthors.Add(new BookAuthor { BookId = book.Id, AuthorId = authorId });
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Updated book {BookId}", id);

        return await GetBookAsync(id, ct);
    }

    public async Task DeleteBookAsync(int id, CancellationToken ct = default)
    {
        if (!await _books.ExistsAsync(id, ct))
        {
            throw new NotFoundException(nameof(Book), id);
        }

        // BookRepository overrides this to a soft delete.
        await _books.DeleteAsync(id, ct);
        _logger.LogInformation("Soft-deleted book {BookId}", id);
    }

    /// <summary>
    /// Replaces the book's cover. The previous file is deleted only AFTER the
    /// new path is committed — deleting first would lose the old image if the
    /// save then failed.
    /// </summary>
    public async Task<BookDetailDto> UploadCoverAsync(int id, IFormFile file, CancellationToken ct = default)
    {
        var book = await _books.GetForUpdateAsync(id, ct)
                   ?? throw new NotFoundException(nameof(Book), id);

        var previousPath = book.CoverImagePath;

        var newPath = await _files.SaveAsync(file, "covers", MaxCoverBytes, ct);

        try
        {
            book.CoverImagePath = newPath;
            await _context.SaveChangesAsync(ct);
        }
        catch
        {
            // The row did not save, so the new file is orphaned. Remove it and
            // leave the old cover untouched.
            _files.Delete(newPath);
            throw;
        }

        if (!string.IsNullOrEmpty(previousPath) && previousPath != newPath)
        {
            _files.Delete(previousPath);
        }

        _logger.LogInformation("Updated cover for book {BookId}", id);
        return await GetBookAsync(id, ct);
    }

    // ---- helpers ----

    private async Task ValidateCategoryAsync(int categoryId, CancellationToken ct)
    {
        var exists = await _context.Categories.AnyAsync(c => c.Id == categoryId, ct);
        if (!exists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(CreateBookDto.CategoryId)] = new[] { $"Category {categoryId} does not exist." }
            });
        }
    }

    private async Task ValidateAuthorsAsync(IReadOnlyCollection<int> authorIds, CancellationToken ct)
    {
        if (authorIds.Count == 0) return;

        var ids = authorIds.Distinct().ToList();
        var found = await _context.Authors.CountAsync(a => ids.Contains(a.Id), ct);

        if (found != ids.Count)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(CreateBookDto.AuthorIds)] = new[] { "One or more author ids do not exist." }
            });
        }
    }

    // Mapping lives here rather than in the controller so the shape of the
    // response is decided in one place.
    private static BookDto ToDto(Book b) => new(
        b.Id,
        b.Title,
        b.Isbn,
        b.CategoryId,
        b.Category?.Name ?? string.Empty,
        b.TotalCopies,
        b.AvailableCopies,
        b.AvailableCopies > 0,
        b.CoverImagePath);

    private static BookDetailDto ToDetailDto(Book b) => new(
        b.Id,
        b.Title,
        b.Isbn,
        b.Description,
        b.PublishedYear,
        b.CategoryId,
        b.Category?.Name ?? string.Empty,
        b.TotalCopies,
        b.AvailableCopies,
        b.AvailableCopies > 0,
        b.CoverImagePath,
        b.BookAuthors.Select(ba => ba.Author.Name).OrderBy(n => n).ToList());
}
