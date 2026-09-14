using BackendAPI.Models;

namespace BackendAPI.Repositories;

/// <summary>
/// Book-specific queries on top of the generic contract. Inheriting the
/// interface means callers get all of IRepository&lt;Book&gt; plus these.
/// </summary>
public interface IBookRepository : IRepository<Book>
{
    /// <summary>Book with Category and Authors eagerly loaded.</summary>
    Task<Book?> GetWithDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>Active books, optionally filtered. All filters are optional.</summary>
    Task<IReadOnlyList<Book>> SearchAsync(
        string? search,
        int? categoryId,
        bool? onlyAvailable,
        CancellationToken ct = default);

    /// <summary>True if another book already uses this ISBN.</summary>
    Task<bool> IsbnExistsAsync(string isbn, int? excludingBookId = null, CancellationToken ct = default);

    /// <summary>Tracked book with its author links loaded, for editing.</summary>
    Task<Book?> GetForUpdateAsync(int id, CancellationToken ct = default);
}
