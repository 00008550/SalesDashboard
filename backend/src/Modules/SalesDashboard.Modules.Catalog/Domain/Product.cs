namespace SalesDashboard.Modules.Catalog.Domain;

/// <summary>
/// A sellable product in a category. <see cref="BasePrice"/> and <see cref="BaseCost"/> are the
/// catalog's reference figures; the actual figures a sale used are captured on the sale line, so
/// this is a starting point for the seeder and product analytics, not the source of past revenue.
/// </summary>
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }

    public decimal BasePrice { get; set; }
    public decimal BaseCost { get; set; }
}
