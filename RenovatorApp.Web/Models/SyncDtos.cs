namespace RenovatorApp.Web.Models;

public sealed record SyncRequest(
    string DeviceId,
    DateTime? LastSyncedAtUtc,
    IReadOnlyList<SyncAppSettingDto>? Settings,
    IReadOnlyList<SyncPartSourceDto>? PartSources,
    IReadOnlyList<SyncPartDto>? Parts,
    IReadOnlyList<SyncInspectionAreaCategoryDto>? InspectionAreaCategories,
    IReadOnlyList<SyncInspectionAreaTypeDto>? InspectionAreaTypes,
    IReadOnlyList<SyncBuildingTypeDto>? BuildingTypes,
    IReadOnlyList<SyncEmployeeDto>? Employees,
    IReadOnlyList<SyncCustomerDto>? Customers,
    IReadOnlyList<SyncCustomerPropertyDto>? CustomerProperties,
    IReadOnlyList<SyncPropertyDto>? Properties,
    IReadOnlyList<SyncAddressDto>? Addresses,
    IReadOnlyList<SyncBuildingDto>? Buildings,
    IReadOnlyList<SyncInspectionDto>? Inspections,
    IReadOnlyList<SyncInspectionAreaDto>? InspectionAreas,
    IReadOnlyList<SyncInspectionAreaNoteDto>? InspectionAreaNotes,
    IReadOnlyList<SyncInspectionAreaNoteEstimateItemDto>? InspectionAreaNoteEstimateItems,
    IReadOnlyList<SyncInspectionAreaNotePhotoDto>? InspectionAreaNotePhotos,
    IReadOnlyList<SyncMileageTrackingDto>? MileageTracking,
    IReadOnlyList<SyncMileageTrackingWaypointDto>? MileageTrackingWaypoints,
    IReadOnlyList<SyncCalendarEventDto>? CalendarEvents,
    IReadOnlyList<Guid>? DeletedInspectionAreaIds,
    IReadOnlyList<Guid>? DeletedBuildingIds);

public sealed record SyncResponse(
    DateTime SyncedAtUtc,
    IReadOnlyList<SyncEntityResult> Results,
    SyncServerChangesDto ServerChanges);

public sealed record SyncEntityResult(
    string EntityName,
    Guid Id,
    string Status,
    string? Message);

public sealed record SyncServerChangesDto(
    IReadOnlyList<SyncAppSettingDto> Settings,
    IReadOnlyList<SyncPartSourceDto> PartSources,
    IReadOnlyList<SyncPartDto> Parts,
    IReadOnlyList<SyncInspectionAreaCategoryDto> InspectionAreaCategories,
    IReadOnlyList<SyncInspectionAreaTypeDto> InspectionAreaTypes,
    IReadOnlyList<SyncBuildingTypeDto> BuildingTypes,
    IReadOnlyList<SyncEmployeeDto> Employees,
    IReadOnlyList<SyncCustomerDto> Customers,
    IReadOnlyList<SyncCustomerPropertyDto> CustomerProperties,
    IReadOnlyList<SyncPropertyDto> Properties,
    IReadOnlyList<SyncAddressDto> Addresses,
    IReadOnlyList<SyncBuildingDto> Buildings,
    IReadOnlyList<SyncInspectionDto> Inspections,
    IReadOnlyList<SyncInspectionAreaDto> InspectionAreas,
    IReadOnlyList<SyncInspectionAreaNoteDto> InspectionAreaNotes,
    IReadOnlyList<SyncInspectionAreaNoteEstimateItemDto> InspectionAreaNoteEstimateItems,
    IReadOnlyList<SyncInspectionAreaNotePhotoDto> InspectionAreaNotePhotos,
    IReadOnlyList<SyncMileageTrackingDto> MileageTracking,
    IReadOnlyList<SyncMileageTrackingWaypointDto> MileageTrackingWaypoints,
    IReadOnlyList<SyncCalendarEventDto> CalendarEvents,
    IReadOnlyList<Guid> DeletedEmployeeIds,
    IReadOnlyList<Guid> DeletedCustomerIds,
    IReadOnlyList<Guid> DeletedPropertyIds,
    IReadOnlyList<Guid> DeletedAddressIds,
    IReadOnlyList<Guid> DeletedBuildingIds,
    IReadOnlyList<Guid> DeletedInspectionIds,
    IReadOnlyList<Guid> DeletedInspectionAreaIds,
    IReadOnlyList<Guid> DeletedInspectionAreaNoteIds,
    IReadOnlyList<Guid> DeletedInspectionAreaNoteEstimateItemIds,
    IReadOnlyList<Guid> DeletedInspectionAreaNotePhotoIds,
    IReadOnlyList<Guid> DeletedMileageTrackingIds,
    IReadOnlyList<Guid> DeletedMileageTrackingWaypointIds);

public sealed record SyncAppSettingDto(
    Guid Id,
    string Name,
    string Value);

public sealed record SyncPartSourceDto(
    Guid PartSourceId,
    string Name);

public sealed record SyncPartDto(
    Guid PartId,
    Guid PartSourceId,
    string Name,
    string Description,
    string ModelNumber,
    string Manufacturer,
    string Sku,
    string? Url,
    string? ImageUrl,
    decimal Cost,
    bool IsPackage,
    int PackageUnits);

public sealed record SyncInspectionAreaCategoryDto(
    Guid Id,
    string Name,
    int SortOrder);

public sealed record SyncInspectionAreaTypeDto(
    Guid AreaTypeId,
    Guid CategoryId,
    string Name,
    int SortOrder);

public sealed record SyncBuildingTypeDto(
    Guid Id,
    string Name);

public sealed record SyncEmployeeDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Phone,
    string Email,
    bool IsInspector,
    bool IsDefaultInspector,
    decimal InspectorHourlyRate);

public sealed record SyncCustomerDto(
    Guid CustomerId,
    string FirstName,
    string LastName,
    string CompanyName,
    string Phone,
    string Email,
    string Street1,
    string Street2,
    string City,
    string State,
    string PostalCode,
    string Notes);

public sealed record SyncCustomerPropertyDto(
    Guid CustomerId,
    Guid PropertyId);

public sealed record SyncPropertyDto(
    Guid Id,
    string? Name);

public sealed record SyncAddressDto(
    Guid Id,
    Guid? PropertyId,
    string Street1,
    string Street2,
    string City,
    string State,
    string PostalCode);

public sealed record SyncBuildingDto(
    Guid Id,
    Guid PropertyId,
    Guid BuildingTypeId,
    string Name,
    int SortOrder);

public sealed record SyncInspectionDto(
    Guid Id,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string Title,
    DateTime InspectionDate,
    string InspectorName,
    string GeneralNotes,
    Guid PropertyId,
    Guid? CustomerId);

public sealed record SyncInspectionAreaDto(
    Guid Id,
    Guid PropertyId,
    Guid? BuildingId,
    Guid AreaTypeId,
    string DisplayName,
    int OverallRating,
    int SortOrder);

public sealed record SyncInspectionAreaNoteDto(
    Guid Id,
    Guid PropertyId,
    Guid? BuildingId,
    Guid AreaId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string Text);

public sealed record SyncInspectionAreaNoteEstimateItemDto(
    Guid Id,
    Guid PropertyId,
    Guid? BuildingId,
    Guid AreaId,
    Guid AreaNoteId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string Name,
    decimal Cost,
    decimal Hours);

public sealed record SyncInspectionAreaNotePhotoDto(
    Guid Id,
    Guid PropertyId,
    Guid? BuildingId,
    Guid AreaId,
    Guid AreaNoteId,
    DateTime CreatedAtUtc,
    string FileName,
    string ContentType,
    string DataBase64);

public sealed record SyncMileageTrackingDto(
    Guid UniqueId,
    DateTime TrackingStartedAtUtc,
    double TotalMileage,
    TimeSpan TotalTime,
    string StartingLocation,
    string StartingPosition,
    string EndingLocation,
    string EndingPosition,
    Guid? InspectionId);

public sealed record SyncMileageTrackingWaypointDto(
    Guid UniqueId,
    Guid MileageTrackingId,
    DateTime WaypointTime,
    double CumulativeMiles,
    string GpsCoordinates,
    string? Location);

public sealed record SyncCalendarEventDto(
    Guid UniqueEventId,
    Guid RenoUserID,
    string Title,
    DateTime Date,
    bool AllDay,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string EventAlertTimes,
    string Notes,
    bool IsPrivate,
    Guid? InspectionId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    bool IsDeleted);


