namespace RenovatorApp.Infrastructure.Models;

public sealed class InspectionAreaNoteEstimateItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PropertyId { get; set; }
    public Guid? BuildingId { get; set; }
    public Guid AreaId { get; set; }
    public Guid AreaNoteId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Name { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public decimal Hours { get; set; }
    public InspectionAreaNote? AreaNote { get; set; }
}
