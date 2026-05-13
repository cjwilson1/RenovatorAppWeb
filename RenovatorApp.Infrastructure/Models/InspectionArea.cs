namespace RenovatorApp.Infrastructure.Models;

public sealed class InspectionArea
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PropertyId { get; set; }
    public Guid? BuildingId { get; set; }
    public Guid AreaTypeId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int OverallRating { get; set; }
    public int SortOrder { get; set; }
    public Property? Property { get; set; }
    public Building? Building { get; set; }
    public InspectionAreaType? AreaType { get; set; }
    public List<InspectionAreaNote> AreaNotes { get; set; } = [];
}
