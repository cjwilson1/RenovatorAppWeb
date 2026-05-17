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

public sealed class InspectionEditViewModel
{
    public Guid? Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime InspectionDate { get; init; } = DateTime.Today;
    public string InspectorName { get; init; } = string.Empty;
    public string GeneralNotes { get; init; } = string.Empty;
    public InspectionPropertyAddressEditViewModel PropertyAddress { get; init; } = new();
    public InspectionClientEditViewModel Client { get; init; } = new();
    public IReadOnlyList<InspectionBuildingEditViewModel> Buildings { get; init; } = [];
    public IReadOnlyList<InspectorPickerItemViewModel> Inspectors { get; init; } = [];
    public string PageTitle => Id.HasValue ? "Edit Inspection" : "New Inspection";
    public string CancelAction => Id.HasValue ? "Details" : "Index";
}

public sealed class InspectorPickerItemViewModel
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public decimal HourlyRate { get; init; }
}

public sealed class InspectionPropertyAddressEditViewModel
{
    public string Street1 { get; init; } = string.Empty;
    public string Street2 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
}

public sealed class InspectionClientEditViewModel
{
    public Guid? ClientId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Street1 { get; init; } = string.Empty;
    public string Street2 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public sealed class InspectionBuildingEditViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string BuildingTypeName { get; init; } = string.Empty;
    public IReadOnlyList<InspectionAreaEditViewModel> Areas { get; init; } = [];
}

public sealed class InspectionAreaEditViewModel
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string AreaTypeName { get; init; } = string.Empty;
    public int OverallRating { get; init; }
    public IReadOnlyList<InspectionAreaNoteEditViewModel> Notes { get; init; } = [];
}

public sealed class InspectionAreaNoteEditViewModel
{
    public Guid Id { get; init; }
    public string Text { get; init; } = string.Empty;
    public decimal EstimateCost { get; init; }
    public decimal EstimateHours { get; init; }
    public IReadOnlyList<InspectionAreaNoteEstimateItemEditViewModel> EstimateItems { get; init; } = [];
    public IReadOnlyList<InspectionAreaNotePhotoEditViewModel> Photos { get; init; } = [];
}

public sealed class InspectionAreaNoteEstimateItemEditViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Cost { get; init; }
    public decimal Hours { get; init; }
    public bool IsNew { get; init; }
}

public sealed class InspectionAreaNotePhotoEditViewModel
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string ImageType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public int? WidthPixels { get; init; }
    public int? HeightPixels { get; init; }
    public string CroppedDataUrl { get; init; } = string.Empty;
    public byte[] Data { get; init; } = [];
}
