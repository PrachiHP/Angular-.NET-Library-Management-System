using BackendAPI.Models;

namespace BackendAPI.Extensions;

/// <summary>
/// Extension methods add behaviour to the entity classes without changing them.
/// The entities stay plain data (which keeps EF Core happy) while the rules
/// that read them live in one discoverable place.
/// A static class with static methods whose first parameter is marked 'this'.
/// </summary>
public static class BookExtensions
{
    /// <summary>A book can be borrowed if it is active and a copy is on the shelf.</summary>
    public static bool IsAvailable(this Book book)
        => book.IsActive && book.AvailableCopies > 0;

    /// <summary>Copies currently out on loan.</summary>
    public static int CopiesOnLoan(this Book book)
        => book.TotalCopies - book.AvailableCopies;
}

public static class IssuedBookExtensions
{
    /// <summary>
    /// Days past the due date, measured at ReturnDate (or now if still out).
    /// Never negative — returning early earns no credit.
    /// </summary>
    public static int OverdueDays(this IssuedBook loan, DateTime? asOf = null)
    {
        var reference = loan.ReturnDate ?? asOf ?? DateTime.UtcNow;
        var days = (reference.Date - loan.DueDate.Date).Days;
        return Math.Max(0, days);
    }

    public static bool IsOverdue(this IssuedBook loan, DateTime? asOf = null)
        => !loan.IsReturned && loan.OverdueDays(asOf) > 0;
}

public static class DateTimeExtensions
{
    /// <summary>
    /// True once the cooldown window measured from this date has elapsed.
    /// Used as: loan.DueDate.IsCooldownExpired(3)
    /// </summary>
    public static bool IsCooldownExpired(this DateTime from, int cooldownMonths, DateTime? asOf = null)
        => (asOf ?? DateTime.UtcNow) >= from.AddMonths(cooldownMonths);

    /// <summary>The moment the cooldown measured from this date ends.</summary>
    public static DateTime CooldownEnd(this DateTime from, int cooldownMonths)
        => from.AddMonths(cooldownMonths);
}
