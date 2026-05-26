namespace RenovatorApp.Infrastructure.Models;

public sealed class MileageTracking
{
    public Guid UniqueId { get; set; } = Guid.NewGuid();
    public DateTime TrackingStartedAtUtc { get; set; }
    public double TotalMileage { get; set; }
    public TimeSpan TotalTime { get; set; }
    public string StartingLocation { get; set; } = string.Empty;
    public string StartingPosition { get; set; } = string.Empty;
    public string EndingLocation { get; set; } = string.Empty;
    public string EndingPosition { get; set; } = string.Empty;
    public List<MileageTrackingWaypoint> Waypoints { get; set; } = [];
}
