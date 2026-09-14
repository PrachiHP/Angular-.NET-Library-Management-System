namespace BackendAPI.Models;

public class Book
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Isbn { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? PublishedYear { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    /// <summary>Total physical copies the library owns.</summary>
    public int TotalCopies { get; set; }

    /// <summary>
    /// Copies on the shelf right now. Decremented on issue, incremented on return.
    /// Denormalised on purpose — the alternative is counting unreturned IssuedBooks
    /// on every catalogue page load.
    /// </summary>
    public int AvailableCopies { get; set; }

    public string? CoverImagePath { get; set; }

    /// <summary>Soft delete. Hard deleting would orphan IssuedBook history.</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
    public ICollection<IssueRequest> IssueRequests { get; set; } = new List<IssueRequest>();
    public ICollection<IssuedBook> IssuedBooks { get; set; } = new List<IssuedBook>();
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    public ICollection<BookQuote> BookQuotes { get; set; } = new List<BookQuote>();
    public ICollection<BookProblem> BookProblems { get; set; } = new List<BookProblem>();
}
