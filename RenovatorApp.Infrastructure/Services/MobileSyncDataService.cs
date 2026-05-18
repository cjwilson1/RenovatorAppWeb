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

    public async Task<IReadOnlyList<MobileSyncResult>> SyncAsync(MobileSyncBatch batch, CancellationToken cancellationToken = default)
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
                .FirstOrDefaultAsync(setting => setting.Name.ToLower() == "defaultstate", cancellationToken);

            if (entity is null)
            {
                _dbContext.AppSettings.Add(new AppSetting
                {
                    Id = item.Id,
                    Name = item.Name,
                    Value = item.Value
                });
                results.Add(MobileSyncResult.Created(nameof(AppSetting), item.Id));
                continue;
            }

            entity.Name = item.Name;
            entity.Value = item.Value;
            results.Add(MobileSyncResult.Updated(nameof(AppSetting), entity.Id));
        }

        foreach (var item in batch.PartSources)
        {
            var entity = await _dbContext.PartSources.FindAsync([item.PartSourceId], cancellationToken);

            if (entity is null)
            {
                _dbContext.PartSources.Add(new PartSource
                {
                    PartSourceId = item.PartSourceId,
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
            var entity = await _dbContext.Parts.FindAsync([item.PartId], cancellationToken);

            if (entity is null)
            {
                _dbContext.Parts.Add(new Part
                {
                    PartId = item.PartId,
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
            var entity = await _dbContext.InspectionAreaCategories.FindAsync([item.Id], cancellationToken);

            if (entity is null)
            {
                _dbContext.InspectionAreaCategories.Add(new InspectionAreaCategory
                {
                    Id = item.Id,
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
            var entity = await _dbContext.InspectionAreaTypes.FindAsync([item.AreaTypeId], cancellationToken);

            if (entity is null)
            {
                _dbContext.InspectionAreaTypes.Add(new InspectionAreaType
                {
                    AreaTypeId = item.AreaTypeId,
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
            var entity = await _dbContext.BuildingTypes.FindAsync([item.Id], cancellationToken);

            if (entity is null)
            {
                _dbContext.BuildingTypes.Add(new BuildingType
                {
                    Id = item.Id,
                    Name = item.Name
                });
                results.Add(MobileSyncResult.Created(nameof(BuildingType), item.Id));
                continue;
            }

            entity.Name = item.Name;
            results.Add(MobileSyncResult.Updated(nameof(BuildingType), item.Id));
        }

        foreach (var item in batch.Inspectors)
        {
            var entity = await _dbContext.Inspectors.FindAsync([item.Id], cancellationToken);

            if (entity is null)
            {
                _dbContext.Inspectors.Add(new Inspector
                {
                    Id = item.Id,
                    FirstName = item.FirstName,
                    LastName = item.LastName,
                    HourlyRate = item.HourlyRate,
                    Phone = item.Phone,
                    Email = item.Email,
                    IsDefault = item.IsDefault
                });
                results.Add(MobileSyncResult.Created(nameof(Inspector), item.Id));
                continue;
            }

            entity.FirstName = item.FirstName;
            entity.LastName = item.LastName;
            entity.HourlyRate = item.HourlyRate;
            entity.Phone = item.Phone;
            entity.Email = item.Email;
            entity.IsDefault = item.IsDefault;
            results.Add(MobileSyncResult.Updated(nameof(Inspector), item.Id));
        }

        foreach (var item in batch.Customers)
        {
            var entity = await _dbContext.Customers
                .Include(customer => customer.BillAddress)
                .FirstOrDefaultAsync(customer => customer.CustomerId == item.CustomerId, cancellationToken);

            if (entity is null)
            {
                entity = new Customer
                {
                    CustomerId = item.CustomerId,
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
            var entity = await _dbContext.Properties.FindAsync([item.Id], cancellationToken);

            if (entity is null)
            {
                _dbContext.Properties.Add(new Property { Id = item.Id });
                results.Add(MobileSyncResult.Created(nameof(Property), item.Id));
                continue;
            }

            results.Add(MobileSyncResult.Unchanged(nameof(Property), item.Id));
        }

        foreach (var item in batch.Addresses)
        {
            var entity = await _dbContext.Addresses.FindAsync([item.Id], cancellationToken);

            if (entity is null)
            {
                _dbContext.Addresses.Add(new Address
                {
                    Id = item.Id,
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

        foreach (var item in batch.Buildings)
        {
            var entity = await _dbContext.Buildings.FindAsync([item.Id], cancellationToken);

            if (entity is null)
            {
                _dbContext.Buildings.Add(new Building
                {
                    Id = item.Id,
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
            var entity = await _dbContext.Inspections.FindAsync([item.Id], cancellationToken);
            var incomingUpdatedAtUtc = NormalizeUtc(item.UpdatedAtUtc);

            if (entity is null)
            {
                _dbContext.Inspections.Add(new Inspection
                {
                    Id = item.Id,
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

        foreach (var item in batch.InspectionAreas)
        {
            var entity = await _dbContext.InspectionAreas.FindAsync([item.Id], cancellationToken);

            if (entity is null)
            {
                _dbContext.InspectionAreas.Add(new InspectionArea
                {
                    Id = item.Id,
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
            var entity = await _dbContext.InspectionAreaNotes.FindAsync([item.Id], cancellationToken);
            var incomingUpdatedAtUtc = NormalizeUtc(item.UpdatedAtUtc);

            if (entity is null)
            {
                _dbContext.InspectionAreaNotes.Add(new InspectionAreaNote
                {
                    Id = item.Id,
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
            var entity = await _dbContext.InspectionAreaNoteEstimateItems.FindAsync([item.Id], cancellationToken);
            var incomingUpdatedAtUtc = NormalizeUtc(item.UpdatedAtUtc);

            if (entity is null)
            {
                _dbContext.InspectionAreaNoteEstimateItems.Add(new InspectionAreaNoteEstimateItem
                {
                    Id = item.Id,
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
            var entity = await _dbContext.InspectionAreaNotePhotos.FindAsync([item.Id], cancellationToken);

            if (!TryDecodeBase64(item.DataBase64, out var data))
            {
                results.Add(MobileSyncResult.Skipped(nameof(InspectionAreaNotePhoto), item.Id, "Photo DataBase64 was not valid base64."));
                continue;
            }

            if (entity is null)
            {
                _dbContext.InspectionAreaNotePhotos.Add(new InspectionAreaNotePhoto
                {
                    Id = item.Id,
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
            address = new Address();
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
    IReadOnlyList<MobileSyncInspector> Inspectors,
    IReadOnlyList<MobileSyncCustomer> Customers,
    IReadOnlyList<MobileSyncProperty> Properties,
    IReadOnlyList<MobileSyncAddress> Addresses,
    IReadOnlyList<MobileSyncBuilding> Buildings,
    IReadOnlyList<MobileSyncInspection> Inspections,
    IReadOnlyList<MobileSyncInspectionArea> InspectionAreas,
    IReadOnlyList<MobileSyncInspectionAreaNote> InspectionAreaNotes,
    IReadOnlyList<MobileSyncInspectionAreaNoteEstimateItem> InspectionAreaNoteEstimateItems,
    IReadOnlyList<MobileSyncInspectionAreaNotePhoto> InspectionAreaNotePhotos);

public sealed record MobileSyncResult(string EntityName, Guid Id, string Status, string? Message)
{
    public static MobileSyncResult Created(string entityName, Guid id) => new(entityName, id, "created", null);
    public static MobileSyncResult Updated(string entityName, Guid id) => new(entityName, id, "updated", null);
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
public sealed record MobileSyncInspector(Guid Id, string FirstName, string LastName, decimal HourlyRate, string Phone, string Email, bool IsDefault);
public sealed record MobileSyncCustomer(Guid CustomerId, string FirstName, string LastName, string CompanyName, string Phone, string Email, string Street1, string Street2, string City, string State, string PostalCode, string Notes);
public sealed record MobileSyncProperty(Guid Id);
public sealed record MobileSyncAddress(Guid Id, Guid PropertyId, string Street1, string Street2, string City, string State, string PostalCode);
public sealed record MobileSyncBuilding(Guid Id, Guid PropertyId, Guid BuildingTypeId, string Name, int SortOrder);
public sealed record MobileSyncInspection(Guid Id, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string Title, DateTime InspectionDate, string InspectorName, string GeneralNotes, Guid PropertyId, Guid? CustomerId);
public sealed record MobileSyncInspectionArea(Guid Id, Guid PropertyId, Guid? BuildingId, Guid AreaTypeId, string DisplayName, int OverallRating, int SortOrder);
public sealed record MobileSyncInspectionAreaNote(Guid Id, Guid PropertyId, Guid? BuildingId, Guid AreaId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string Text);
public sealed record MobileSyncInspectionAreaNoteEstimateItem(Guid Id, Guid PropertyId, Guid? BuildingId, Guid AreaId, Guid AreaNoteId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string Name, decimal Cost, decimal Hours);
public sealed record MobileSyncInspectionAreaNotePhoto(Guid Id, Guid PropertyId, Guid? BuildingId, Guid AreaId, Guid AreaNoteId, DateTime CreatedAtUtc, string FileName, string ContentType, string DataBase64);
