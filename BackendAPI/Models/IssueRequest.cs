namespace BackendAPI.Models;

/// <summary>
/// A member's request to borrow a book, awaiting librarian decision.
/// Approving one creates the <see cref="IssuedBook"/> record.
/// </summary>
public class IssueRequest
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public DateTime RequestedOn { get; set; } = DateTime.UtcNow;

    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    /// <summary>Set only when Status is Rejected.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>When the librarian approved or rejected. Null while Pending.</summary>
    public DateTime? DecidedOn { get; set; }

    // A request produces at most one issued-book record.
    public IssuedBook? IssuedBook { get; set; }
}
