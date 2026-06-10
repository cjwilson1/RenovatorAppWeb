namespace RenovatorApp.Infrastructure.Models;

public sealed class Inspection : IRenoCompanyEntity
{
    public Guid InspectionId { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Title { get; set; } = string.Empty;
    public DateTime InspectionDate { get; set; } = DateTime.Today;
    public string InspectorName { get; set; } = string.Empty;
    public string GeneralNotes { get; set; } = string.Empty;
    public Guid PropertyId { get; set; } = Guid.NewGuid();
    public Guid? CustomerId { get; set; }
    public Property Property { get; set; } = null!;
    public Customer? Customer { get; set; }
    public List<MileageTracking> MileageTrackingRecords { get; set; } = [];
    public List<CalendarEvent> CalendarEvents { get; set; } = [];
    public List<Document> Documents { get; set; } = [];
}
