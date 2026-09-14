namespace BackendAPI.Models;

/// <summary>
/// A passage a member liked. Either typed as text or uploaded as a photo of
/// the page — exactly one of Text/ImagePath is set, which is why both are nullable.
/// </summary>
public class BookQuote
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public string? Text { get; set; }

    public string? ImagePath { get; set; }

    public int Likes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
