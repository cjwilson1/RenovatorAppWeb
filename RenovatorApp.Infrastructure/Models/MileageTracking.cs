namespace RenovatorApp.Infrastructure.Models;

public sealed class MileageTracking : IRenoCompanyEntity
{
    public Guid MileageTrackingID { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public DateTime TrackingStartedAtUtc { get; set; }
    public double TotalMileage { get; set; }
    public TimeSpan TotalTime { get; set; }
    public string StartingLocation { get; set; } = string.Empty;
    public string StartingPosition { get; set; } = string.Empty;
    public string EndingLocation { get; set; } = string.Empty;
    public string EndingPosition { get; set; } = string.Empty;
    public Guid? InspectionId { get; set; }
    public Inspection? Inspection { get; set; }
    public List<MileageTrackingWaypoint> Waypoints { get; set; } = [];
}
