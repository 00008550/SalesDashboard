namespace SalesDashboard.Contracts.Dashboard;

// The dashboard API's stable response vocabulary. Every figure here is computed server-side; the
// frontend only formats. Ratios (margin) and averages are nullable and are null — never NaN/Infinity
// — when their denominator is zero. Percentage changes are null when the previous baseline is zero.

/// <summary>Half-open UTC window [Start, End).</summary>
public sealed record PeriodWindow(DateTimeOffset Start, DateTimeOffset End);

/// <summary>The resolved period echoed back so the comparison is transparent and testable.</summary>
public sealed record PeriodDto(
    string Preset,
    PeriodWindow Current,
    PeriodWindow Previous,
    string Timezone,
    string Granularity);

public sealed record MoneyKpi(decimal Current, decimal Previous, double? ChangePercent);
public sealed record CountKpi(int Current, int Previous, double? ChangePercent);
public sealed record AverageCheckKpi(decimal? Current, decimal? Previous, double? ChangePercent);

/// <summary>Margin as a fraction (0.42 = 42%). ChangePoints is the difference in fractions
/// (0.021 = +2.1 percentage points); null when either side is null.</summary>
public sealed record MarginKpi(double? Current, double? Previous, double? ChangePoints);

public sealed record BestManagerDto(Guid ManagerId, string Name, string Initials, string AvatarColor, decimal GrossProfit);

public sealed record SummaryDto(
    MoneyKpi Revenue,
    MoneyKpi GrossProfit,
    MarginKpi Margin,
    CountKpi PaidSales,
    AverageCheckKpi AverageCheck,
    BestManagerDto? BestManager);

/// <summary>One ranked manager row. Rank and ordering are assigned server-side.</summary>
public sealed record ManagerRankRow(
    int Rank,
    Guid ManagerId,
    string Name,
    string Initials,
    string AvatarColor,
    bool Active,
    int PaidSales,
    decimal Revenue,
    decimal GrossProfit,
    decimal? AverageCheck,
    double? Margin,
    decimal GrossProfitPrevious,
    double? GrossProfitChangePercent,
    decimal? AverageCheckPrevious,
    double? AverageCheckChangePercent);

/// <summary>Two pre-ranked collections; the client only chooses which one to display.</summary>
public sealed record RankingsDto(
    IReadOnlyList<ManagerRankRow> GrossProfit,
    IReadOnlyList<ManagerRankRow> AverageCheck);

/// <summary>A continuous, zero-filled trend bucket (Paid sales only).</summary>
public sealed record TrendPoint(DateTimeOffset BucketStart, decimal Revenue, decimal GrossProfit, int PaidSales);

public sealed record CategorySlice(Guid CategoryId, string Name, decimal Revenue, decimal GrossProfit, double Share);

public sealed record ProductRow(Guid ProductId, string Name, string Category, decimal Revenue, decimal GrossProfit, int UnitsSold);

/// <summary>A recent sale shown with its ORIGINAL amount/cost/profit and its status, so Cancelled/
/// Refunded rows visibly do not reconcile with the net financial KPIs.</summary>
public sealed record RecentSaleRow(
    Guid SaleId,
    DateTimeOffset OccurredAt,
    string Manager,
    string Customer,
    string Company,
    string Products,
    int ItemCount,
    string Status,
    decimal Amount,
    decimal Cost,
    decimal GrossProfit);

public sealed record DashboardResponse(
    PeriodDto Period,
    SummaryDto Summary,
    RankingsDto Rankings,
    IReadOnlyList<TrendPoint> Trend,
    IReadOnlyList<CategorySlice> Categories,
    IReadOnlyList<ProductRow> TopProducts,
    IReadOnlyList<RecentSaleRow> RecentSales);
