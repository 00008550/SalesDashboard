namespace SalesDashboard.Contracts;

/// <summary>
/// The lifecycle state of a sale. This is the shared vocabulary every module agrees on, so it
/// lives in Contracts rather than inside the Sales module: Analytics filters on it without taking
/// a dependency on the Sales write model, and it is persisted as text so analytical SQL reads
/// <c>WHERE status = 'Paid'</c> rather than a magic integer.
/// </summary>
public enum SaleStatus
{
    /// <summary>Money changed hands. The only status that contributes to Revenue and Gross Profit.</summary>
    Paid = 0,

    /// <summary>The sale never completed. Excluded from every aggregate (revenue, profit, counts, ranking).</summary>
    Cancelled = 1,

    /// <summary>
    /// A previously-paid sale that was financially reversed. Contributes net zero to Revenue and
    /// Gross Profit, but stays visible in operational views (Recent Sales) so the reversal is not
    /// silently hidden. See README "Business rules" for the full rationale.
    /// </summary>
    Refunded = 2,
}
