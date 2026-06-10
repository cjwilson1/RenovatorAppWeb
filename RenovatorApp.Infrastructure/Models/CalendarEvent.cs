namespace RenovatorApp.Infrastructure.Models;

public sealed class CalendarEvent : IRenoCompanyEntity
{
    public Guid CalendarEventId { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public Guid RenoUserID { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Today;
    public bool AllDay { get; set; }
    public TimeSpan StartTime { get; set; } = new(9, 0, 0);
    public TimeSpan EndTime { get; set; } = new(10, 0, 0);
    public string EventAlertTimes { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? InspectionId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Inspection? Inspection { get; set; }
    public RenoUser? RenoUser { get; set; }
}
