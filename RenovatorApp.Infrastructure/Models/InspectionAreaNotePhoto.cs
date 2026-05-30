namespace RenovatorApp.Infrastructure.Models;

public sealed class InspectionAreaNotePhoto : IRenoCompanyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public Guid PropertyId { get; set; }
    public Guid? BuildingId { get; set; }
    public Guid AreaId { get; set; }
    public Guid AreaNoteId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public InspectionAreaNote? AreaNote { get; set; }
}
