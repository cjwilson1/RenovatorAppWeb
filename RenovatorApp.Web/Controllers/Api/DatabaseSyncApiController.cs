using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.Services;

namespace RenovatorApp.Web.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/database-sync")]
public sealed class DatabaseSyncApiController : ControllerBase
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string RenoCompanyIDHeaderName = "X-Reno-Company-ID";
    private readonly IConfiguration _configuration;
    private readonly RenovatorAppDbContext _dbContext;
    private readonly CurrentUserSession _currentUserSession;

    public DatabaseSyncApiController(IConfiguration configuration, RenovatorAppDbContext dbContext, CurrentUserSession currentUserSession)
    {
        _configuration = configuration;
        _dbContext = dbContext;
        _currentUserSession = currentUserSession;
    }

    [HttpGet]
    public async Task<ActionResult<DatabaseSyncResponse>> GetDatabase(CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        if (!TryGetRenoCompanyID(out var renoCompanyID))
        {
            ModelState.AddModelError(RenoCompanyIDHeaderName, $"{RenoCompanyIDHeaderName} is required until mobile authentication is implemented.");
            return ValidationProblem(ModelState);
        }

        var addresses = await _dbContext.Addresses.AsNoTracking().ForCompany(renoCompanyID).OrderBy(address => address.AddressId).Select(address => new DatabaseSyncAddressDto(address.AddressId, address.PropertyId, address.Street1, address.Street2, address.City, address.State, address.PostalCode)).ToListAsync(cancellationToken);
        var buildings = await _dbContext.Buildings.AsNoTracking().ForCompany(renoCompanyID).OrderBy(building => building.PropertyId).ThenBy(building => building.SortOrder).ThenBy(building => building.Name).Select(building => new DatabaseSyncBuildingDto(building.BuildingId, building.PropertyId, building.BuildingTypeId, building.Name, building.SortOrder)).ToListAsync(cancellationToken);
        var buildingTypes = await _dbContext.BuildingTypes.AsNoTracking().ForCompany(renoCompanyID).OrderBy(buildingType => buildingType.Name).Select(buildingType => new DatabaseSyncBuildingTypeDto(buildingType.BuildingTypeId, buildingType.Name)).ToListAsync(cancellationToken);
        var customers = await _dbContext.Customers.AsNoTracking().ForCompany(renoCompanyID).Include(customer => customer.BillAddress).OrderBy(customer => customer.DisplayName).Select(customer => ToCustomerDto(customer)).ToListAsync(cancellationToken);
        var inspectors = await _dbContext.Inspectors.AsNoTracking().ForCompany(renoCompanyID).OrderBy(inspector => inspector.FirstName).ThenBy(inspector => inspector.LastName).Select(inspector => new DatabaseSyncInspectorDto(inspector.InspectorId, inspector.FirstName, inspector.LastName, inspector.HourlyRate, inspector.Phone, inspector.Email, inspector.IsDefault)).ToListAsync(cancellationToken);
        var properties = await _dbContext.Properties.AsNoTracking().ForCompany(renoCompanyID).OrderBy(property => property.PropertyId).Select(property => new DatabaseSyncPropertyDto(property.PropertyId, property.Name)).ToListAsync(cancellationToken);
        var parts = await _dbContext.Parts.AsNoTracking().ForCompany(renoCompanyID).OrderBy(part => part.Name).Select(part => new DatabaseSyncPartDto(part.PartId, part.PartSourceId, part.Name, part.Description, part.ModelNumber, part.Manufacturer, part.Sku, part.Url, part.ImageUrl, part.Cost, part.IsPackage, part.PackageUnits)).ToListAsync(cancellationToken);
        var partSources = await _dbContext.PartSources.AsNoTracking().ForCompany(renoCompanyID).OrderBy(partSource => partSource.Name).Select(partSource => new DatabaseSyncPartSourceDto(partSource.PartSourceId, partSource.Name)).ToListAsync(cancellationToken);
        var inspectionAreaCategories = await _dbContext.InspectionAreaCategories.AsNoTracking().ForCompany(renoCompanyID).OrderBy(category => category.SortOrder).ThenBy(category => category.Name).Select(category => new DatabaseSyncInspectionAreaCategoryDto(category.InspectionAreaCategoryId, category.Name, category.SortOrder)).ToListAsync(cancellationToken);
        var inspectionAreaTypes = await _dbContext.InspectionAreaTypes.AsNoTracking().ForCompany(renoCompanyID).OrderBy(areaType => areaType.SortOrder).ThenBy(areaType => areaType.Name).Select(areaType => new DatabaseSyncInspectionAreaTypeDto(areaType.InspectionAreaTypeId, areaType.CategoryId, areaType.Name, areaType.SortOrder)).ToListAsync(cancellationToken);

        return new DatabaseSyncResponse(DateTime.UtcNow, addresses, buildings, buildingTypes, customers, inspectors, properties, parts, partSources, inspectionAreaCategories, inspectionAreaTypes);
    }

    private bool IsAuthorized()
    {
        var configuredApiKey = _configuration["MobileSync:ApiKey"] ?? _configuration["MOBILE_SYNC_API_KEY"];
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            return true;
        }

        return Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKey) && string.Equals(apiKey.ToString(), configuredApiKey, StringComparison.Ordinal);
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

    private static DatabaseSyncCustomerDto ToCustomerDto(Customer customer)
    {
        var address = customer.BillAddress;
        return new DatabaseSyncCustomerDto(customer.CustomerId, customer.QuickBooksCustomerId, customer.SyncToken, customer.DisplayName, customer.FullyQualifiedName, customer.CompanyName, customer.Title, customer.GivenName, customer.MiddleName, customer.FamilyName, customer.Suffix, customer.PrintOnCheckName, customer.PrimaryEmailAddress, customer.PrimaryPhone, customer.AlternatePhone, customer.MobilePhone, customer.Fax, customer.Website, customer.Notes, customer.Active, customer.Taxable, customer.Job, customer.BillWithParent, customer.Balance, customer.BalanceWithJobs, customer.PreferredDeliveryMethod, customer.ParentRefValue, customer.ParentRefName, customer.PaymentMethodRefValue, customer.PaymentMethodRefName, customer.SalesTermRefValue, customer.SalesTermRefName, customer.CurrencyRefValue, customer.CurrencyRefName, customer.QuickBooksCreateTime, customer.QuickBooksLastUpdatedTime, address?.Street1 ?? string.Empty, address?.Street2 ?? string.Empty, address?.City ?? string.Empty, address?.State ?? string.Empty, address?.PostalCode ?? string.Empty, customer.CreatedDate, customer.LastSyncDate, customer.LastEditDate);
    }
}

public sealed record DatabaseSyncResponse(DateTime PulledAtUtc, IReadOnlyList<DatabaseSyncAddressDto> Addresses, IReadOnlyList<DatabaseSyncBuildingDto> Buildings, IReadOnlyList<DatabaseSyncBuildingTypeDto> BuildingTypes, IReadOnlyList<DatabaseSyncCustomerDto> Customers, IReadOnlyList<DatabaseSyncInspectorDto> Inspectors, IReadOnlyList<DatabaseSyncPropertyDto> Properties, IReadOnlyList<DatabaseSyncPartDto> Parts, IReadOnlyList<DatabaseSyncPartSourceDto> PartSources, IReadOnlyList<DatabaseSyncInspectionAreaCategoryDto> InspectionAreaCategories, IReadOnlyList<DatabaseSyncInspectionAreaTypeDto> InspectionAreaTypes);
public sealed record DatabaseSyncAddressDto(Guid Id, Guid? PropertyId, string Street1, string Street2, string City, string State, string PostalCode);
public sealed record DatabaseSyncBuildingDto(Guid Id, Guid PropertyId, Guid BuildingTypeId, string Name, int SortOrder);
public sealed record DatabaseSyncBuildingTypeDto(Guid Id, string Name);
public sealed record DatabaseSyncCustomerDto(Guid CustomerId, string QuickBooksCustomerId, string SyncToken, string DisplayName, string FullyQualifiedName, string CompanyName, string Title, string GivenName, string MiddleName, string FamilyName, string Suffix, string PrintOnCheckName, string PrimaryEmailAddress, string PrimaryPhone, string AlternatePhone, string MobilePhone, string Fax, string Website, string Notes, bool Active, bool Taxable, bool Job, bool BillWithParent, decimal Balance, decimal BalanceWithJobs, string PreferredDeliveryMethod, string ParentRefValue, string ParentRefName, string PaymentMethodRefValue, string PaymentMethodRefName, string SalesTermRefValue, string SalesTermRefName, string CurrencyRefValue, string CurrencyRefName, DateTime? QuickBooksCreateTime, DateTime? QuickBooksLastUpdatedTime, string Street1, string Street2, string City, string State, string PostalCode, DateTime CreatedDate, DateTime? LastSyncDate, DateTime? LastEditDate);
public sealed record DatabaseSyncInspectorDto(Guid Id, string FirstName, string LastName, decimal HourlyRate, string Phone, string Email, bool IsDefault);
public sealed record DatabaseSyncPropertyDto(Guid Id, string Name);
public sealed record DatabaseSyncPartDto(Guid PartId, Guid PartSourceId, string Name, string Description, string ModelNumber, string Manufacturer, string Sku, string Url, string ImageUrl, decimal Cost, bool IsPackage, int PackageUnits);
public sealed record DatabaseSyncPartSourceDto(Guid PartSourceId, string Name);
public sealed record DatabaseSyncInspectionAreaCategoryDto(Guid Id, string Name, int SortOrder);
public sealed record DatabaseSyncInspectionAreaTypeDto(Guid AreaTypeId, Guid CategoryId, string Name, int SortOrder);
