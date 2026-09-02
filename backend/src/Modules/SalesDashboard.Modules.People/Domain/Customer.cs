namespace SalesDashboard.Modules.People.Domain;

/// <summary>The market segment a customer belongs to. Persisted as text for readable SQL.</summary>
public enum CustomerSegment
{
    Smb = 0,
    MidMarket = 1,
    Enterprise = 2,
}

/// <summary>A customer a sale is made to. Owned by the People module.</summary>
public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public CustomerSegment Segment { get; set; }
}
