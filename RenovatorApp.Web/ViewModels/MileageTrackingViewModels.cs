namespace RenovatorApp.Web.ViewModels;

public sealed class MileageTrackingIndexViewModel
{
    public IReadOnlyList<MileageTrackingRowViewModel> Trips { get; init; } = [];
}

public sealed class MileageTrackingTripViewModel
{
    public MileageTrackingRowViewModel Trip { get; init; } = new();
    public string MapImageUrl { get; init; } = string.Empty;
    public IReadOnlyList<MileageTrackingWaypointViewModel> Waypoints { get; init; } = [];
}

public sealed class MileageTrackingRowViewModel
{
    public Guid UniqueId { get; init; }
    public DateTime StartTimeUtc { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public double TotalMileage { get; init; }
    public DateTime EndTimeUtc { get; init; }
    public string InspectionTitle { get; init; } = string.Empty;
}

public sealed class MileageTrackingAttachInspectionViewModel
{
    public MileageTrackingRowViewModel Trip { get; init; } = new();
    public IReadOnlyList<MileageTrackingInspectionPickerRowViewModel> Inspections { get; init; } = [];
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalInspections { get; init; }
}

public sealed class MileageTrackingInspectionPickerRowViewModel
{
    public Guid InspectionId { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime InspectionDate { get; init; }
    public string CustomerName { get; init; } = string.Empty;
}

public sealed class MileageTrackingWaypointViewModel
{
    public DateTime WaypointTimeUtc { get; init; }
    public double CumulativeMiles { get; init; }
    public string GpsCoordinates { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
}
