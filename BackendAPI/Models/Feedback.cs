namespace BackendAPI.Models;

public class Feedback
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    /// <summary>1 to 5. Enforced in the service layer, not the schema.</summary>
    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
