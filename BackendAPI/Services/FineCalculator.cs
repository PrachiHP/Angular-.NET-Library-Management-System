using BackendAPI.Extensions;
using BackendAPI.Models;

namespace BackendAPI.Services;

/// <summary>
/// Overdue fine arithmetic.
///
/// 'sealed' because this is a stateless utility with no meaningful subclass —
/// sealing documents that intent and lets the JIT devirtualise calls.
/// 'static' because it holds no per-instance state, so requiring callers to
/// construct one would be noise.
/// </summary>
public sealed class FineCalculator
{
    /// <summary>Base rate in rupees per overdue day, per the specification.</summary>
    public const decimal DefaultRatePerDay = 5m;

    /// <summary>
    /// Escalating rates: the longer a book is overdue, the higher the daily rate
    /// for days in that band. Populated by the static constructor below.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, decimal> RateBands;

    /// <summary>
    /// A static constructor runs exactly once, lazily, before the first access
    /// to any static member of this type — and the runtime guarantees it is
    /// thread-safe. That is what makes it the right place to build a lookup
    /// table that must never be rebuilt per call.
    /// </summary>
    static FineCalculator()
    {
        RateBands = new Dictionary<int, decimal>
        {
            [0] = DefaultRatePerDay,   // days 1-7
            [7] = DefaultRatePerDay * 2m,   // days 8-30
            [30] = DefaultRatePerDay * 4m,  // day 31 onwards
        };
    }

    // Private constructor: nobody should ever instantiate this. Combined with
    // 'static' members only, it makes misuse impossible rather than merely
    // discouraged.
    private FineCalculator()
    {
    }

    /// <summary>
    /// Flat fine: overdueDays * ratePerDay. This is the rule the specification
    /// states, and the one the API actually applies.
    /// </summary>
    public static decimal Calculate(int overdueDays, decimal ratePerDay = DefaultRatePerDay)
    {
        if (overdueDays <= 0) return 0m;

        // 'checked' makes overflow throw instead of silently wrapping to a
        // negative fine. decimal overflow already throws by default, so this is
        // belt-and-braces — but it documents that the risk was considered.
        checked
        {
            return overdueDays * ratePerDay;
        }
    }

    /// <summary>Convenience overload — method overloading, same name, different signature.</summary>
    public static decimal Calculate(IssuedBook loan, decimal ratePerDay = DefaultRatePerDay)
        => Calculate(loan.OverdueDays(), ratePerDay);

    /// <summary>
    /// Escalating variant using the band table. Not wired into the API — kept
    /// to show the static constructor doing real work, and as the natural
    /// extension point if the library ever adopts tiered penalties.
    /// </summary>
    public static decimal CalculateEscalating(int overdueDays)
    {
        if (overdueDays <= 0) return 0m;

        decimal total = 0m;

        // Descending so each band consumes the days above its threshold first.
        foreach (var band in RateBands.OrderByDescending(b => b.Key))
        {
            if (overdueDays <= band.Key) continue;

            var daysInBand = overdueDays - band.Key;
            total += daysInBand * band.Value;
            overdueDays = band.Key;
        }

        return total;
    }
}
