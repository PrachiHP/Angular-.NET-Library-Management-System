namespace BackendAPI.Models;

/// <summary>
/// The physical handover record: this member has this copy until DueDate.
/// Columns mirror the schema in README section 3.
/// </summary>
public class IssuedBook
{
    public int Id { get; set; }

    public int IssueRequestId { get; set; }
    public IssueRequest IssueRequest { get; set; } = null!;

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public DateTime IssuedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Extended by 14 days on each re-issue. The cooldown is measured from this.</summary>
    public DateTime DueDate { get; set; }

    /// <summary>Null until the book comes back.</summary>
    public DateTime? ReturnDate { get; set; }

    public bool IsReturned { get; set; }

    /// <summary>Capped at 2 by IssueService.MAX_REISSUE.</summary>
    public int ReIssueCount { get; set; }

    /// <summary>Overdue days * 5. Calculated on return; decimal(18,2) in SQL.</summary>
    public decimal Fine { get; set; }
}
