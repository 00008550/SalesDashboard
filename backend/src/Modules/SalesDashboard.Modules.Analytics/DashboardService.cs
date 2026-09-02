using SalesDashboard.Contracts.Dashboard;

namespace SalesDashboard.Modules.Analytics;

/// <summary>
/// Composes the single dashboard snapshot. It captures <c>now</c> once, resolves the period, runs the
/// analytical reads for the current and previous windows, and assembles the response — including the
/// two server-ranked manager collections and all previous-period comparisons. All ratio/average/delta
/// null-guards live here so the API never emits NaN or Infinity.
/// </summary>
public sealed class DashboardService(DashboardQueries queries, TimeProvider clock)
{
    private const int TopProductsLimit = 8;
    private const int RecentSalesLimit = 12;

    /// <summary>Resolves the period (throws <see cref="ArgumentException"/> on bad input) and builds the snapshot.</summary>
    public async Task<DashboardResponse> BuildAsync(string? preset, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var period = ResolvePeriod(preset, from, to, now);

        await using var conn = await queries.OpenAsync(ct);

        var (curRev, curCost, curPaid) = await queries.GetTotalsAsync(conn, period.Current, ct);
        var (prevRev, prevCost, prevPaid) = await queries.GetTotalsAsync(conn, period.Previous, ct);

        var managersCurrent = await queries.GetManagerAggregatesAsync(conn, period.Current, ct);
        var managersPrevious = await queries.GetManagerAggregatesAsync(conn, period.Previous, ct);
        var rankings = BuildRankings(managersCurrent, managersPrevious);

        var trend = (await queries.GetTrendAsync(conn, period, ct))
            .Select(t => new TrendPoint(t.BucketStart.ToUniversalTime(), t.Revenue, t.GrossProfit, t.PaidSales)).ToList();

        var categoryRows = await queries.GetCategoriesAsync(conn, period.Current, ct);
        var categoryTotal = categoryRows.Sum(c => c.Revenue);
        var categories = categoryRows.Select(c => new CategorySlice(
            c.CategoryId, c.Name, c.Revenue, c.GrossProfit,
            categoryTotal > 0 ? (double)(c.Revenue / categoryTotal) : 0)).ToList();

        var topProducts = (await queries.GetTopProductsAsync(conn, period.Current, TopProductsLimit, ct))
            .Select(p => new ProductRow(p.ProductId, p.Name, p.Category, p.Revenue, p.GrossProfit, p.UnitsSold)).ToList();

        var recent = (await queries.GetRecentSalesAsync(conn, period.Current, RecentSalesLimit, ct))
            .Select(r => new RecentSaleRow(r.SaleId, r.OccurredAt.ToUniversalTime(), r.Manager, r.Customer, r.Company,
                r.Products, r.ItemCount, r.Status, r.Amount, r.Cost, r.GrossProfit)).ToList();

        var summary = BuildSummary(curRev, curCost, curPaid, prevRev, prevCost, prevPaid, rankings.GrossProfit);

        var periodDto = new PeriodDto(
            period.Preset,
            new PeriodWindow(period.Current.Start.ToUniversalTime(), period.Current.End.ToUniversalTime()),
            new PeriodWindow(period.Previous.Start.ToUniversalTime(), period.Previous.End.ToUniversalTime()),
            PeriodResolver.TimezoneLabel,
            period.Granularity);

        return new DashboardResponse(periodDto, summary, rankings, trend, categories, topProducts, recent);
    }

    private static ResolvedPeriod ResolvePeriod(string? preset, DateOnly? from, DateOnly? to, DateTimeOffset now)
    {
        var hasPreset = !string.IsNullOrWhiteSpace(preset);
        var hasCustom = from is not null || to is not null;

        if (hasPreset && hasCustom)
            throw new ArgumentException("Provide either 'preset' or 'from'/'to', not both.");
        if (hasPreset)
            return PeriodResolver.ResolvePreset(preset!, now);
        if (from is { } f && to is { } t)
            return PeriodResolver.ResolveCustom(f, t);
        throw new ArgumentException("Provide either a 'preset' or both 'from' and 'to'.");
    }

    private static SummaryDto BuildSummary(
        decimal curRev, decimal curCost, int curPaid,
        decimal prevRev, decimal prevCost, int prevPaid,
        IReadOnlyList<ManagerRankRow> gpRanking)
    {
        var curGp = curRev - curCost;
        var prevGp = prevRev - prevCost;

        double? curMargin = curRev > 0 ? (double)(curGp / curRev) : null;
        double? prevMargin = prevRev > 0 ? (double)(prevGp / prevRev) : null;
        // Delta in actual percentage points (already ×100), null when either side is null.
        double? marginDeltaPp = curMargin is { } cm && prevMargin is { } pm ? (cm - pm) * 100 : null;

        decimal? curAvg = curPaid > 0 ? curRev / curPaid : null;
        decimal? prevAvg = prevPaid > 0 ? prevRev / prevPaid : null;

        var best = gpRanking.Count > 0
            ? new BestManagerDto(gpRanking[0].ManagerId, gpRanking[0].Name, gpRanking[0].Initials,
                gpRanking[0].AvatarColor, gpRanking[0].GrossProfit)
            : null;

        return new SummaryDto(
            new MoneyKpi(curRev, prevRev, Percent(curRev, prevRev)),
            new MoneyKpi(curCost, prevCost, Percent(curCost, prevCost)),
            new MoneyKpi(curGp, prevGp, Percent(curGp, prevGp)),
            new MarginKpi(curMargin, prevMargin, marginDeltaPp),
            new CountKpi(curPaid, prevPaid, Percent(curPaid, prevPaid)),
            new AverageCheckKpi(curAvg, prevAvg, Percent(curAvg, prevAvg)),
            best);
    }

    private static RankingsDto BuildRankings(IReadOnlyList<ManagerAggRow> current, IReadOnlyList<ManagerAggRow> previous)
    {
        var prevById = previous.ToDictionary(m => m.ManagerId);

        var rows = current.Select(m =>
        {
            var gp = m.Revenue - m.Cost;
            decimal? avg = m.PaidSales > 0 ? m.Revenue / m.PaidSales : null;
            double? margin = m.Revenue > 0 ? (double)(gp / m.Revenue) : null;

            decimal prevGp = 0;
            decimal? prevAvg = null;
            if (prevById.TryGetValue(m.ManagerId, out var p))
            {
                prevGp = p.Revenue - p.Cost;
                prevAvg = p.PaidSales > 0 ? p.Revenue / p.PaidSales : null;
            }

            return new ManagerRankRow(
                0, m.ManagerId, m.FullName, m.Initials, m.AvatarColor, m.IsActive,
                m.PaidSales, m.Revenue, gp, avg, margin,
                prevGp, Percent(gp, prevGp),
                prevAvg, Percent(avg, prevAvg));
        }).ToList();

        // Deterministic tie-break: selected metric desc, then Gross Profit desc, Revenue desc, name, id.
        var byGrossProfit = rows
            .OrderByDescending(r => r.GrossProfit)
            .ThenByDescending(r => r.Revenue)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .ThenBy(r => r.ManagerId)
            .Select((r, i) => r with { Rank = i + 1 })
            .ToList();

        var byAverageCheck = rows
            .OrderByDescending(r => r.AverageCheck ?? decimal.MinValue)
            .ThenByDescending(r => r.GrossProfit)
            .ThenByDescending(r => r.Revenue)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .ThenBy(r => r.ManagerId)
            .Select((r, i) => r with { Rank = i + 1 })
            .ToList();

        return new RankingsDto(byGrossProfit, byAverageCheck);
    }

    // Percentage change as a fraction; null when the previous baseline is zero (never NaN/Infinity).
    private static double? Percent(decimal current, decimal previous) =>
        previous == 0 ? null : (double)((current - previous) / previous);

    private static double? Percent(int current, int previous) =>
        previous == 0 ? null : (double)(current - previous) / previous;

    private static double? Percent(decimal? current, decimal? previous) =>
        current is { } c && previous is { } p && p != 0 ? (double)((c - p) / p) : null;
}
