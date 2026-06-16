using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Services;
using RenovatorApp.Web.Models;
using RenovatorApp.Web.Services;

namespace RenovatorApp.Web.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/sync")]
public sealed class SyncApiController : ControllerBase
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string RenoCompanyIDHeaderName = "X-Reno-Company-ID";
    private readonly IConfiguration _configuration;
    private readonly MobileSyncDataService _mobileSyncDataService;
    private readonly CurrentUserSession _currentUserSession;
    private readonly RenovatorAppDbContext _dbContext;

    public SyncApiController(
        IConfiguration configuration,
        MobileSyncDataService mobileSyncDataService,
        CurrentUserSession currentUserSession,
        RenovatorAppDbContext dbContext)
    {
        _configuration = configuration;
        _mobileSyncDataService = mobileSyncDataService;
        _currentUserSession = currentUserSession;
        _dbContext = dbContext;
    }

    [HttpGet]
    public ActionResult<object> Health()
    {
        return Ok(new
        {
            status = "ok",
            endpoint = "api/sync",
            accepts = "POST"
        });
    }

    [HttpPost]
    public async Task<ActionResult<SyncResponse>> Sync(SyncRequest request, CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            ModelState.AddModelError(nameof(request.DeviceId), "DeviceId is required.");
            return ValidationProblem(ModelState);
        }

        var syncedAtUtc = DateTime.UtcNow;
        IReadOnlyList<MobileSyncResult> results;

        try
        {
            if (!TryGetRenoCompanyID(out var renoCompanyID))
            {
                ModelState.AddModelError(RenoCompanyIDHeaderName, $"{RenoCompanyIDHeaderName} is required until mobile authentication is implemented.");
                return ValidationProblem(ModelState);
            }

            results = await _mobileSyncDataService.SyncAsync(renoCompanyID, ToBatch(request), cancellationToken);
            var serverChanges = await BuildServerChangesAsync(renoCompanyID, request, cancellationToken);

            return new SyncResponse(
                syncedAtUtc,
                results.Select(result => new SyncEntityResult(
                        result.EntityName,
                        result.Id,
                        result.Status,
                        result.Message))
                    .ToList(),
                serverChanges);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgresException)
        {
            return Problem(
                title: "Sync database update failed.",
                detail: $"{postgresException.SqlState}: {postgresException.MessageText} Constraint: {postgresException.ConstraintName}",
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private bool IsAuthorized()
    {
        var configuredApiKey = _configuration["MobileSync:ApiKey"] ?? _configuration["MOBILE_SYNC_API_KEY"];

        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            return true;
        }

        return Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKey)
            && string.Equals(apiKey.ToString(), configuredApiKey, StringComparison.Ordinal);
    }

    private bool TryGetRenoCompanyID(out Guid renoCompanyID)
    {
        if (_currentUserSession.IsLoggedIn)
        {
            renoCompanyID = _currentUserSession.RenoCompanyID;
            return true;
        }

        if (Request.Headers.TryGetValue(RenoCompanyIDHeaderName, out var headerValue)
            && Guid.TryParse(headerValue.ToString(), out renoCompanyID)
            && renoCompanyID != Guid.Empty)
        {
            return true;
        }

        renoCompanyID = Guid.Empty;
        return false;
    }

    private async Task<SyncServerChangesDto> BuildServerChangesAsync(Guid renoCompanyID, SyncRequest request, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.AppSettings
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(setting => new SyncAppSettingDto(setting.AppSettingId, setting.Name, setting.Value))
            .ToListAsync(cancellationToken);

        var partSources = await _dbContext.PartSources
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(source => new SyncPartSourceDto(source.PartSourceId, source.Name))
            .ToListAsync(cancellationToken);

        var parts = await _dbContext.Parts
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(part => new SyncPartDto(
                part.PartId,
                part.PartSourceId,
                part.Name,
                part.Description,
                part.ModelNumber,
                part.Manufacturer,
                part.Sku,
                part.Url,
                part.ImageUrl,
                part.Cost,
                part.IsPackage,
                part.PackageUnits))
            .ToListAsync(cancellationToken);

        var inspectionAreaCategories = await _dbContext.InspectionAreaCategories
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(category => new SyncInspectionAreaCategoryDto(category.InspectionAreaCategoryId, category.Name, category.SortOrder))
            .ToListAsync(cancellationToken);

        var inspectionAreaTypes = await _dbContext.InspectionAreaTypes
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(areaType => new SyncInspectionAreaTypeDto(areaType.InspectionAreaTypeId, areaType.CategoryId, areaType.Name, areaType.SortOrder))
            .ToListAsync(cancellationToken);

        var buildingTypes = await _dbContext.BuildingTypes
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(buildingType => new SyncBuildingTypeDto(buildingType.BuildingTypeId, buildingType.Name))
            .ToListAsync(cancellationToken);

        var employees = await _dbContext.Employees
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(employee => new SyncEmployeeDto(
                employee.EmployeeId,
                employee.GivenName,
                employee.FamilyName,
                employee.PrimaryPhone,
                employee.PrimaryEmailAddress,
                employee.IsInspector,
                employee.IsDefaultInspector,
                employee.InspectorHourlyRate))
            .ToListAsync(cancellationToken);

        var customers = await _dbContext.Customers
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Include(customer => customer.BillAddress)
            .Select(customer => new SyncCustomerDto(
                customer.CustomerId,
                customer.GivenName,
                customer.FamilyName,
                customer.CompanyName,
                customer.PrimaryPhone,
                customer.PrimaryEmailAddress,
                customer.BillAddress == null ? string.Empty : customer.BillAddress.Street1,
                customer.BillAddress == null ? string.Empty : customer.BillAddress.Street2,
                customer.BillAddress == null ? string.Empty : customer.BillAddress.City,
                customer.BillAddress == null ? string.Empty : customer.BillAddress.State,
                customer.BillAddress == null ? string.Empty : customer.BillAddress.PostalCode,
                customer.Notes))
            .ToListAsync(cancellationToken);

        var properties = await _dbContext.Properties
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(property => new SyncPropertyDto(property.PropertyId, property.Name))
            .ToListAsync(cancellationToken);

        var customerProperties = await _dbContext.Customers
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .SelectMany(customer => customer.Properties.Select(property => new SyncCustomerPropertyDto(customer.CustomerId, property.PropertyId)))
            .ToListAsync(cancellationToken);

        var addresses = await _dbContext.Addresses
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(address => new SyncAddressDto(
                address.AddressId,
                address.PropertyId,
                address.Street1,
                address.Street2,
                address.City,
                address.State,
                address.PostalCode))
            .ToListAsync(cancellationToken);

        var buildings = await _dbContext.Buildings
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(building => new SyncBuildingDto(
                building.BuildingId,
                building.PropertyId,
                building.BuildingTypeId,
                building.Name,
                building.SortOrder))
            .ToListAsync(cancellationToken);

        var inspections = await _dbContext.Inspections
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(inspection => new SyncInspectionDto(
                inspection.InspectionId,
                inspection.CreatedAtUtc,
                inspection.UpdatedAtUtc,
                inspection.Title,
                inspection.InspectionDate,
                inspection.InspectorName,
                inspection.GeneralNotes,
                inspection.PropertyId,
                inspection.CustomerId))
            .ToListAsync(cancellationToken);

        var inspectionAreas = await _dbContext.InspectionAreas
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(area => new SyncInspectionAreaDto(
                area.InspectionAreaId,
                area.PropertyId,
                area.BuildingId,
                area.AreaTypeId,
                area.DisplayName,
                area.OverallRating,
                area.SortOrder))
            .ToListAsync(cancellationToken);

        var inspectionAreaNotes = await _dbContext.InspectionAreaNotes
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(note => new SyncInspectionAreaNoteDto(
                note.InspectionAreaNoteId,
                note.PropertyId,
                note.BuildingId,
                note.AreaId,
                note.CreatedAtUtc,
                note.UpdatedAtUtc,
                note.Text))
            .ToListAsync(cancellationToken);

        var inspectionAreaNoteEstimateItems = await _dbContext.InspectionAreaNoteEstimateItems
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(item => new SyncInspectionAreaNoteEstimateItemDto(
                item.InspectionAreaNoteEstimateItemId,
                item.PropertyId,
                item.BuildingId,
                item.AreaId,
                item.AreaNoteId,
                item.CreatedAtUtc,
                item.UpdatedAtUtc,
                item.Name,
                item.Cost,
                item.Hours))
            .ToListAsync(cancellationToken);

        var inspectionAreaNotePhotoRows = await _dbContext.InspectionAreaNotePhotos
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(photo => new
            {
                photo.InspectionAreaNotePhotoId,
                photo.PropertyId,
                photo.BuildingId,
                photo.AreaId,
                photo.AreaNoteId,
                photo.CreatedAtUtc,
                photo.FileName,
                photo.ContentType,
                photo.Data
            })
            .ToListAsync(cancellationToken);
        var inspectionAreaNotePhotos = inspectionAreaNotePhotoRows
            .Select(photo => new SyncInspectionAreaNotePhotoDto(
                photo.InspectionAreaNotePhotoId,
                photo.PropertyId,
                photo.BuildingId,
                photo.AreaId,
                photo.AreaNoteId,
                photo.CreatedAtUtc,
                photo.FileName,
                photo.ContentType,
                Convert.ToBase64String(photo.Data)))
            .ToList();

        var mileageTracking = await _dbContext.MileageTracking
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(session => new SyncMileageTrackingDto(
                session.MileageTrackingID,
                session.TrackingStartedAtUtc,
                session.TotalMileage,
                session.TotalTime,
                session.StartingLocation,
                session.StartingPosition,
                session.EndingLocation,
                session.EndingPosition,
                session.InspectionId))
            .ToListAsync(cancellationToken);

        var mileageTrackingWaypoints = await _dbContext.MileageTrackingWaypoints
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(waypoint => new SyncMileageTrackingWaypointDto(
                waypoint.MileageTrackingWaypointId,
                waypoint.MileageTrackingId,
                waypoint.WaypointTime,
                waypoint.CumulativeMiles,
                waypoint.GpsCoordinates,
                waypoint.Location))
            .ToListAsync(cancellationToken);

        var calendarEvents = await _dbContext.CalendarEvents
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Select(calendarEvent => new SyncCalendarEventDto(
                calendarEvent.CalendarEventId,
                calendarEvent.RenoUserID,
                calendarEvent.Title,
                calendarEvent.Date,
                calendarEvent.AllDay,
                calendarEvent.StartTime,
                calendarEvent.EndTime,
                calendarEvent.EventAlertTimes,
                calendarEvent.Notes,
                calendarEvent.IsPrivate,
                calendarEvent.InspectionId,
                calendarEvent.CreatedAtUtc,
                calendarEvent.UpdatedAtUtc,
                calendarEvent.IsDeleted))
            .ToListAsync(cancellationToken);

        return new SyncServerChangesDto(
            settings,
            partSources,
            parts,
            inspectionAreaCategories,
            inspectionAreaTypes,
            buildingTypes,
            employees,
            customers,
            customerProperties,
            properties,
            addresses,
            buildings,
            inspections,
            inspectionAreas,
            inspectionAreaNotes,
            inspectionAreaNoteEstimateItems,
            inspectionAreaNotePhotos,
            mileageTracking,
            mileageTrackingWaypoints,
            calendarEvents,
            FindMissingIds(request.Employees?.Select(item => item.Id), employees.Select(item => item.Id)),
            FindMissingIds(request.Customers?.Select(item => item.CustomerId), customers.Select(item => item.CustomerId)),
            FindMissingIds(request.Properties?.Select(item => item.Id), properties.Select(item => item.Id)),
            FindMissingIds(request.Addresses?.Select(item => item.Id), addresses.Select(item => item.Id)),
            FindMissingIds(request.Buildings?.Select(item => item.Id), buildings.Select(item => item.Id)),
            FindMissingIds(request.Inspections?.Select(item => item.Id), inspections.Select(item => item.Id)),
            FindMissingIds(request.InspectionAreas?.Select(item => item.Id), inspectionAreas.Select(item => item.Id)),
            FindMissingIds(request.InspectionAreaNotes?.Select(item => item.Id), inspectionAreaNotes.Select(item => item.Id)),
            FindMissingIds(request.InspectionAreaNoteEstimateItems?.Select(item => item.Id), inspectionAreaNoteEstimateItems.Select(item => item.Id)),
            FindMissingIds(request.InspectionAreaNotePhotos?.Select(item => item.Id), inspectionAreaNotePhotos.Select(item => item.Id)),
            FindMissingIds(request.MileageTracking?.Select(item => item.UniqueId), mileageTracking.Select(item => item.UniqueId)),
            FindMissingIds(request.MileageTrackingWaypoints?.Select(item => item.UniqueId), mileageTrackingWaypoints.Select(item => item.UniqueId)));
    }

    private static IReadOnlyList<Guid> FindMissingIds(IEnumerable<Guid>? clientIds, IEnumerable<Guid> serverIds)
    {
        if (clientIds is null)
        {
            return [];
        }

        var serverIdSet = serverIds.ToHashSet();
        return clientIds
            .Where(id => id != Guid.Empty && !serverIdSet.Contains(id))
            .Distinct()
            .ToList();
    }

    private static MobileSyncBatch ToBatch(SyncRequest request)
    {
        return new MobileSyncBatch(
            (request.Settings ?? []).Select(item => new MobileSyncAppSetting(
                item.Id,
                item.Name,
                item.Value)).ToList(),
            (request.PartSources ?? []).Select(item => new MobileSyncPartSource(
                item.PartSourceId,
                item.Name)).ToList(),
            (request.Parts ?? []).Select(item => new MobileSyncPart(
                item.PartId,
                item.PartSourceId,
                item.Name,
                item.Description,
                item.ModelNumber,
                item.Manufacturer,
                item.Sku,
                item.Url,
                item.ImageUrl,
                item.Cost,
                item.IsPackage,
                item.PackageUnits)).ToList(),
            (request.InspectionAreaCategories ?? []).Select(item => new MobileSyncInspectionAreaCategory(
                item.Id,
                item.Name,
                item.SortOrder)).ToList(),
            (request.InspectionAreaTypes ?? []).Select(item => new MobileSyncInspectionAreaType(
                item.AreaTypeId,
                item.CategoryId,
                item.Name,
                item.SortOrder)).ToList(),
            (request.BuildingTypes ?? []).Select(item => new MobileSyncBuildingType(
                item.Id,
                item.Name)).ToList(),
            (request.Employees ?? []).Select(item => new MobileSyncEmployee(
                item.Id,
                item.FirstName,
                item.LastName,
                item.Phone,
                item.Email,
                item.IsInspector,
                item.IsDefaultInspector,
                item.InspectorHourlyRate)).ToList(),
            (request.Customers ?? []).Select(item => new MobileSyncCustomer(
                item.CustomerId,
                item.FirstName,
                item.LastName,
                item.CompanyName,
                item.Phone,
                item.Email,
                item.Street1,
                item.Street2,
                item.City,
                item.State,
                item.PostalCode,
                item.Notes)).ToList(),
            (request.CustomerProperties ?? []).Select(item => new MobileSyncCustomerProperty(
                item.CustomerId,
                item.PropertyId)).ToList(),
            (request.Properties ?? []).Select(item => new MobileSyncProperty(
                item.Id,
                item.Name)).ToList(),
            (request.Addresses ?? []).Select(item => new MobileSyncAddress(
                item.Id,
                item.PropertyId,
                item.Street1,
                item.Street2,
                item.City,
                item.State,
                item.PostalCode)).ToList(),
            (request.Buildings ?? []).Select(item => new MobileSyncBuilding(
                item.Id,
                item.PropertyId,
                item.BuildingTypeId,
                item.Name,
                item.SortOrder)).ToList(),
            (request.Inspections ?? []).Select(item => new MobileSyncInspection(
                item.Id,
                item.CreatedAtUtc,
                item.UpdatedAtUtc,
                item.Title,
                item.InspectionDate,
                item.InspectorName,
                item.GeneralNotes,
                item.PropertyId,
                item.CustomerId)).ToList(),
            (request.InspectionAreas ?? []).Select(item => new MobileSyncInspectionArea(
                item.Id,
                item.PropertyId,
                item.BuildingId,
                item.AreaTypeId,
                item.DisplayName,
                item.OverallRating,
                item.SortOrder)).ToList(),
            (request.InspectionAreaNotes ?? []).Select(item => new MobileSyncInspectionAreaNote(
                item.Id,
                item.PropertyId,
                item.BuildingId,
                item.AreaId,
                item.CreatedAtUtc,
                item.UpdatedAtUtc,
                item.Text)).ToList(),
            (request.InspectionAreaNoteEstimateItems ?? []).Select(item => new MobileSyncInspectionAreaNoteEstimateItem(
                item.Id,
                item.PropertyId,
                item.BuildingId,
                item.AreaId,
                item.AreaNoteId,
                item.CreatedAtUtc,
                item.UpdatedAtUtc,
                item.Name,
                item.Cost,
                item.Hours)).ToList(),
            (request.InspectionAreaNotePhotos ?? []).Select(item => new MobileSyncInspectionAreaNotePhoto(
                item.Id,
                item.PropertyId,
                item.BuildingId,
                item.AreaId,
                item.AreaNoteId,
                item.CreatedAtUtc,
                item.FileName,
                item.ContentType,
                item.DataBase64)).ToList(),
            (request.MileageTracking ?? []).Select(item => new MobileSyncMileageTracking(
                item.UniqueId,
                item.TrackingStartedAtUtc,
                item.TotalMileage,
                item.TotalTime,
                item.StartingLocation,
                item.StartingPosition,
                item.EndingLocation,
                item.EndingPosition,
                item.InspectionId)).ToList(),
            (request.MileageTrackingWaypoints ?? []).Select(item => new MobileSyncMileageTrackingWaypoint(
                item.UniqueId,
                item.MileageTrackingId,
                item.WaypointTime,
                item.CumulativeMiles,
                item.GpsCoordinates,
                item.Location)).ToList(),
            (request.CalendarEvents ?? []).Select(item => new MobileSyncCalendarEvent(
                item.UniqueEventId,
                item.RenoUserID,
                item.Title,
                item.Date,
                item.AllDay,
                item.StartTime,
                item.EndTime,
                item.EventAlertTimes,
                item.Notes,
                item.IsPrivate,
                item.InspectionId,
                item.CreatedAtUtc,
                item.UpdatedAtUtc,
                item.IsDeleted)).ToList(),
            request.DeletedInspectionAreaIds ?? [],
            request.DeletedBuildingIds ?? []);
    }
}



