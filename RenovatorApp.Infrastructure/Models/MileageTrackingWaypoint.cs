namespace RenovatorApp.Infrastructure.Models;

public sealed class MileageTrackingWaypoint : IRenoCompanyEntity
{
    public Guid UniqueId { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public Guid MileageTrackingId { get; set; }
    public DateTime WaypointTime { get; set; }
    public double CumulativeMiles { get; set; }
    public string GpsCoordinates { get; set; } = string.Empty;
    public string? Location { get; set; }
    public MileageTracking? MileageTracking { get; set; }
}
