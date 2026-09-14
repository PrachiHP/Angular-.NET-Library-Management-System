namespace BackendAPI.Services;

/// <summary>
/// Loan policy, bound from the "Loan" and "Fine" sections of appsettings.json.
///
/// The options pattern rather than reading IConfiguration everywhere: the
/// values are validated once at startup, injected as a strongly typed object,
/// and a service that needs them declares that dependency honestly instead of
/// reaching into global configuration.
/// </summary>
public class LoanOptions
{
    public const string SectionName = "Loan";

    /// <summary>Length of a loan, and of each re-issue extension.</summary>
    public int LoanDays { get; set; } = 14;

    /// <summary>Maximum re-issues per issue cycle. Specification says 2.</summary>
    public int MaxReIssues { get; set; } = 2;

    /// <summary>
    /// Months a member must wait before re-requesting the same book, measured
    /// from the final due date rather than the return date.
    /// </summary>
    public int CooldownMonths { get; set; } = 3;
}

public class FineOptions
{
    public const string SectionName = "Fine";

    /// <summary>Rupees per overdue day. Specification says 5.</summary>
    public decimal RatePerDay { get; set; } = 5m;
}
