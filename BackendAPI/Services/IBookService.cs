using BackendAPI.Dtos;

namespace BackendAPI.Services;

/// <summary>
/// Business operations on books. Note the signature: DTOs in, DTOs out, no
/// entities and nothing HTTP-shaped. This interface could be called from a
/// console app or a background job unchanged — that is the test of whether
/// the layer boundary is clean.
/// </summary>
public interface IBookService
{
    Task<IReadOnlyList<BookDto>> GetBooksAsync(
        string? search,
        int? categoryId,
        bool? onlyAvailable,
        CancellationToken ct = default);

    Task<BookDetailDto> GetBookAsync(int id, CancellationToken ct = default);

    Task<BookDetailDto> CreateBookAsync(CreateBookDto dto, CancellationToken ct = default);

    Task<BookDetailDto> UpdateBookAsync(int id, UpdateBookDto dto, CancellationToken ct = default);

    Task DeleteBookAsync(int id, CancellationToken ct = default);

    /// <summary>Saves a cover image and returns the updated book.</summary>
    Task<BookDetailDto> UploadCoverAsync(int id, IFormFile file, CancellationToken ct = default);
}
