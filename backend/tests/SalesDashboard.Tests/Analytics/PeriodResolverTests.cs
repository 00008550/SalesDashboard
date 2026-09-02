using SalesDashboard.Modules.Analytics;

namespace SalesDashboard.Tests.Analytics;

/// <summary>Pure tests of the frozen per-preset period semantics, using a fixed instant for "now".
/// DateTimeOffset equality compares instants, so MSK-constructed expectations compare correctly.</summary>
public sealed class PeriodResolverTests
{
    private static DateTimeOffset Msk(int y, int mo, int d, int h = 0, int mi = 0) =>
        new(y, mo, d, h, mi, 0, TimeSpan.FromHours(3));

    private static readonly DateTimeOffset Now = Msk(2026, 6, 15, 14, 30); // 2026-06-15 14:30 MSK

    [Fact]
    public void Today_is_day_to_date_and_previous_is_same_elapsed_yesterday()
    {
        var p = PeriodResolver.ResolvePreset("today", Now);
        Assert.Equal(Msk(2026, 6, 15), p.Current.Start);
        Assert.Equal(Now, p.Current.End);
        Assert.Equal(Msk(2026, 6, 14), p.Previous.Start);
        Assert.Equal(Msk(2026, 6, 14, 14, 30), p.Previous.End); // same elapsed local time
        Assert.Equal("hour", p.Granularity);
    }

    [Fact]
    public void Last7_current_spans_seven_days_and_previous_shifts_back_seven()
    {
        var p = PeriodResolver.ResolvePreset("last7", Now);
        Assert.Equal(Msk(2026, 6, 9), p.Current.Start);
        Assert.Equal(Now, p.Current.End);
        Assert.Equal(Msk(2026, 6, 2), p.Previous.Start);
        Assert.Equal(Now.AddDays(-7), p.Previous.End);
    }

    [Fact]
    public void Last30_current_spans_thirty_days_and_previous_shifts_back_thirty()
    {
        var p = PeriodResolver.ResolvePreset("last30", Now);
        Assert.Equal(Msk(2026, 5, 17), p.Current.Start);
        Assert.Equal(Now, p.Current.End);
        Assert.Equal(Msk(2026, 4, 17), p.Previous.Start);
        Assert.Equal(Now.AddDays(-30), p.Previous.End);
        Assert.Equal("day", p.Granularity);
    }

    [Fact]
    public void ThisMonth_is_month_to_date_with_same_elapsed_previous_month()
    {
        var p = PeriodResolver.ResolvePreset("thisMonth", Now);
        Assert.Equal(Msk(2026, 6, 1), p.Current.Start);
        Assert.Equal(Now, p.Current.End);
        Assert.Equal(Msk(2026, 5, 1), p.Previous.Start);
        Assert.Equal(Msk(2026, 5, 1) + (Now - Msk(2026, 6, 1)), p.Previous.End);
    }

    [Fact]
    public void ThisMonth_caps_previous_end_when_previous_month_is_shorter()
    {
        // 2026-03-31: month-to-date is ~30.5 days, but February has 28 days, so the previous window
        // must be capped at the end of February (= start of March).
        var now = Msk(2026, 3, 31, 12, 0);
        var p = PeriodResolver.ResolvePreset("thisMonth", now);
        Assert.Equal(Msk(2026, 3, 1), p.Current.Start);
        Assert.Equal(Msk(2026, 2, 1), p.Previous.Start);
        Assert.Equal(Msk(2026, 3, 1), p.Previous.End); // capped at end of the shorter previous month
    }

    [Fact]
    public void PreviousMonth_is_full_previous_calendar_month_vs_the_month_before()
    {
        var p = PeriodResolver.ResolvePreset("prevMonth", Now);
        Assert.Equal(Msk(2026, 5, 1), p.Current.Start);
        Assert.Equal(Msk(2026, 6, 1), p.Current.End);
        Assert.Equal(Msk(2026, 4, 1), p.Previous.Start);
        Assert.Equal(Msk(2026, 5, 1), p.Previous.End);
    }

    [Fact]
    public void Custom_inclusive_dates_resolve_to_half_open_and_previous_equal_duration()
    {
        var p = PeriodResolver.ResolveCustom(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));
        Assert.Equal(Msk(2026, 6, 1), p.Current.Start);
        Assert.Equal(Msk(2026, 7, 1), p.Current.End); // 'to' is inclusive -> exclusive next day
        Assert.Equal(Msk(2026, 5, 2), p.Previous.Start); // 30-day window immediately before
        Assert.Equal(Msk(2026, 6, 1), p.Previous.End);
    }

    [Fact]
    public void Custom_rejects_inverted_range() =>
        Assert.Throws<ArgumentException>(() => PeriodResolver.ResolveCustom(new DateOnly(2026, 6, 30), new DateOnly(2026, 6, 1)));

    [Fact]
    public void Custom_accepts_a_range_at_the_maximum_span()
    {
        var from = new DateOnly(2025, 1, 1);
        var to = from.AddDays(PeriodResolver.MaxCustomRangeDays - 1); // inclusive-day count == max
        var p = PeriodResolver.ResolveCustom(from, to);
        Assert.Equal("custom", p.Preset);
    }

    [Fact]
    public void Custom_rejects_a_range_beyond_the_maximum_span()
    {
        var from = new DateOnly(2025, 1, 1);
        var to = from.AddDays(PeriodResolver.MaxCustomRangeDays); // one day too many
        Assert.Throws<ArgumentException>(() => PeriodResolver.ResolveCustom(from, to));
    }

    [Fact]
    public void Unknown_preset_throws() =>
        Assert.Throws<ArgumentException>(() => PeriodResolver.ResolvePreset("yesteryear", Now));
}
