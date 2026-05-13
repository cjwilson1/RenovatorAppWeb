using Microsoft.AspNetCore.Mvc;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Infrastructure.Services;

namespace RenovatorApp.Web.Controllers.Api;

[ApiController]
[Route("api/inspections")]
public sealed class InspectionsApiController : ControllerBase
{
    private readonly InspectionDataService _inspectionDataService;

    public InspectionsApiController(InspectionDataService inspectionDataService)
    {
        _inspectionDataService = inspectionDataService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InspectionSummaryDto>>> GetInspections(CancellationToken cancellationToken)
    {
        var inspections = await _inspectionDataService.GetInspectionsAsync(cancellationToken);

        return inspections.Select(ToSummaryDto).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InspectionDetailDto>> GetInspection(Guid id, CancellationToken cancellationToken)
    {
        var inspection = await _inspectionDataService.GetInspectionDetailAsync(id, cancellationToken);

        return inspection is null ? NotFound() : ToDetailDto(inspection);
    }

    private static InspectionSummaryDto ToSummaryDto(Inspection inspection)
    {
        return new InspectionSummaryDto(
            inspection.Id,
            inspection.Title,
            inspection.InspectionDate,
            inspection.UpdatedAtUtc,
            inspection.InspectorName,
            inspection.PropertyId,
            inspection.ClientId);
    }

    private static InspectionDetailDto ToDetailDto(Inspection inspection)
    {
        return new InspectionDetailDto(
            ToSummaryDto(inspection),
            inspection.GeneralNotes,
            ToPropertyDto(inspection.Property),
            ToClientDto(inspection.Client),
            inspection.Property.Buildings
                .OrderBy(building => building.SortOrder)
                .ThenBy(building => building.Name)
                .Select(building => new BuildingDto(
                    building.Id,
                    building.Name,
                    building.BuildingType?.Name ?? string.Empty,
                    building.Areas.Select(ToAreaDto).ToList()))
                .ToList(),
            inspection.Property.Areas.Select(ToAreaDto).ToList());
    }

    private static PropertyDto ToPropertyDto(Property property)
    {
        return new PropertyDto(
            property.Id,
            property.Address.Street1,
            property.Address.Street2,
            property.Address.City,
            property.Address.State,
            property.Address.PostalCode);
    }

    private static ClientDto? ToClientDto(Client? client)
    {
        return client is null
            ? null
            : new ClientDto(
                client.ClientId,
                client.FirstName,
                client.LastName,
                client.CompanyName,
                client.Phone,
                client.Email);
    }

    private static AreaDto ToAreaDto(InspectionArea area)
    {
        return new AreaDto(
            area.Id,
            area.DisplayName,
            area.AreaType?.Name ?? string.Empty,
            area.AreaType?.Category?.Name ?? string.Empty,
            area.OverallRating,
            area.AreaNotes.Select(note => new NoteDto(
                note.Id,
                note.Text,
                note.EstimateItems.Count,
                note.Photos.Count)).ToList());
    }

    public sealed record InspectionSummaryDto(
        Guid Id,
        string Title,
        DateTime InspectionDate,
        DateTime UpdatedAtUtc,
        string InspectorName,
        Guid PropertyId,
        Guid? ClientId);

    public sealed record InspectionDetailDto(
        InspectionSummaryDto Inspection,
        string GeneralNotes,
        PropertyDto Property,
        ClientDto? Client,
        IReadOnlyList<BuildingDto> Buildings,
        IReadOnlyList<AreaDto> PropertyAreas);

    public sealed record PropertyDto(
        Guid Id,
        string Street1,
        string Street2,
        string City,
        string State,
        string PostalCode);

    public sealed record ClientDto(
        Guid Id,
        string FirstName,
        string LastName,
        string CompanyName,
        string Phone,
        string Email);

    public sealed record BuildingDto(
        Guid Id,
        string Name,
        string BuildingTypeName,
        IReadOnlyList<AreaDto> Areas);

    public sealed record AreaDto(
        Guid Id,
        string DisplayName,
        string AreaTypeName,
        string CategoryName,
        int OverallRating,
        IReadOnlyList<NoteDto> Notes);

    public sealed record NoteDto(
        Guid Id,
        string Text,
        int EstimateItemCount,
        int PhotoCount);
}
