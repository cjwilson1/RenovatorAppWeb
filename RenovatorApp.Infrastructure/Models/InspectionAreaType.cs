namespace RenovatorApp.Infrastructure.Models;

public sealed class InspectionAreaType : IRenoCompanyEntity
{
    public Guid AreaTypeId { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public InspectionAreaCategory? Category { get; set; }
    public List<InspectionArea> Areas { get; set; } = [];
}
