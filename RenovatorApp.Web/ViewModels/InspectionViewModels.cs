using RenovatorApp.Infrastructure.Models;

namespace RenovatorApp.Web.ViewModels;

public sealed record InspectionListItemViewModel(
    Guid Id,
    string Title,
    DateTime InspectionDate,
    string InspectorName,
    string PropertyAddress,
    string CustomerName);

public sealed class InspectionDetailViewModel
{
    public required Inspection Inspection { get; init; }
    public string PropertyAddress { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string DefaultReportName { get; init; } = string.Empty;
    public IReadOnlyList<InspectionDocumentViewModel> Documents { get; init; } = [];
    public IReadOnlyList<InspectionAttachDocumentViewModel> AttachDocuments { get; init; } = [];
    public IReadOnlyList<InspectionMileageTrackingViewModel> MileageTrackingRecords { get; init; } = [];
    public IReadOnlyList<InspectionMileageTrackingAttachViewModel> MileageTrackingAttachRecords { get; init; } = [];
}

public sealed class InspectionDocumentViewModel
{
    public Guid DocumentId { get; init; }
    public string DocumentName { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public string Filename { get; init; } = string.Empty;
    public DateTime CreateDate { get; init; }
}

public sealed class InspectionAttachDocumentViewModel
{
    public Guid DocumentId { get; init; }
    public string DocumentName { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string InspectionTitle { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public DateTime CreateDate { get; init; }
    public bool IsAttached { get; init; }
}

public sealed class InspectionMileageTrackingViewModel
{
    public Guid UniqueId { get; init; }
    public DateTime TrackingStartedAtUtc { get; init; }
    public TimeSpan TotalTime { get; init; }
    public double TotalMileage { get; init; }
    public DateTime EndTimeUtc => TrackingStartedAtUtc.Add(TotalTime);
    public string MapImageUrl { get; init; } = string.Empty;
    public IReadOnlyList<InspectionMileageTrackingWaypointViewModel> Waypoints { get; init; } = [];
}

public sealed class InspectionMileageTrackingWaypointViewModel
{
    public DateTime WaypointTimeUtc { get; init; }
    public double CumulativeMiles { get; init; }
    public string GpsCoordinates { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
}

public sealed class InspectionMileageTrackingAttachViewModel
{
    public Guid UniqueId { get; init; }
    public DateTime TrackingStartedAtUtc { get; init; }
    public TimeSpan TotalTime { get; init; }
    public double TotalMileage { get; init; }
    public DateTime EndTimeUtc => TrackingStartedAtUtc.Add(TotalTime);
    public Guid? InspectionId { get; init; }
    public string InspectionTitle { get; init; } = string.Empty;
}

public sealed class InspectionEditViewModel
{
    public Guid? Id { get; init; }
    public string ActiveTab { get; set; } = "general";
    public string Title { get; init; } = string.Empty;
    public DateTime InspectionDate { get; init; } = DateTime.Today;
    public string InspectorName { get; init; } = string.Empty;
    public string GeneralNotes { get; init; } = string.Empty;
    public Guid? PropertyId { get; init; }
    public InspectionPropertyAddressEditViewModel PropertyAddress { get; init; } = new();
    public InspectionCustomerEditViewModel Customer { get; init; } = new();
    public IReadOnlyList<InspectionBuildingEditViewModel> Buildings { get; init; } = [];
    public IReadOnlyList<InspectorPickerItemViewModel> Inspectors { get; init; } = [];
    public IReadOnlyList<PartPickerItemViewModel> Parts { get; init; } = [];
    public InspectionCustomerPickerViewModel CustomerPicker { get; init; } = new();
    public bool ForceNewCustomer { get; set; }
    public bool ShowCustomerMatchDialog { get; init; }
    public IReadOnlyList<InspectionCustomerMatchViewModel> CustomerMatches { get; init; } = [];
    public string PageTitle => Id.HasValue ? "Edit Inspection" : "New Inspection";
    public string CancelAction => Id.HasValue ? "Details" : "Index";
}

public sealed class InspectionCustomerMatchViewModel
{
    public Guid CustomerId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Street1 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
}

public sealed class InspectionCustomerPickerViewModel
{
    public IReadOnlyList<InspectionCustomerPickerItemViewModel> Customers { get; init; } = [];
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 15;
    public int TotalCustomers { get; init; }
    public int TotalPages { get; init; } = 1;
    public string Search { get; init; } = string.Empty;
    public bool OpenOnLoad { get; init; }
}

public sealed class InspectionCustomerPickerItemViewModel
{
    public Guid CustomerId { get; init; }
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

public sealed class InspectionNewCustomerViewModel
{
    public string GivenName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string PrimaryEmailAddress { get; set; } = string.Empty;
    public string PrimaryPhone { get; set; } = string.Empty;
    public string MobilePhone { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public InspectionNewCustomerAddressViewModel BillAddress { get; set; } = new();
}

public sealed class InspectionNewCustomerAddressViewModel
{
    public string Street1 { get; set; } = string.Empty;
    public string Street2 { get; set; } = string.Empty;
    public string Street3 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public sealed class InspectionPropertyPickerViewModel
{
    public Guid InspectionId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid? SelectedPropertyId { get; init; }
    public IReadOnlyList<InspectionPropertyPickerItemViewModel> Properties { get; init; } = [];
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 15;
    public int TotalProperties { get; init; }
    public int TotalPages { get; init; } = 1;
    public string Search { get; init; } = string.Empty;
    public InspectionPropertyAddressEditViewModel NewProperty { get; init; } = new();
}

public sealed class InspectionPropertyPickerItemViewModel
{
    public Guid PropertyId { get; init; }
    public string PropertyName { get; init; } = string.Empty;
    public string Street1 { get; init; } = string.Empty;
    public string Street2 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string AddressLine => string.Join(" - ", new[]
    {
        string.Join(" ", new[] { Street1, Street2 }.Where(value => !string.IsNullOrWhiteSpace(value))),
        string.Join(" ", new[]
        {
            string.Join(", ", new[] { City, State }.Where(value => !string.IsNullOrWhiteSpace(value))),
            PostalCode
        }.Where(value => !string.IsNullOrWhiteSpace(value)))
    }.Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed class InspectorPickerItemViewModel
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public decimal HourlyRate { get; init; }
    public bool IsDefault { get; init; }
}

public sealed class PartPickerItemViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public decimal Cost { get; init; }
}

public sealed class InspectionPropertyAddressEditViewModel
{
    public string Street1 { get; init; } = string.Empty;
    public string Street2 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
}

public sealed class InspectionCustomerEditViewModel
{
    public Guid? CustomerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Street1 { get; set; } = string.Empty;
    public string Street2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
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
