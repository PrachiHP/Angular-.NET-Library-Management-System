namespace BackendAPI.Models;

/// <summary>
/// Junction table for the many-to-many between Books and Authors.
/// Has no Id of its own — the composite (BookId, AuthorId) is the key,
/// configured in AppDbContext.OnModelCreating.
/// </summary>
public class BookAuthor
{
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;
}
