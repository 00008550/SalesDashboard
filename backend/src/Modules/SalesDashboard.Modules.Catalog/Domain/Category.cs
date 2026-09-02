namespace SalesDashboard.Modules.Catalog.Domain;

/// <summary>A product category (e.g. "Drones", "Cameras"). Owned by the Catalog module.</summary>
public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
