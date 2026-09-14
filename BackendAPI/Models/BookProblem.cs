namespace BackendAPI.Models;

/// <summary>
/// A damage or defect report raised by a member against a book.
/// </summary>
public class BookProblem
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public ProblemType ProblemType { get; set; }

    public string? Description { get; set; }

    public bool IsResolved { get; set; }

    public DateTime ReportedOn { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedOn { get; set; }
}
