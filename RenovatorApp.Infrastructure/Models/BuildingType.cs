namespace RenovatorApp.Infrastructure.Models;

public sealed class BuildingType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public List<Building> Buildings { get; set; } = [];
}
