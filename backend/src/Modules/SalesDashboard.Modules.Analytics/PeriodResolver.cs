namespace SalesDashboard.Modules.Analytics;

/// <summary>A resolved reporting window, half-open [Start, End) as UTC instants.</summary>
public sealed record Window(DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// The current and previous windows for a request, plus the trend bucketing to use. Boundaries are
/// resolved in the reporting timezone (fixed UTC+03:00 / MSK) and expressed as UTC instants. The
/// previous window follows the semantic per-preset rule (not one arithmetic rule) — see
/// <c>.claude/skills/sales-domain</c>.
/// </summary>
public sealed record ResolvedPeriod(
    string Preset,
    Window Current,
    Window Previous,
    string Granularity,
    string TruncUnit,
    string StepInterval);

/// <summary>
/// Pure period resolution. <c>now</c> is captured once per request and passed in, so this is fully
/// deterministic and testable with a fixed clock. The reporting timezone is a fixed +03:00 offset:
/// MSK observes no DST, so a fixed offset is exact, not an approximation.
/// </summary>
public static class PeriodResolver
{
    public static readonly TimeSpan ReportingOffset = TimeSpan.FromHours(3);
    public const string TimezoneLabel = "UTC+03:00 (MSK)";

    public static readonly IReadOnlySet<string> Presets =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "today", "last7", "last30", "thisMonth", "prevMonth" };

    private static DateTimeOffset MskMidnight(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, ReportingOffset);

    /// <summary>Resolves a preset. Throws <see cref="ArgumentException"/> for an unknown preset.</summary>
    public static ResolvedPeriod ResolvePreset(string preset, DateTimeOffset now)
    {
        var nowMsk = now.ToOffset(ReportingOffset);
        var today = MskMidnight(nowMsk.Year, nowMsk.Month, nowMsk.Day);
        var monthStart = MskMidnight(nowMsk.Year, nowMsk.Month, 1);

        Window current, previous;
        switch (preset.ToLowerInvariant())
        {
            case "today":
            {
                current = new Window(today, now);
                var prevStart = today.AddDays(-1);
                previous = new Window(prevStart, prevStart + (now - today)); // same elapsed on the previous day
                break;
            }
            case "last7":
            {
                var start = today.AddDays(-6);
                current = new Window(start, now);
                previous = new Window(start.AddDays(-7), now.AddDays(-7)); // shifted back 7 calendar days
                break;
            }
            case "last30":
            {
                var start = today.AddDays(-29);
                current = new Window(start, now);
                previous = new Window(start.AddDays(-30), now.AddDays(-30)); // shifted back 30 calendar days
                break;
            }
            case "thismonth":
            {
                current = new Window(monthStart, now); // month-to-date
                var prevMonthStart = monthStart.AddMonths(-1);
                var prevEnd = prevMonthStart + (now - monthStart);
                if (prevEnd > monthStart) prevEnd = monthStart; // cap when the previous month is shorter
                previous = new Window(prevMonthStart, prevEnd);
                break;
            }
            case "prevmonth":
            {
                var prevMonthStart = monthStart.AddMonths(-1);
                current = new Window(prevMonthStart, monthStart); // full previous calendar month
                previous = new Window(prevMonthStart.AddMonths(-1), prevMonthStart);
                break;
            }
            default:
                throw new ArgumentException($"Unknown preset '{preset}'.", nameof(preset));
        }

        return Build(preset, current, previous);
    }

    /// <summary>Resolves an inclusive date-only custom range into half-open windows.</summary>
    public static ResolvedPeriod ResolveCustom(DateOnly from, DateOnly to)
    {
        if (to < from) throw new ArgumentException("'to' must not be earlier than 'from'.", nameof(to));

        var start = MskMidnight(from.Year, from.Month, from.Day);
        var endExclusiveDay = to.AddDays(1);
        var end = MskMidnight(endExclusiveDay.Year, endExclusiveDay.Month, endExclusiveDay.Day);

        var length = end - start;
        var previous = new Window(start - length, start);
        return Build("custom", new Window(start, end), previous);
    }

    private static ResolvedPeriod Build(string preset, Window current, Window previous)
    {
        // Windows are computed with the +03:00 reporting offset but stored as UTC instants: Npgsql
        // requires offset-0 DateTimeOffset for timestamptz, and the API echoes UTC boundaries anyway.
        current = new Window(current.Start.ToUniversalTime(), current.End.ToUniversalTime());
        previous = new Window(previous.Start.ToUniversalTime(), previous.End.ToUniversalTime());

        var length = current.End - current.Start;
        var (granularity, unit, step) =
            length <= TimeSpan.FromDays(2) ? ("hour", "hour", "1 hour")
            : length <= TimeSpan.FromDays(92) ? ("day", "day", "1 day")
            : length <= TimeSpan.FromDays(731) ? ("week", "week", "1 week")
            : ("month", "month", "1 month");

        return new ResolvedPeriod(preset, current, previous, granularity, unit, step);
    }
}
