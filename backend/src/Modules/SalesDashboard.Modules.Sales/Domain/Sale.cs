using SalesDashboard.Contracts;

namespace SalesDashboard.Modules.Sales.Domain;

/// <summary>
/// A sale made by a manager to a customer on a given instant, with one or more line items.
/// The Sales module owns this entity and its status rules; other modules reach sales data only
/// through the analytical read contract (the <c>sales</c> schema), never this type.
/// </summary>
public class Sale
{
    public Guid Id { get; set; }

    /// <summary>The manager who made the sale (People module). Stored as an id, not a navigation,
    /// to keep the module boundary: Sales does not reference People's entity types.</summary>
    public Guid ManagerId { get; set; }

    /// <summary>The customer the sale was made to (People module).</summary>
    public Guid CustomerId { get; set; }

    /// <summary>The instant the sale occurred, in UTC. Stored as <c>timestamptz</c>. Period filters
    /// are half-open on this instant, so a sale exactly on a boundary lands deterministically.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    public SaleStatus Status { get; set; }

    public List<SaleItem> Items { get; set; } = [];
}
