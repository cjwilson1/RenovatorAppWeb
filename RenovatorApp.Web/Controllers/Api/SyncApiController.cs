using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RenovatorApp.Infrastructure.Services;
using RenovatorApp.Web.Models;

namespace RenovatorApp.Web.Controllers.Api;

[ApiController]
[Route("api/sync")]
public sealed class SyncApiController : ControllerBase
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly IConfiguration _configuration;
    private readonly MobileSyncDataService _mobileSyncDataService;

    public SyncApiController(IConfiguration configuration, MobileSyncDataService mobileSyncDataService)
    {
        _configuration = configuration;
        _mobileSyncDataService = mobileSyncDataService;
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
            results = await _mobileSyncDataService.SyncAsync(ToBatch(request), cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgresException)
        {
            return Problem(
                title: "Sync database update failed.",
                detail: $"{postgresException.SqlState}: {postgresException.MessageText} Constraint: {postgresException.ConstraintName}",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return new SyncResponse(
            syncedAtUtc,
            results.Select(result => new SyncEntityResult(
                    result.EntityName,
                    result.Id,
                    result.Status,
                    result.Message))
                .ToList());
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
            (request.Inspectors ?? []).Select(item => new MobileSyncInspector(
                item.Id,
                item.FirstName,
                item.LastName,
                item.HourlyRate,
                item.Phone,
                item.Email,
                item.IsDefault)).ToList(),
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
            (request.Properties ?? []).Select(item => new MobileSyncProperty(
                item.Id)).ToList(),
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
                item.DataBase64)).ToList());
    }
}
