using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalesDashboard.Contracts;
using SalesDashboard.Infrastructure.Persistence;
using SalesDashboard.Modules.Catalog.Domain;
using SalesDashboard.Modules.People.Domain;
using SalesDashboard.Modules.Sales.Domain;

namespace SalesDashboard.Infrastructure.Seeding;

/// <summary>
/// Fills an empty database with a realistic, intentionally non-uniform dataset. Two properties matter
/// and are both guaranteed here:
///
/// <list type="bullet">
/// <item><b>Deterministic shape.</b> A fixed RNG seed drives every choice, so the distribution
/// (which managers are strong, the seasonality curve, margins, the edge cases) is identical on every
/// rebuild. Sale instants are anchored to a single captured "now" so the trailing 12-month window
/// always contains data on a fresh clone; only the absolute calendar shifts with the build date.</item>
/// <item><b>Idempotent + transactional.</b> Everything runs in one transaction. The
/// <see cref="SeedMarker"/> is checked first and written last, so re-running never duplicates data and
/// a failure mid-way rolls back completely — the marker is only committed once the whole seed succeeds.</item>
/// </list>
/// </summary>
public sealed class DeterministicSeeder(
    WriteDbContext db,
    TimeProvider clock,
    ILogger<DeterministicSeeder> logger)
{
    /// <summary>Bump to force a re-seed (the seeder clears and regenerates when the stored version differs).</summary>
    public const int SeedVersion = 1;

    private const int RngSeed = 73_939_133;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var marker = await db.SeedState.FirstOrDefaultAsync(m => m.Id == 1, ct);
        if (marker is { } m && m.Version == SeedVersion)
        {
            logger.LogInformation("Seed v{Version} already applied ({AppliedAt:u}); skipping.", m.Version, m.AppliedAt);
            return; // transaction disposed without commit — nothing was written
        }

        // Re-seed path (version bump): clear in FK-safe order. No-op on a fresh database.
        await db.SaleItems.ExecuteDeleteAsync(ct);
        await db.Sales.ExecuteDeleteAsync(ct);
        await db.Products.ExecuteDeleteAsync(ct);
        await db.Categories.ExecuteDeleteAsync(ct);
        await db.Customers.ExecuteDeleteAsync(ct);
        await db.Managers.ExecuteDeleteAsync(ct);

        var anchor = clock.GetUtcNow();
        var rng = new Random(RngSeed);

        var previous = db.ChangeTracker.AutoDetectChangesEnabled;
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            var categories = BuildCategories(rng);
            var products = BuildProducts(rng, categories);
            var managers = BuildManagers(rng);
            var customers = BuildCustomers(rng);
            var sales = BuildSales(rng, anchor, managers, customers, products);

            db.Categories.AddRange(categories);
            db.Products.AddRange(products);
            db.Managers.AddRange(managers);
            db.Customers.AddRange(customers);
            db.Sales.AddRange(sales);

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Seeded {Categories} categories, {Products} products, {Managers} managers, {Customers} customers, {Sales} sales.",
                categories.Count, products.Count, managers.Count, customers.Count, sales.Count);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = previous;
        }

        // Marker written only after the full dataset persisted successfully, inside the same transaction.
        if (marker is null)
            db.SeedState.Add(new SeedMarker { Id = 1, Version = SeedVersion, AppliedAt = anchor });
        else
        {
            marker.Version = SeedVersion;
            marker.AppliedAt = anchor;
        }
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
    }

    // ---- generators -------------------------------------------------------------------------

    private static Guid NextGuid(Random r)
    {
        var b = new byte[16];
        r.NextBytes(b);
        return new Guid(b);
    }

    private static decimal Money(double value) => Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Six categories, each with a target gross margin that gives the dataset varied margins.</summary>
    private static readonly (string Name, double MarginTarget, double LowPrice, double HighPrice)[] CategorySpec =
    [
        ("Consumer Drones",     0.34, 499, 2199),
        ("Professional Drones", 0.30, 2999, 12999),
        ("Handheld Cameras",    0.42, 199, 899),
        ("Gimbals & Stabilizers", 0.45, 149, 749),
        ("Batteries & Power",   0.55, 39, 259),
        ("Accessories",         0.62, 15, 189),
    ];

    private List<Category> BuildCategories(Random rng) =>
        [.. CategorySpec.Select(c => new Category { Id = NextGuid(rng), Name = c.Name })];

    private static readonly string[] ProductAdjectives =
        ["Pro", "Air", "Mini", "Max", "SE", "Cine", "Classic", "Plus", "Ultra", "RS", "Nano", "X"];

    private List<Product> BuildProducts(Random rng, List<Category> categories)
    {
        var products = new List<Product>();
        var sku = 1000;
        // ~40 products spread across categories, weighted a little toward the broad categories.
        int[] perCategory = [8, 6, 8, 6, 6, 8];
        for (var ci = 0; ci < categories.Count; ci++)
        {
            var spec = CategorySpec[ci];
            for (var i = 0; i < perCategory[ci]; i++)
            {
                var line = spec.Name.Split(' ')[0];
                var name = $"{line} {ProductAdjectives[rng.Next(ProductAdjectives.Length)]} {rng.Next(1, 9)}";
                var price = spec.LowPrice + rng.NextDouble() * (spec.HighPrice - spec.LowPrice);
                // Cost derives from a per-product margin near the category target, with noise.
                var margin = Math.Clamp(spec.MarginTarget + (rng.NextDouble() - 0.5) * 0.18, 0.08, 0.80);
                var cost = price * (1 - margin);
                products.Add(new Product
                {
                    Id = NextGuid(rng),
                    Name = name,
                    Sku = $"DJI-{sku++}",
                    CategoryId = categories[ci].Id,
                    BasePrice = Money(price),
                    BaseCost = Money(cost),
                });
            }
        }
        return products;
    }

    private static readonly string[] FirstNames =
        ["Alexei", "Marina", "Dmitri", "Sofia", "Ivan", "Elena", "Pavel", "Natalia", "Sergei", "Olga",
         "Nikolai", "Anna", "Viktor", "Yulia", "Andrei", "Ekaterina", "Roman", "Tatiana", "Mikhail", "Irina",
         "Konstantin", "Vera"];

    private static readonly string[] LastNames =
        ["Volkov", "Petrova", "Sokolov", "Ivanova", "Kuznetsov", "Popova", "Morozov", "Novikova", "Fedorov",
         "Lebedeva", "Kozlov", "Egorova", "Pavlov", "Smirnova", "Orlov", "Vasilieva", "Zaytsev", "Belova",
         "Makarov", "Karpova", "Nikitin", "Titova"];

    private static readonly string[] Teams = ["North", "South", "East", "West", "Enterprise", "Channel"];
    // Dark (700/800) shades so white avatar initials meet WCAG AA contrast.
    private static readonly string[] AvatarColors =
        ["#1D4ED8", "#6D28D9", "#BE185D", "#047857", "#B45309", "#B91C1C", "#155E75", "#4338CA", "#3F6212", "#C2410C"];

    private List<Manager> BuildManagers(Random rng)
    {
        const int count = 18; // within the required 15–25
        var managers = new List<Manager>(count);
        for (var i = 0; i < count; i++)
        {
            var first = FirstNames[i % FirstNames.Length];
            var last = LastNames[i % LastNames.Length];
            // A couple of managers are inactive but keep their historical sales (an explicit edge case:
            // inactive managers must still appear in historical rankings).
            var isActive = i is not (4 or 11);
            managers.Add(new Manager
            {
                Id = NextGuid(rng),
                FullName = $"{first} {last}",
                Title = rng.NextDouble() < 0.25 ? "Senior Account Executive" : "Account Executive",
                Team = Teams[i % Teams.Length],
                IsActive = isActive,
                Initials = $"{first[0]}{last[0]}",
                AvatarColor = AvatarColors[i % AvatarColors.Length],
            });
        }
        return managers;
    }

    private static readonly string[] CompanyRoots =
        ["Aerial", "SkyLine", "Horizon", "Vertex", "Summit", "Northwind", "BlueOrbit", "Falcon", "Meridian",
         "Cirrus", "Vantage", "Apex", "Polar", "Delta", "Zephyr", "Granite", "Beacon", "Ironwood", "Cobalt",
         "Vector", "Lumen", "Crest", "Kestrel", "Tundra"];
    private static readonly string[] CompanySuffixes =
        ["Media", "Films", "Surveys", "Logistics", "Agriculture", "Security", "Studios", "Systems", "Group",
         "Robotics", "Inspections", "Analytics"];

    private List<Customer> BuildCustomers(Random rng)
    {
        const int count = 70; // within the required 50–100
        var customers = new List<Customer>(count);
        for (var i = 0; i < count; i++)
        {
            var company = $"{CompanyRoots[i % CompanyRoots.Length]} {CompanySuffixes[(i / CompanyRoots.Length + i) % CompanySuffixes.Length]}";
            // Segment skew: mostly SMB, fewer MidMarket, few Enterprise — drives sale-size variety.
            var roll = rng.NextDouble();
            var segment = roll < 0.60 ? CustomerSegment.Smb : roll < 0.88 ? CustomerSegment.MidMarket : CustomerSegment.Enterprise;
            customers.Add(new Customer
            {
                Id = NextGuid(rng),
                Name = $"{FirstNames[(i * 7) % FirstNames.Length]} {LastNames[(i * 5) % LastNames.Length]}",
                Company = company,
                Segment = segment,
            });
        }
        return customers;
    }

    // Monthly seasonality multipliers (index 1..12), peaking in Q4 with a summer dip.
    private static readonly double[] MonthWeight =
        [0, 0.85, 0.80, 1.00, 1.05, 1.10, 0.95, 0.80, 0.90, 1.10, 1.25, 1.45, 1.30];

    private List<Sale> BuildSales(
        Random rng, DateTimeOffset anchor, List<Manager> managers, List<Customer> customers, List<Product> products)
    {
        // Per-manager strength → relative share of sales and a price/quantity bias. Strong and weak
        // managers, different average checks, all deterministic.
        var strength = managers.Select(_ => 0.3 + rng.NextDouble() * 1.4).ToArray();
        var checkBias = managers.Select(_ => 0.7 + rng.NextDouble() * 0.8).ToArray();
        var totalStrength = strength.Sum();

        // Some managers have a blackout window (no sales) to exercise the "manager with no sales in a
        // period" edge case.
        var blackoutStartDay = new int[managers.Count];
        for (var i = 0; i < managers.Count; i++)
            blackoutStartDay[i] = rng.NextDouble() < 0.25 ? rng.Next(30, 300) : -1;

        const int windowDays = 365;
        const int targetSales = 3200; // within the required 2,000–5,000

        var sales = new List<Sale>(targetSales);
        for (var s = 0; s < targetSales; s++)
        {
            // Pick a day weighted by seasonality (0 = anchor day, up to windowDays ago).
            var dayOffset = PickWeightedDay(rng, anchor, windowDays);
            // Place the sale within its MSK reporting-calendar day, but never after the captured
            // anchor: for the anchor's own day only the elapsed portion is available, so no sale is
            // ever future-dated relative to the seed instant.
            var offset = TimeSpan.FromHours(3);
            var dayStart = new DateTimeOffset(anchor.ToOffset(offset).Date.AddDays(-dayOffset), offset);
            var dayEnd = dayStart.AddDays(1);
            var cap = dayEnd < anchor ? dayEnd : anchor;
            var span = cap - dayStart;
            var occurredAt = (span > TimeSpan.Zero ? dayStart.AddTicks((long)(rng.NextDouble() * span.Ticks)) : dayStart)
                .ToUniversalTime();

            var mi = PickManager(rng, strength, totalStrength);
            // Respect blackout: a ~45-day gap for that manager.
            if (blackoutStartDay[mi] >= 0)
            {
                var absDay = windowDays - dayOffset; // 0 = oldest
                if (absDay >= blackoutStartDay[mi] && absDay < blackoutStartDay[mi] + 45)
                    mi = (mi + 1) % managers.Count; // reassign to keep the gap real for that manager
            }

            var customer = customers[rng.Next(customers.Count)];
            var status = PickStatus(rng);

            // Enterprise customers and strong managers trend toward larger baskets; one in ~120 is a
            // very large deal (the "one huge sale" edge case shows up naturally across the dataset).
            var big = rng.Next(120) == 0 || customer.Segment == CustomerSegment.Enterprise && rng.NextDouble() < 0.35;
            var itemCount = big ? rng.Next(4, 9) : WeightedSmallBasket(rng);

            var sale = new Sale
            {
                Id = NextGuid(rng),
                ManagerId = managers[mi].Id,
                CustomerId = customer.Id,
                OccurredAt = occurredAt,
                Status = status,
                Items = [],
            };

            for (var k = 0; k < itemCount; k++)
            {
                var product = products[rng.Next(products.Count)];
                var qty = big ? rng.Next(1, 12) : WeightedQuantity(rng);
                // Sale price wobbles around the catalog price by the manager's bias and a discount.
                var priceFactor = checkBias[mi] * (0.90 + rng.NextDouble() * 0.18);
                var unitPrice = (double)product.BasePrice * priceFactor;
                var unitCost = (double)product.BaseCost * (0.97 + rng.NextDouble() * 0.06);
                sale.Items.Add(new SaleItem
                {
                    Id = NextGuid(rng),
                    ProductId = product.Id,
                    Quantity = qty,
                    UnitPrice = Money(Math.Max(unitCost, unitPrice)), // never below cost
                    UnitCost = Money(unitCost),
                });
            }
            sales.Add(sale);
        }
        return sales;
    }

    private static int PickWeightedDay(Random rng, DateTimeOffset anchor, int windowDays)
    {
        // Rejection-sample a day whose month weight accepts it, so Q4 gets more sales.
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var d = rng.Next(0, windowDays);
            var month = anchor.AddDays(-d).Month;
            if (rng.NextDouble() < MonthWeight[month] / 1.45)
                return d;
        }
        return rng.Next(0, windowDays);
    }

    private static int PickManager(Random rng, double[] strength, double total)
    {
        var roll = rng.NextDouble() * total;
        var acc = 0.0;
        for (var i = 0; i < strength.Length; i++)
        {
            acc += strength[i];
            if (roll <= acc) return i;
        }
        return strength.Length - 1;
    }

    private static SaleStatus PickStatus(Random rng)
    {
        var roll = rng.NextDouble();
        // ~86% Paid, ~7% Cancelled, ~7% Refunded — both non-Paid statuses well represented.
        return roll < 0.86 ? SaleStatus.Paid : roll < 0.93 ? SaleStatus.Cancelled : SaleStatus.Refunded;
    }

    private static int WeightedSmallBasket(Random rng)
    {
        var roll = rng.NextDouble();
        return roll < 0.55 ? 1 : roll < 0.85 ? 2 : 3;
    }

    private static int WeightedQuantity(Random rng)
    {
        var roll = rng.NextDouble();
        return roll < 0.6 ? 1 : roll < 0.85 ? 2 : roll < 0.96 ? 3 : rng.Next(4, 8);
    }
}
