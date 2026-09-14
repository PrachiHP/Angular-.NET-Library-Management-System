namespace BackendAPI.Models;

/// <summary>
/// Extended profile for a User whose role is Member. One-to-one with User.
/// </summary>
public class Member
{
    public int Id { get; set; }

    // Foreign key + navigation property. EF pairs these by name convention.
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public DateTime JoinedOn { get; set; } = DateTime.UtcNow;

    // Members are deactivated, never deleted — their loan history must survive.
    public bool IsActive { get; set; } = true;

    public ICollection<IssueRequest> IssueRequests { get; set; } = new List<IssueRequest>();
    public ICollection<IssuedBook> IssuedBooks { get; set; } = new List<IssuedBook>();
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    public ICollection<BookQuote> BookQuotes { get; set; } = new List<BookQuote>();
    public ICollection<BookProblem> BookProblems { get; set; } = new List<BookProblem>();
}
