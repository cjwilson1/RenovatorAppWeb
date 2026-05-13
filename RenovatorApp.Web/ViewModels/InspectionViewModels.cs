using RenovatorApp.Infrastructure.Models;

namespace RenovatorApp.Web.ViewModels;

public sealed record InspectionListItemViewModel(
    Guid Id,
    string Title,
    DateTime InspectionDate,
    string InspectorName,
    string PropertyAddress,
    string ClientName);

public sealed class InspectionDetailViewModel
{
    public required Inspection Inspection { get; init; }
    public string PropertyAddress { get; init; } = string.Empty;
    public string ClientName { get; init; } = string.Empty;
}
