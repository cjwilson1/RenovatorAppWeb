namespace RenovatorApp.Web.ViewModels;

public sealed class MileageTrackingIndexViewModel
{
    public IReadOnlyList<MileageTrackingRowViewModel> Trips { get; init; } = [];
}

public sealed class MileageTrackingTripViewModel
{
    public MileageTrackingRowViewModel Trip { get; init; } = new();
    public IReadOnlyList<MileageTrackingWaypointViewModel> Waypoints { get; init; } = [];
}

public sealed class MileageTrackingRowViewModel
{
    public Guid UniqueId { get; init; }
    public DateTime StartTimeUtc { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public double TotalMileage { get; init; }
    public DateTime EndTimeUtc { get; init; }
}

public sealed class MileageTrackingWaypointViewModel
{
    public DateTime WaypointTimeUtc { get; init; }
    public string GpsCoordinates { get; init; } = string.Empty;
}
