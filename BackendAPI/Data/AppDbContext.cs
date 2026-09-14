using BackendAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Data;

/// <summary>
/// The EF Core session with the database. Declared partial so that the bulky
/// seed data lives in AppDbContext.Seed.cs and this file stays about mapping.
/// </summary>
public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Each DbSet becomes a table, and is the entry point for querying it.
    public DbSet<User> Users => Set<User>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<IssueRequest> IssueRequests => Set<IssueRequest>();
    public DbSet<IssuedBook> IssuedBooks => Set<IssuedBook>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<BookQuote> BookQuotes => Set<BookQuote>();
    public DbSet<BookProblem> BookProblems => Set<BookProblem>();

    /// <summary>
    /// Everything convention cannot infer from the entity classes alone.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Composite key: BookAuthor has no Id of its own ---
        modelBuilder.Entity<BookAuthor>()
            .HasKey(ba => new { ba.BookId, ba.AuthorId });

        // --- Enums stored as readable text instead of 0/1/2 ---
        // Costs a few bytes; makes the tables legible in SSMS and keeps the
        // stored value stable if enum members are ever reordered.
        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<IssueRequest>()
            .Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<BookProblem>()
            .Property(p => p.ProblemType)
            .HasConversion<string>()
            .HasMaxLength(20);

        // --- Money needs explicit precision, or SQL Server warns and guesses ---
        modelBuilder.Entity<IssuedBook>()
            .Property(i => i.Fine)
            .HasPrecision(18, 2);

        // --- Lengths: nvarchar(max) everywhere is wasteful and unindexable ---
        modelBuilder.Entity<User>().Property(u => u.Email).HasMaxLength(256).IsRequired();
        modelBuilder.Entity<User>().Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        modelBuilder.Entity<Member>().Property(m => m.FirstName).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<Member>().Property(m => m.LastName).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<Member>().Property(m => m.Phone).HasMaxLength(20);
        modelBuilder.Entity<Member>().Property(m => m.Address).HasMaxLength(500);
        modelBuilder.Entity<Book>().Property(b => b.Title).HasMaxLength(300).IsRequired();
        modelBuilder.Entity<Book>().Property(b => b.Isbn).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<Book>().Property(b => b.CoverImagePath).HasMaxLength(400);
        modelBuilder.Entity<Author>().Property(a => a.Name).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<Category>().Property(c => c.Name).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<BookQuote>().Property(q => q.ImagePath).HasMaxLength(400);

        // --- Uniqueness the business rules depend on ---
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Book>()
            .HasIndex(b => b.Isbn)
            .IsUnique();

        // --- One-to-one: a Member profile belongs to exactly one User ---
        modelBuilder.Entity<Member>()
            .HasOne(m => m.User)
            .WithOne(u => u.Member)
            .HasForeignKey<Member>(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- One-to-one: approving a request produces one IssuedBook ---
        modelBuilder.Entity<IssuedBook>()
            .HasOne(i => i.IssueRequest)
            .WithOne(r => r.IssuedBook)
            .HasForeignKey<IssuedBook>(i => i.IssueRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- Cascade paths ---
        // Book and Member both reach IssuedBook (and Feedback, Quote, Problem).
        // If both cascaded, SQL Server would reject the migration with
        // "may cause cycles or multiple cascade paths". Restrict is also the
        // behaviour we actually want: deleting a book must not silently erase
        // loan history. Books use soft delete (IsActive) for this reason.
        foreach (var fk in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetForeignKeys())
                     .Where(fk => !fk.IsOwnership && fk.DeleteBehavior == DeleteBehavior.Cascade
                                  && fk.DeclaringEntityType.ClrType != typeof(Member)))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }

        SeedData(modelBuilder);
    }
}
