namespace RenovatorApp.Infrastructure.Models;

public sealed class BuildingType : IRenoCompanyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Building> Buildings { get; set; } = [];
}
