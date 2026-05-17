namespace RenovatorApp.Infrastructure.Models;

public sealed class Part
{
    public Guid PartId { get; set; } = Guid.NewGuid();
    public Guid PartSourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ModelNumber { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public bool IsPackage { get; set; }
    public int PackageUnits { get; set; }
    public PartSource? PartSource { get; set; }
}
