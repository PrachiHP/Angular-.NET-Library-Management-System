using BackendAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Data;

/// <summary>
/// Creates the demo Librarian and Member accounts at startup. This cannot be
/// done with EF's HasData: BCrypt generates a random salt, so the hash differs
/// on every call and EF would see the model as changed on every migrations add.
///
/// Each account is seeded independently, so adding a new one later still works
/// on a database that already has the others.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        // AppDbContext is Scoped, and this runs outside any request, so a scope
        // must be created explicitly before resolving it.
        using var scope = services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(DataSeeder));

        await SeedLibrarianAsync(context, config, logger);
        await SeedMemberAsync(context, config, logger);
    }

    private static async Task SeedLibrarianAsync(
        AppDbContext context,
        IConfiguration config,
        ILogger logger)
    {
        var email = (config["Seed:LibrarianEmail"] ?? "librarian@library.local").ToLowerInvariant();

        if (await context.Users.AnyAsync(u => u.Email == email))
        {
            return;
        }

        var password = config["Seed:LibrarianPassword"] ?? "Librarian#123";

        context.Users.Add(new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = UserRole.Librarian,
        });

        await context.SaveChangesAsync();
        logger.LogWarning("Seeded demo Librarian {Email} — change this password before real use.", email);
    }

    /// <summary>
    /// A Member needs both a User (credentials) and a Member profile. Assigning
    /// the User to member.User lets EF insert both and wire the foreign key in
    /// one SaveChanges.
    /// </summary>
    private static async Task SeedMemberAsync(
        AppDbContext context,
        IConfiguration config,
        ILogger logger)
    {
        var email = (config["Seed:MemberEmail"] ?? "member@library.local").ToLowerInvariant();

        if (await context.Users.AnyAsync(u => u.Email == email))
        {
            return;
        }

        var password = config["Seed:MemberPassword"] ?? "Member#123";

        context.Members.Add(new Member
        {
            User = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRole.Member,
            },
            FirstName = config["Seed:MemberFirstName"] ?? "Demo",
            LastName = config["Seed:MemberLastName"] ?? "Member",
            Phone = "9876500000",
        });

        await context.SaveChangesAsync();
        logger.LogWarning("Seeded demo Member {Email} — change this password before real use.", email);
    }
}
