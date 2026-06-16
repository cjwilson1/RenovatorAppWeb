using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;

namespace RenovatorApp.Infrastructure.Services;

public sealed class MobileSyncDataService
{
    private readonly RenovatorAppDbContext _dbContext;

    public MobileSyncDataService(RenovatorAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MobileSyncResult>> SyncAsync(Guid renoCompanyID, MobileSyncBatch batch, CancellationToken cancellationToken = default)
    {
        var results = new List<MobileSyncResult>();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var item in batch.Settings)
        {
            if (!string.Equals(item.Name, "defaultstate", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(MobileSyncResult.Skipped(nameof(AppSetting), item.Id, "Only the defaultstate setting can be synced."));
                continue;
            }

            var entity = await _dbContext.AppSettings
                .ForCompany(renoCompanyID)
                .FirstOrDefaultAsync(setting => setting.Name.ToLower() == "defaultstate", cancellationToken);

            if (entity is null)
            {
                _dbContext.AppSettings.Add(new AppSetting
                {
                    AppSettingId = item.Id,
                    RenoCompanyID = renoCompanyID,
                    Name = item.Name,
                    Value = item.Value
                });
                results.Add(MobileSyncResult.Created(nameof(AppSetting), item.Id));
                continue;
            }

            entity.Name = item.Name;
            entity.Value = item.Value;
            results.Add(MobileSyncResult.Updated(nameof(AppSetting), entity.AppSettingId));
        }

        foreach (var item in batch.PartSources)
        {
            var entity = await _dbContext.PartSources.ForCompany(renoCompanyID).FirstOrDefaultAsync(source => source.PartSourceId == item.PartSourceId, cancellationToken);

            if (entity is null)
            {
                _dbContext.PartSources.Add(new PartSource
                {
                    PartSourceId = item.PartSourceId,
                    RenoCompanyID = renoCompanyID,
                    Name = item.Name
                });
                results.Add(MobileSyncResult.Created(nameof(PartSource), item.PartSourceId));
                continue;
            }

            entity.Name = item.Name;
            results.Add(MobileSyncResult.Updated(nameof(PartSource), item.PartSourceId));
        }

        foreach (var item in batch.Parts)
        {
            var entity = await _dbContext.Parts.ForCompany(renoCompanyID).FirstOrDefaultAsync(part => part.PartId == item.PartId, cancellationToken);

            if (entity is null)
            {
                _dbContext.Parts.Add(new Part
                {
                    PartId = item.PartId,
                    RenoCompanyID = renoCompanyID,
                    PartSourceId = item.PartSourceId,
                    Name = item.Name,
                    Description = item.Description,
                    ModelNumber = item.ModelNumber,
                    Manufacturer = item.Manufacturer,
                    Sku = item.Sku,
                    Url = item.Url ?? string.Empty,
                    ImageUrl = item.ImageUrl ?? string.Empty,
                    Cost = item.Cost,
                    IsPackage = item.IsPackage,
                    PackageUnits = item.PackageUnits
                });
                results.Add(MobileSyncResult.Created(nameof(Part), item.PartId));
                continue;
            }

            entity.PartSourceId = item.PartSourceId;
            entity.Name = item.Name;
            entity.Description = item.Description;
            entity.ModelNumber = item.ModelNumber;
            entity.Manufacturer = item.Manufacturer;
            entity.Sku = item.Sku;
            entity.Url = item.Url ?? string.Empty;
            entity.ImageUrl = item.ImageUrl ?? string.Empty;
            entity.Cost = item.Cost;
            entity.IsPackage = item.IsPackage;
            entity.PackageUnits = item.PackageUnits;
            results.Add(MobileSyncResult.Updated(nameof(Part), item.PartId));
        }

        foreach (var item in batch.InspectionAreaCategories)
        {
            var entity = await _dbContext.InspectionAreaCategories.ForCompany(renoCompanyID).FirstOrDefaultAsync(category => category.InspectionAreaCategoryId == item.Id, cancellationToken);

            if (entity is null)
            {
                _dbContext.InspectionAreaCategories.Add(new InspectionAreaCategory
                {
                    InspectionAreaCategoryId = item.Id,
                    RenoCompanyID = renoCompanyID,
                    Name = item.Name,
                    SortOrder = item.SortOrder
                });
                results.Add(MobileSyncResult.Created(nameof(InspectionAreaCategory), item.Id));
                continue;
            }

            entity.Name = item.Name;
            entity.SortOrder = item.SortOrder;
            results.Add(MobileSyncResult.Updated(nameof(InspectionAreaCategory), item.Id));
        }

        foreach (var item in batch.InspectionAreaTypes)
        {
            var entity = await _dbContext.InspectionAreaTypes.ForCompany(renoCompanyID).FirstOrDefaultAsync(areaType => areaType.InspectionAreaTypeId == item.AreaTypeId, cancellationToken);

            if (entity is null)
            {
                _dbContext.InspectionAreaTypes.Add(new InspectionAreaType
                {
                    InspectionAreaTypeId = item.AreaTypeId,
                    RenoCompanyID = renoCompanyID,
                    CategoryId = item.CategoryId,
                    Name = item.Name,
                    SortOrder = item.SortOrder
                });
                results.Add(MobileSyncResult.Created(nameof(InspectionAreaType), item.AreaTypeId));
                continue;
            }

            entity.CategoryId = item.CategoryId;
            entity.Name = item.Name;
            entity.SortOrder = item.SortOrder;
            results.Add(MobileSyncResult.Updated(nameof(InspectionAreaType), item.AreaTypeId));
        }

        foreach (var item in batch.BuildingTypes)
        {
            var entity = await _dbContext.BuildingTypes.ForCompany(renoCompanyID).FirstOrDefaultAsync(buildingType => buildingType.BuildingTypeId == item.Id, cancellationToken);

            if (entity is null)
            {
                _dbContext.BuildingTypes.Add(new BuildingType
                {
                    BuildingTypeId = item.Id,
                    RenoCompanyID = renoCompanyID,
                    Name = item.Name
                });
                results.Add(MobileSyncResult.Created(nameof(BuildingType), item.Id));
                continue;
            }

            entity.Name = item.Name;
            results.Add(MobileSyncResult.Updated(nameof(BuildingType), item.Id));
        }
        foreach (var item in batch.Employees)
        {
            var entity = await _dbContext.Employees.ForCompany(renoCompanyID).FirstOrDefaultAsync(employee => employee.EmployeeId == item.Id, cancellationToken);
            var isDefaultInspector = item.IsInspector && item.IsDefaultInspector;

            if (isDefaultInspector)
            {
                var previousDefaults = await _dbContext.Employees
                    .ForCompany(renoCompanyID)
                    .Where(employee => employee.EmployeeId != item.Id && employee.IsDefaultInspector)
                    .ToListAsync(cancellationToken);

                foreach (var previousDefault in previousDefaults)
                {
                    previousDefault.IsDefaultInspector = false;
                }
            }

            if (entity is null)
            {
                entity = new Employee
                {
                    EmployeeId = item.Id,
                    RenoCompanyID = renoCompanyID,
                    Active = true,
                    CreatedDate = DateTime.UtcNow
                };

                ApplyEmployee(item, entity, isDefaultInspector);
                _dbContext.Employees.Add(entity);
                results.Add(MobileSyncResult.Created(nameof(Employee), item.Id));
                continue;
            }

            ApplyEmployee(item, entity, isDefaultInspector);
            results.Add(MobileSyncResult.Updated(nameof(Employee), item.Id));
        }


        foreach (var item in batch.Customers)
        {
            var entity = await _dbContext.Customers
                .ForCompany(renoCompanyID)
                .Include(customer => customer.BillAddress)
                .FirstOrDefaultAsync(customer => customer.CustomerId == item.CustomerId, cancellationToken);

            if (entity is null)
            {
                entity = new Customer
                {
                    CustomerId = item.CustomerId,
                    RenoCompanyID = renoCompanyID,
                    Active = true
                };
                ApplyCustomer(item, entity);
                ApplyCustomerAddress(item, entity);
                _dbContext.Customers.Add(entity);
                results.Add(MobileSyncResult.Created(nameof(Customer), item.CustomerId));
                continue;
            }

            ApplyCustomer(item, entity);
            ApplyCustomerAddress(item, entity);
            results.Add(MobileSyncResult.Updated(nameof(Customer), item.CustomerId));
        }

        foreach (var item in batch.Properties)
        {
            var entity = await _dbContext.Properties.ForCompany(renoCompanyID).FirstOrDefaultAsync(property => property.PropertyId == item.Id, cancellationToken);

            if (entity is null)
            {
                _dbContext.Properties.Add(new Property { PropertyId = item.Id, RenoCompanyID = renoCompanyID, Name = item.Name ?? string.Empty });
                results.Add(MobileSyncResult.Created(nameof(Property), item.Id));
                continue;
            }

            entity.Name = item.Name ?? entity.Name;
            results.Add(MobileSyncResult.Updated(nameof(Property), item.Id));
        }

        foreach (var item in batch.Addresses)
        {
            var entity = await _dbContext.Addresses.ForCompany(renoCompanyID).FirstOrDefaultAsync(address => address.AddressId == item.Id, cancellationToken);

            if (entity is null)
            {
                _dbContext.Addresses.Add(new Address
                {
                    AddressId = item.Id,
                    RenoCompanyID = renoCompanyID,
                    PropertyId = item.PropertyId,
                    Street1 = item.Street1,
                    Street2 = item.Street2,
                    City = item.City,
                    State = item.State,
                    PostalCode = item.PostalCode
                });
                results.Add(MobileSyncResult.Created(nameof(Address), item.Id));
                continue;
            }

            entity.PropertyId = item.PropertyId;
            entity.Street1 = item.Street1;
            entity.Street2 = item.Street2;
            entity.City = item.City;
            entity.State = item.State;
            entity.PostalCode = item.PostalCode;
            results.Add(MobileSyncResult.Updated(nameof(Address), item.Id));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var item in batch.CustomerProperties)
        {
            var customerExists = await _dbContext.Customers
                .ForCompany(renoCompanyID)
                .AnyAsync(customer => customer.CustomerId == item.CustomerId, cancellationToken);
            var propertyExists = await _dbContext.Properties
                .ForCompany(renoCompanyID)
                .AnyAsync(property => property.PropertyId == item.PropertyId, cancellationToken);

            if (!customerExists || !propertyExists)
            {
                results.Add(MobileSyncResult.Skipped(
                    "CustomerProperty",
                    item.PropertyId,
                    "Customer or property was not found for this company."));
                continue;
            }

            var rowsAffected = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO "CustomerProperty" ("CustomerId", "PropertyId")
                 VALUES ({item.CustomerId}, {item.PropertyId})
                 ON CONFLICT DO NOTHING
                 """,
                cancellationToken);

            results.Add(rowsAffected > 0
                ? MobileSyncResult.Created("CustomerProperty", item.PropertyId)
                : MobileSyncResult.Unchanged("CustomerProperty", item.PropertyId));
        }

        foreach (var item in batch.Buildings)
        {
            var entity = await _dbContext.Buildings.ForCompany(renoCompanyID).FirstOrDefaultAsync(building => building.BuildingId == item.Id, cancellationToken);

            if (entity is null)
            {
                _dbContext.Buildings.Add(new Building
                {
                    BuildingId = item.Id,
                    RenoCompanyID = renoCompanyID,
                    PropertyId = item.PropertyId,
                    BuildingTypeId = item.BuildingTypeId,
                    Name = item.Name,
                    SortOrder = item.SortOrder
                });
                results.Add(MobileSyncResult.Created(nameof(Building), item.Id));
                continue;
            }

            entity.PropertyId = item.PropertyId;
            entity.BuildingTypeId = item.BuildingTypeId;
            entity.Name = item.Name;
            entity.SortOrder = item.SortOrder;
            results.Add(MobileSyncResult.Updated(nameof(Building), item.Id));
        }

        foreach (var item in batch.Inspections)
        {
            var entity = await _dbContext.Inspections.ForCompany(renoCompanyID).FirstOrDefaultAsync(inspection => inspection.InspectionId == item.Id, cancellationToken);
            var incomingUpdatedAtUtc = NormalizeUtc(item.UpdatedAtUtc);

            if (entity is null)
            {
                _dbContext.Inspections.Add(new Inspection
                {
                    InspectionId = item.Id,
                    RenoCompanyID = renoCompanyID,
                    CreatedAtUtc = NormalizeUtc(item.CreatedAtUtc),
                    UpdatedAtUtc = incomingUpdatedAtUtc,
                    Title = item.Title,
                    InspectionDate = NormalizeDate(item.InspectionDate),
                    InspectorName = item.InspectorName,
                    GeneralNotes = item.GeneralNotes,
                    PropertyId = item.PropertyId,
                    CustomerId = item.CustomerId
                });
                results.Add(MobileSyncResult.Created(nameof(Inspection), item.Id));
                continue;
            }

            if (entity.UpdatedAtUtc > incomingUpdatedAtUtc)
            {
                results.Add(MobileSyncResult.Conflict(nameof(Inspection), item.Id, "Server row is newer than incoming mobile row."));
                continue;
            }

            entity.CreatedAtUtc = NormalizeUtc(item.CreatedAtUtc);
            entity.UpdatedAtUtc = incomingUpdatedAtUtc;
            entity.Title = item.Title;
            entity.InspectionDate = NormalizeDate(item.InspectionDate);
            entity.InspectorName = item.InspectorName;
            entity.GeneralNotes = item.GeneralNotes;
            entity.PropertyId = item.PropertyId;
            entity.CustomerId = item.CustomerId;
            results.Add(MobileSyncResult.Updated(nameof(Inspection), item.Id));
        }

        foreach (var buildingId in batch.DeletedBuildingIds.Distinct())
        {
            var photosDeleted = await _dbContext.InspectionAreaNotePhotos
                .ForCompany(renoCompanyID)
                .Where(photo => photo.BuildingId == buildingId)
                .ExecuteDeleteAsync(cancellationToken);
            var estimateItemsDeleted = await _dbContext.InspectionAreaNoteEstimateItems
                .ForCompany(renoCompanyID)
                .Where(item => item.BuildingId == buildingId)
                .ExecuteDeleteAsync(cancellationToken);
            var notesDeleted = await _dbContext.InspectionAreaNotes
                .ForCompany(renoCompanyID)
                .Where(note => note.BuildingId == buildingId)
                .ExecuteDeleteAsync(cancellationToken);
            var areasDeleted = await _dbContext.InspectionAreas
                .ForCompany(renoCompanyID)
                .Where(area => area.BuildingId == buildingId)
                .ExecuteDeleteAsync(cancellationToken);
            var buildingsDeleted = await _dbContext.Buildings
                .ForCompany(renoCompanyID)
                .Where(building => building.BuildingId == buildingId)
                .ExecuteDeleteAsync(cancellationToken);

            results.Add(buildingsDeleted > 0 || areasDeleted > 0 || notesDeleted > 0 || estimateItemsDeleted > 0 || photosDeleted > 0
                ? MobileSyncResult.Deleted(nameof(Building), buildingId)
                : MobileSyncResult.Unchanged(nameof(Building), buildingId));
        }

        foreach (var areaId in batch.DeletedInspectionAreaIds.Distinct())
        {
            var photosDeleted = await _dbContext.InspectionAreaNotePhotos
                .ForCompany(renoCompanyID)
                .Where(photo => photo.AreaId == areaId)
                .ExecuteDeleteAsync(cancellationToken);
            var estimateItemsDeleted = await _dbContext.InspectionAreaNoteEstimateItems
                .ForCompany(renoCompanyID)
                .Where(item => item.AreaId == areaId)
                .ExecuteDeleteAsync(cancellationToken);
            var notesDeleted = await _dbContext.InspectionAreaNotes
                .ForCompany(renoCompanyID)
                .Where(note => note.AreaId == areaId)
                .ExecuteDeleteAsync(cancellationToken);
            var areasDeleted = await _dbContext.InspectionAreas
                .ForCompany(renoCompanyID)
                .Where(area => area.InspectionAreaId == areaId)
                .ExecuteDeleteAsync(cancellationToken);

            results.Add(areasDeleted > 0 || notesDeleted > 0 || estimateItemsDeleted > 0 || photosDeleted > 0
                ? MobileSyncResult.Deleted(nameof(InspectionArea), areaId)
                : MobileSyncResult.Unchanged(nameof(InspectionArea), areaId));
        }

        foreach (var item in batch.InspectionAreas)
        {
            var entity = await _dbContext.InspectionAreas.ForCompany(renoCompanyID).FirstOrDefaultAsync(area => area.InspectionAreaId == item.Id, cancellationToken);

            if (entity is null)
            {
                _dbContext.InspectionAreas.Add(new InspectionArea
                {
                    InspectionAreaId = item.Id,
                    RenoCompanyID = renoCompanyID,
                    PropertyId = item.PropertyId,
                    BuildingId = item.BuildingId,
                    AreaTypeId = item.AreaTypeId,
                    DisplayName = item.DisplayName,
                    OverallRating = item.OverallRating,
                    SortOrder = item.SortOrder
                });
                results.Add(MobileSyncResult.Created(nameof(InspectionArea), item.Id));
                continue;
            }

            entity.PropertyId = item.PropertyId;
            entity.BuildingId = item.BuildingId;
            entity.AreaTypeId = item.AreaTypeId;
            entity.DisplayName = item.DisplayName;
            entity.OverallRating = item.OverallRating;
            entity.SortOrder = item.SortOrder;
            results.Add(MobileSyncResult.Updated(nameof(InspectionArea), item.Id));
        }

        foreach (var item in batch.InspectionAreaNotes)
        {
            var entity = await _dbContext.InspectionAreaNotes.ForCompany(renoCompanyID).FirstOrDefaultAsync(note => note.InspectionAreaNoteId == item.Id, cancellationToken);
            var incomingUpdatedAtUtc = NormalizeUtc(item.UpdatedAtUtc);

            if (entity is null)
            {
                _dbContext.InspectionAreaNotes.Add(new InspectionAreaNote
                {
                    InspectionAreaNoteId = item.Id,
                    RenoCompanyID = renoCompanyID,
                    PropertyId = item.PropertyId,
                    BuildingId = item.BuildingId,
                    AreaId = item.AreaId,
                    CreatedAtUtc = NormalizeUtc(item.CreatedAtUtc),
                    UpdatedAtUtc = incomingUpdatedAtUtc,
                    Text = item.Text
                });
                results.Add(MobileSyncResult.Created(nameof(InspectionAreaNote), item.Id));
                continue;
            }

            if (entity.UpdatedAtUtc > incomingUpdatedAtUtc)
            {
                results.Add(MobileSyncResult.Conflict(nameof(InspectionAreaNote), item.Id, "Server row is newer than incoming mobile row."));
                continue;
            }

            entity.PropertyId = item.PropertyId;
            entity.BuildingId = item.BuildingId;
            entity.AreaId = item.AreaId;
            entity.CreatedAtUtc = NormalizeUtc(item.CreatedAtUtc);
            entity.UpdatedAtUtc = incomingUpdatedAtUtc;
            entity.Text = item.Text;
            results.Add(MobileSyncResult.Updated(nameof(InspectionAreaNote), item.Id));
        }

        foreach (var item in batch.InspectionAreaNoteEstimateItems)
        {
            var entity = await _dbContext.InspectionAreaNoteEstimateItems.ForCompany(renoCompanyID).FirstOrDefaultAsync(estimateItem => estimateItem.InspectionAreaNoteEstimateItemId == item.Id, cancellationToken);
            var incomingUpdatedAtUtc = NormalizeUtc(item.UpdatedAtUtc);

            if (entity is null)
            {
                _dbContext.InspectionAreaNoteEstimateItems.Add(new InspectionAreaNoteEstimateItem
                {
                    InspectionAreaNoteEstimateItemId = item.Id,
                    RenoCompanyID = renoCompanyID,
                    PropertyId = item.PropertyId,
                    BuildingId = item.BuildingId,
                    AreaId = item.AreaId,
                    AreaNoteId = item.AreaNoteId,
                    CreatedAtUtc = NormalizeUtc(item.CreatedAtUtc),
                    UpdatedAtUtc = incomingUpdatedAtUtc,
                    Name = item.Name,
                    Cost = item.Cost,
                    Hours = item.Hours
                });
                results.Add(MobileSyncResult.Created(nameof(InspectionAreaNoteEstimateItem), item.Id));
                continue;
            }

            if (entity.UpdatedAtUtc > incomingUpdatedAtUtc)
            {
                results.Add(MobileSyncResult.Conflict(nameof(InspectionAreaNoteEstimateItem), item.Id, "Server row is newer than incoming mobile row."));
                continue;
            }

            entity.PropertyId = item.PropertyId;
            entity.BuildingId = item.BuildingId;
            entity.AreaId = item.AreaId;
            entity.AreaNoteId = item.AreaNoteId;
            entity.CreatedAtUtc = NormalizeUtc(item.CreatedAtUtc);
            entity.UpdatedAtUtc = incomingUpdatedAtUtc;
            entity.Name = item.Name;
            entity.Cost = item.Cost;
            entity.Hours = item.Hours;
            results.Add(MobileSyncResult.Updated(nameof(InspectionAreaNoteEstimateItem), item.Id));
        }

        foreach (var item in batch.InspectionAreaNotePhotos)
        {
            var entity = await _dbContext.InspectionAreaNotePhotos.ForCompany(renoCompanyID).FirstOrDefaultAsync(photo => photo.InspectionAreaNotePhotoId == item.Id, cancellationToken);

            if (!TryDecodeBase64(item.DataBase64, out var data))
            {
                results.Add(MobileSyncResult.Skipped(nameof(InspectionAreaNotePhoto), item.Id, "Photo DataBase64 was not valid base64."));
                continue;
            }

            if (entity is null)
            {
                _dbContext.InspectionAreaNotePhotos.Add(new InspectionAreaNotePhoto
                {
                    InspectionAreaNotePhotoId = item.Id,
                    RenoCompanyID = renoCompanyID,
                    PropertyId = item.PropertyId,
                    BuildingId = item.BuildingId,
                    AreaId = item.AreaId,
                    AreaNoteId = item.AreaNoteId,
                    CreatedAtUtc = NormalizeUtc(item.CreatedAtUtc),
                    FileName = item.FileName,
                    ContentType = item.ContentType,
                    Data = data
                });
                results.Add(MobileSyncResult.Created(nameof(InspectionAreaNotePhoto), item.Id));
                continue;
            }

            entity.PropertyId = item.PropertyId;
            entity.BuildingId = item.BuildingId;
            entity.AreaId = item.AreaId;
            entity.AreaNoteId = item.AreaNoteId;
            entity.CreatedAtUtc = NormalizeUtc(item.CreatedAtUtc);
            entity.FileName = item.FileName;
            entity.ContentType = item.ContentType;
            entity.Data = data;
            results.Add(MobileSyncResult.Updated(nameof(InspectionAreaNotePhoto), item.Id));
        }

        foreach (var item in batch.MileageTracking)
        {
            var entity = await _dbContext.MileageTracking.ForCompany(renoCompanyID).FirstOrDefaultAsync(session => session.MileageTrackingID == item.UniqueId, cancellationToken);

            if (entity is null)
            {
                _dbContext.MileageTracking.Add(new MileageTracking
                {
                    MileageTrackingID = item.UniqueId,
                    RenoCompanyID = renoCompanyID,
                    TrackingStartedAtUtc = NormalizeUtc(item.TrackingStartedAtUtc),
                    TotalMileage = item.TotalMileage,
                    TotalTime = item.TotalTime,
                    StartingLocation = item.StartingLocation,
                    StartingPosition = item.StartingPosition,
                    EndingLocation = item.EndingLocation,
                    EndingPosition = item.EndingPosition,
                    InspectionId = item.InspectionId
                });
                results.Add(MobileSyncResult.Created(nameof(MileageTracking), item.UniqueId));
                continue;
            }

            entity.TrackingStartedAtUtc = NormalizeUtc(item.TrackingStartedAtUtc);
            entity.TotalMileage = item.TotalMileage;
            entity.TotalTime = item.TotalTime;
            entity.StartingLocation = item.StartingLocation;
            entity.StartingPosition = item.StartingPosition;
            entity.EndingLocation = item.EndingLocation;
            entity.EndingPosition = item.EndingPosition;
            entity.InspectionId = item.InspectionId;
            results.Add(MobileSyncResult.Updated(nameof(MileageTracking), item.UniqueId));
        }

        foreach (var item in batch.MileageTrackingWaypoints)
        {
            var entity = await _dbContext.MileageTrackingWaypoints.ForCompany(renoCompanyID).FirstOrDefaultAsync(waypoint => waypoint.MileageTrackingWaypointId == item.UniqueId, cancellationToken);

            if (entity is null)
            {
                _dbContext.MileageTrackingWaypoints.Add(new MileageTrackingWaypoint
                {
                    MileageTrackingWaypointId = item.UniqueId,
                    RenoCompanyID = renoCompanyID,
                    MileageTrackingId = item.MileageTrackingId,
                    WaypointTime = NormalizeUtc(item.WaypointTime),
                    CumulativeMiles = item.CumulativeMiles,
                    GpsCoordinates = item.GpsCoordinates,
                    Location = item.Location
                });
                results.Add(MobileSyncResult.Created(nameof(MileageTrackingWaypoint), item.UniqueId));
                continue;
            }

            entity.MileageTrackingId = item.MileageTrackingId;
            entity.WaypointTime = NormalizeUtc(item.WaypointTime);
            entity.CumulativeMiles = item.CumulativeMiles;
            entity.GpsCoordinates = item.GpsCoordinates;
            entity.Location = item.Location;
            results.Add(MobileSyncResult.Updated(nameof(MileageTrackingWaypoint), item.UniqueId));
        }

        foreach (var item in batch.CalendarEvents)
        {
            var entity = await _dbContext.CalendarEvents
                .ForCompany(renoCompanyID)
                .FirstOrDefaultAsync(calendarEvent => calendarEvent.CalendarEventId == item.UniqueEventId, cancellationToken);
            var incomingUpdatedAtUtc = NormalizeUtc(item.UpdatedAtUtc);

            if (entity is null)
            {
                if (item.IsDeleted)
                {
                    results.Add(MobileSyncResult.Unchanged(nameof(CalendarEvent), item.UniqueEventId));
                    continue;
                }

                _dbContext.CalendarEvents.Add(new CalendarEvent
                {
                    CalendarEventId = item.UniqueEventId,
                    RenoCompanyID = renoCompanyID,
                    RenoUserID = item.RenoUserID,
                    Title = item.Title,
                    Date = NormalizeDate(item.Date),
                    AllDay = item.AllDay,
                    StartTime = item.StartTime,
                    EndTime = item.EndTime,
                    EventAlertTimes = item.EventAlertTimes,
                    Notes = item.Notes,
                    IsPrivate = item.IsPrivate,
                    IsDeleted = item.IsDeleted,
                    InspectionId = item.InspectionId,
                    CreatedAtUtc = NormalizeUtc(item.CreatedAtUtc),
                    UpdatedAtUtc = incomingUpdatedAtUtc
                });
                results.Add(MobileSyncResult.Created(nameof(CalendarEvent), item.UniqueEventId));
                continue;
            }

            if (entity.UpdatedAtUtc > incomingUpdatedAtUtc)
            {
                results.Add(MobileSyncResult.Conflict(nameof(CalendarEvent), item.UniqueEventId, "Server row is newer than incoming mobile row."));
                continue;
            }

            entity.RenoUserID = item.RenoUserID;
            entity.Title = item.Title;
            entity.Date = NormalizeDate(item.Date);
            entity.AllDay = item.AllDay;
            entity.StartTime = item.StartTime;
            entity.EndTime = item.EndTime;
            entity.EventAlertTimes = item.EventAlertTimes;
            entity.Notes = item.Notes;
            entity.IsPrivate = item.IsPrivate;
            entity.IsDeleted = item.IsDeleted;
            entity.InspectionId = item.InspectionId;
            entity.CreatedAtUtc = NormalizeUtc(item.CreatedAtUtc);
            entity.UpdatedAtUtc = incomingUpdatedAtUtc;
            results.Add(item.IsDeleted
                ? MobileSyncResult.Updated(nameof(CalendarEvent), item.UniqueEventId)
                : MobileSyncResult.Updated(nameof(CalendarEvent), item.UniqueEventId));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return results;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    private static bool TryDecodeBase64(string value, out byte[] data)
    {
        try
        {
            data = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            data = [];
            return false;
        }
    }
    private static void ApplyEmployee(MobileSyncEmployee item, Employee entity, bool isDefaultInspector)
    {
        entity.GivenName = item.FirstName.Trim();
        entity.FamilyName = item.LastName.Trim();
        entity.PrimaryPhone = item.Phone.Trim();
        entity.PrimaryEmailAddress = item.Email.Trim();
        entity.IsInspector = item.IsInspector;
        entity.IsDefaultInspector = isDefaultInspector;
        entity.InspectorHourlyRate = item.InspectorHourlyRate;
        entity.DisplayName = GetEmployeeDisplayName(entity);
        entity.PrintOnCheckName = entity.DisplayName;
        entity.LastEditDate = DateTime.UtcNow;
    }

    private static string GetEmployeeDisplayName(Employee employee)
    {
        var name = $"{employee.GivenName} {employee.FamilyName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? employee.DisplayName : name;
    }


    private static void ApplyCustomer(MobileSyncCustomer item, Customer entity)
    {
        entity.GivenName = item.FirstName;
        entity.FamilyName = item.LastName;
        entity.CompanyName = item.CompanyName;
        entity.PrimaryPhone = item.Phone;
        entity.PrimaryEmailAddress = item.Email;
        entity.Notes = item.Notes;

        var displayName = string.Join(" ", new[] { item.FirstName, item.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        entity.DisplayName = string.IsNullOrWhiteSpace(displayName) ? item.CompanyName : displayName;
        entity.FullyQualifiedName = entity.DisplayName;
    }

    private void ApplyCustomerAddress(MobileSyncCustomer item, Customer entity)
    {
        if (string.IsNullOrWhiteSpace(item.Street1)
            && string.IsNullOrWhiteSpace(item.Street2)
            && string.IsNullOrWhiteSpace(item.City)
            && string.IsNullOrWhiteSpace(item.State)
            && string.IsNullOrWhiteSpace(item.PostalCode))
        {
            return;
        }

        var address = entity.BillAddress;
        if (address is null)
        {
            address = new Address { RenoCompanyID = entity.RenoCompanyID };
            entity.BillAddress = address;
        }

        address.Street1 = item.Street1;
        address.Street2 = item.Street2;
        address.City = item.City;
        address.State = item.State;
        address.PostalCode = item.PostalCode;
    }
}

public sealed record MobileSyncBatch(
    IReadOnlyList<MobileSyncAppSetting> Settings,
    IReadOnlyList<MobileSyncPartSource> PartSources,
    IReadOnlyList<MobileSyncPart> Parts,
    IReadOnlyList<MobileSyncInspectionAreaCategory> InspectionAreaCategories,
    IReadOnlyList<MobileSyncInspectionAreaType> InspectionAreaTypes,
    IReadOnlyList<MobileSyncBuildingType> BuildingTypes,
    IReadOnlyList<MobileSyncEmployee> Employees,
    IReadOnlyList<MobileSyncCustomer> Customers,
    IReadOnlyList<MobileSyncCustomerProperty> CustomerProperties,
    IReadOnlyList<MobileSyncProperty> Properties,
    IReadOnlyList<MobileSyncAddress> Addresses,
    IReadOnlyList<MobileSyncBuilding> Buildings,
    IReadOnlyList<MobileSyncInspection> Inspections,
    IReadOnlyList<MobileSyncInspectionArea> InspectionAreas,
    IReadOnlyList<MobileSyncInspectionAreaNote> InspectionAreaNotes,
    IReadOnlyList<MobileSyncInspectionAreaNoteEstimateItem> InspectionAreaNoteEstimateItems,
    IReadOnlyList<MobileSyncInspectionAreaNotePhoto> InspectionAreaNotePhotos,
    IReadOnlyList<MobileSyncMileageTracking> MileageTracking,
    IReadOnlyList<MobileSyncMileageTrackingWaypoint> MileageTrackingWaypoints,
    IReadOnlyList<MobileSyncCalendarEvent> CalendarEvents,
    IReadOnlyList<Guid> DeletedInspectionAreaIds,
    IReadOnlyList<Guid> DeletedBuildingIds);

public sealed record MobileSyncResult(string EntityName, Guid Id, string Status, string? Message)
{
    public static MobileSyncResult Created(string entityName, Guid id) => new(entityName, id, "created", null);
    public static MobileSyncResult Updated(string entityName, Guid id) => new(entityName, id, "updated", null);
    public static MobileSyncResult Deleted(string entityName, Guid id) => new(entityName, id, "deleted", null);
    public static MobileSyncResult Unchanged(string entityName, Guid id) => new(entityName, id, "unchanged", null);
    public static MobileSyncResult Conflict(string entityName, Guid id, string message) => new(entityName, id, "conflict", message);
    public static MobileSyncResult Skipped(string entityName, Guid id, string message) => new(entityName, id, "skipped", message);
}

public sealed record MobileSyncAppSetting(Guid Id, string Name, string Value);
public sealed record MobileSyncPartSource(Guid PartSourceId, string Name);
public sealed record MobileSyncPart(Guid PartId, Guid PartSourceId, string Name, string Description, string ModelNumber, string Manufacturer, string Sku, string? Url, string? ImageUrl, decimal Cost, bool IsPackage, int PackageUnits);
public sealed record MobileSyncInspectionAreaCategory(Guid Id, string Name, int SortOrder);
public sealed record MobileSyncInspectionAreaType(Guid AreaTypeId, Guid CategoryId, string Name, int SortOrder);
public sealed record MobileSyncBuildingType(Guid Id, string Name);
public sealed record MobileSyncEmployee(Guid Id, string FirstName, string LastName, string Phone, string Email, bool IsInspector, bool IsDefaultInspector, decimal InspectorHourlyRate);
public sealed record MobileSyncCustomer(Guid CustomerId, string FirstName, string LastName, string CompanyName, string Phone, string Email, string Street1, string Street2, string City, string State, string PostalCode, string Notes);
public sealed record MobileSyncCustomerProperty(Guid CustomerId, Guid PropertyId);
public sealed record MobileSyncProperty(Guid Id, string? Name);
public sealed record MobileSyncAddress(Guid Id, Guid? PropertyId, string Street1, string Street2, string City, string State, string PostalCode);
public sealed record MobileSyncBuilding(Guid Id, Guid PropertyId, Guid BuildingTypeId, string Name, int SortOrder);
public sealed record MobileSyncInspection(Guid Id, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string Title, DateTime InspectionDate, string InspectorName, string GeneralNotes, Guid PropertyId, Guid? CustomerId);
public sealed record MobileSyncInspectionArea(Guid Id, Guid PropertyId, Guid? BuildingId, Guid AreaTypeId, string DisplayName, int OverallRating, int SortOrder);
public sealed record MobileSyncInspectionAreaNote(Guid Id, Guid PropertyId, Guid? BuildingId, Guid AreaId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string Text);
public sealed record MobileSyncInspectionAreaNoteEstimateItem(Guid Id, Guid PropertyId, Guid? BuildingId, Guid AreaId, Guid AreaNoteId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string Name, decimal Cost, decimal Hours);
public sealed record MobileSyncInspectionAreaNotePhoto(Guid Id, Guid PropertyId, Guid? BuildingId, Guid AreaId, Guid AreaNoteId, DateTime CreatedAtUtc, string FileName, string ContentType, string DataBase64);
public sealed record MobileSyncMileageTracking(Guid UniqueId, DateTime TrackingStartedAtUtc, double TotalMileage, TimeSpan TotalTime, string StartingLocation, string StartingPosition, string EndingLocation, string EndingPosition, Guid? InspectionId);
public sealed record MobileSyncMileageTrackingWaypoint(Guid UniqueId, Guid MileageTrackingId, DateTime WaypointTime, double CumulativeMiles, string GpsCoordinates, string? Location);
public sealed record MobileSyncCalendarEvent(Guid UniqueEventId, Guid RenoUserID, string Title, DateTime Date, bool AllDay, TimeSpan StartTime, TimeSpan EndTime, string EventAlertTimes, string Notes, bool IsPrivate, Guid? InspectionId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, bool IsDeleted);



