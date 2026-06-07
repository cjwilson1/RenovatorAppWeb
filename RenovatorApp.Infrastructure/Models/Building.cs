namespace RenovatorApp.Infrastructure.Models;

public sealed class Building : IRenoCompanyEntity
{
    public Guid BuildingId { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public Guid PropertyId { get; set; }
    public Guid BuildingTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public Property? Property { get; set; }
    public BuildingType? BuildingType { get; set; }
    public List<InspectionArea> Areas { get; set; } = [];
}
