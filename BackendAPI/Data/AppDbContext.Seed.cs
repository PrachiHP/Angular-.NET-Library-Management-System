using BackendAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Data;

/// <summary>
/// The other half of the partial AppDbContext. Seed values must be constant —
/// DateTime.UtcNow here would make EF think the model changed on every run
/// and generate a pointless migration each time.
/// </summary>
public partial class AppDbContext
{
    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Fiction", Description = "Novels and short stories" },
            new Category { Id = 2, Name = "Non-Fiction", Description = "Factual and reference works" },
            new Category { Id = 3, Name = "Science & Technology", Description = "STEM titles" },
            new Category { Id = 4, Name = "History", Description = "Historical accounts and biography" },
            new Category { Id = 5, Name = "Children", Description = "Books for young readers" }
        );

        modelBuilder.Entity<Author>().HasData(
            new Author { Id = 1, Name = "R. K. Narayan", Bio = "Indian writer known for Malgudi Days." },
            new Author { Id = 2, Name = "Andrew Hunt", Bio = "Co-author of The Pragmatic Programmer." },
            new Author { Id = 3, Name = "David Thomas", Bio = "Co-author of The Pragmatic Programmer." }
        );
    }
}
