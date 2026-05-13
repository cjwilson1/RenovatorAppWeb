namespace RenovatorApp.Infrastructure.Models;

public sealed class InspectionAreaNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PropertyId { get; set; }
    public Guid? BuildingId { get; set; }
    public Guid AreaId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Text { get; set; } = string.Empty;
    public InspectionArea? Area { get; set; }
    public List<InspectionAreaNoteEstimateItem> EstimateItems { get; set; } = [];
    public List<InspectionAreaNotePhoto> Photos { get; set; } = [];
}
