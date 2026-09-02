namespace SalesDashboard.Modules.Sales.Domain;

/// <summary>
/// One line of a sale: a product, a quantity, the price it sold at, and its unit cost. Price and
/// cost are captured on the line (not read live from the product) because they are historical
/// facts — the product's current price must not retroactively change past revenue or margin.
/// </summary>
public class SaleItem
{
    public Guid Id { get; set; }

    public Guid SaleId { get; set; }

    /// <summary>The product sold (Catalog module), referenced by id across the boundary.</summary>
    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    /// <summary>Unit sale price at the time of sale. Line revenue = <see cref="Quantity"/> × this.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Unit cost of goods at the time of sale. Line cost = <see cref="Quantity"/> × this.</summary>
    public decimal UnitCost { get; set; }
}
