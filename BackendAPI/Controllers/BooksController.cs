using BackendAPI.Dtos;
using BackendAPI.Models;
using BackendAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackendAPI.Controllers;

/// <summary>
/// HTTP surface for books. Notice what is NOT here: no EF Core, no LINQ, no
/// try/catch. The controller's only job is to translate HTTP into a service
/// call and a service result back into HTTP.
/// </summary>
[ApiController]
[Route("api/[controller]")]   // -> /api/books
[Produces("application/json")]
public class BooksController : ControllerBase
{
    private readonly IBookService _bookService;

    // Constructor injection: the DI container supplies IBookService because
    // Program.cs maps that interface to BookService.
    public BooksController(IBookService bookService)
    {
        _bookService = bookService;
    }

    /// <summary>List books, with optional search and filters. Public.</summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BookDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookDto>>> GetBooks(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] bool? onlyAvailable,
        CancellationToken ct)
    {
        var books = await _bookService.GetBooksAsync(search, categoryId, onlyAvailable, ct);
        return Ok(books);
    }

    /// <summary>One book, with authors and full detail. Public.</summary>
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BookDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookDetailDto>> GetBook([FromRoute] int id, CancellationToken ct)
    {
        // No null check needed: the service throws NotFoundException, which the
        // global middleware turns into a 404.
        var book = await _bookService.GetBookAsync(id, ct);
        return Ok(book);
    }

    /// <summary>Create a book. Librarian only once auth is added.</summary>
    [Authorize(Roles = nameof(UserRole.Librarian))]
    [HttpPost]
    [ProducesResponseType(typeof(BookDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookDetailDto>> CreateBook(
        [FromBody] CreateBookDto dto,
        CancellationToken ct)
    {
        var created = await _bookService.CreateBookAsync(dto, ct);

        // 201 Created with a Location header pointing at the new resource —
        // the correct REST response for a successful create.
        return CreatedAtAction(nameof(GetBook), new { id = created.Id }, created);
    }

    /// <summary>Replace a book's details. Librarian only once auth is added.</summary>
    [Authorize(Roles = nameof(UserRole.Librarian))]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(BookDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookDetailDto>> UpdateBook(
        [FromRoute] int id,
        [FromBody] UpdateBookDto dto,
        CancellationToken ct)
    {
        var updated = await _bookService.UpdateBookAsync(id, dto, ct);
        return Ok(updated);
    }

    /// <summary>
    /// Whether the calling member may request this book, and if they are
    /// already holding it, when it is due back.
    /// </summary>
    [HttpGet("{id:int}/member-status")]
    [Authorize(Roles = nameof(UserRole.Member))]
    [ProducesResponseType(typeof(BookMemberStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BookMemberStatusDto>> GetMemberStatus(
        [FromRoute] int id,
        [FromServices] IIssueService issues,
        [FromServices] ICurrentUser currentUser,
        CancellationToken ct)
        => Ok(await issues.GetMemberStatusAsync(currentUser.RequireMemberId(), id, ct));

    /// <summary>Upload or replace the cover image. Librarian only.</summary>
    [HttpPost("{id:int}/cover")]
    [Authorize(Roles = nameof(UserRole.Librarian))]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BookDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BookDetailDto>> UploadCover(
        [FromRoute] int id,
        IFormFile file,
        CancellationToken ct)
        => Ok(await _bookService.UploadCoverAsync(id, file, ct));

    /// <summary>Soft delete. Librarian only once auth is added.</summary>
    [Authorize(Roles = nameof(UserRole.Librarian))]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBook([FromRoute] int id, CancellationToken ct)
    {
        await _bookService.DeleteBookAsync(id, ct);

        // 204: succeeded, and there is deliberately no body to return.
        return NoContent();
    }
}
