namespace SalesDashboard.Modules.People.Domain;

/// <summary>
/// A sales manager. <see cref="Initials"/> and <see cref="AvatarColor"/> carry the avatar data the
/// dashboard renders without needing image assets. <see cref="IsActive"/> lets the seed include
/// inactive managers (and managers with no sales in a period) as an explicit edge case.
/// </summary>
public class Manager
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>Two-letter initials for the avatar chip.</summary>
    public string Initials { get; set; } = string.Empty;

    /// <summary>Hex color (e.g. "#3B82F6") for the avatar chip background.</summary>
    public string AvatarColor { get; set; } = "#64748B";
}
