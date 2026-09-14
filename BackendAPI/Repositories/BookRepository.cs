using BackendAPI.Data;
using BackendAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Repositories;

public class BookRepository : BaseRepository<Book>, IBookRepository
{
    public BookRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Soft delete. Overrides the base implementation, which would physically
    /// remove the row and orphan every IssuedBook and Feedback referencing it.
    /// </summary>
    public override async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var book = await DbSet.FindAsync(new object?[] { id }, ct);
        if (book is null) return;

        book.IsActive = false;
        await Context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Soft-deleted books do not "exist" as far as callers are concerned.
    /// The inherited version uses FindAsync, which knows nothing about IsActive,
    /// so deleting an already-deleted book wrongly returned 204 instead of 404.
    /// </summary>
    public override async Task<bool> ExistsAsync(int id, CancellationToken ct = default)
        => await DbSet.AnyAsync(b => b.Id == id && b.IsActive, ct);

    /// <summary>Only active books are listed; soft-deleted ones stay hidden.</summary>
    public override async Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken ct = default)
        => await DbSet.AsNoTracking()
                      .Where(b => b.IsActive)
                      .Include(b => b.Category)
                      .OrderBy(b => b.Title)
                      .ToListAsync(ct);

    public async Task<Book?> GetWithDetailsAsync(int id, CancellationToken ct = default)
        => await DbSet.AsNoTracking()
                      .Include(b => b.Category)
                      .Include(b => b.BookAuthors)
                          .ThenInclude(ba => ba.Author)
                      .FirstOrDefaultAsync(b => b.Id == id && b.IsActive, ct);

    public async Task<IReadOnlyList<Book>> SearchAsync(
        string? search,
        int? categoryId,
        bool? onlyAvailable,
        CancellationToken ct = default)
    {
        // IQueryable builds an expression tree — nothing is sent to SQL Server
        // until ToListAsync below. That is what lets filters be composed
        // conditionally and still produce a single query.
        IQueryable<Book> query = DbSet.AsNoTracking()
                                      .Include(b => b.Category)
                                      .Where(b => b.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(b => b.Title.Contains(term) || b.Isbn.Contains(term));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(b => b.CategoryId == categoryId.Value);
        }

        if (onlyAvailable == true)
        {
            query = query.Where(b => b.AvailableCopies > 0);
        }

        return await query.OrderBy(b => b.Title).ToListAsync(ct);
    }

    /// <summary>
    /// Tracked (no AsNoTracking) so EF detects changes on SaveChangesAsync.
    /// BookAuthors is included because updating the author list means diffing
    /// the existing junction rows.
    /// </summary>
    public async Task<Book?> GetForUpdateAsync(int id, CancellationToken ct = default)
        => await DbSet.Include(b => b.BookAuthors)
                      .FirstOrDefaultAsync(b => b.Id == id && b.IsActive, ct);

    public async Task<bool> IsbnExistsAsync(string isbn, int? excludingBookId = null, CancellationToken ct = default)
        => await DbSet.AnyAsync(
            b => b.Isbn == isbn && (excludingBookId == null || b.Id != excludingBookId.Value),
            ct);
}
