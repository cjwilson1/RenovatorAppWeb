using Microsoft.AspNetCore.Mvc;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Infrastructure.Services;
using RenovatorApp.Web.Services;

namespace RenovatorApp.Web.Controllers.Api;

[ApiController]
[Route("api/inspections")]
public sealed class InspectionsApiController : ControllerBase
{
    private readonly InspectionDataService _inspectionDataService;
    private readonly CurrentUserSession _currentUserSession;

    public InspectionsApiController(InspectionDataService inspectionDataService, CurrentUserSession currentUserSession)
    {
        _inspectionDataService = inspectionDataService;
        _currentUserSession = currentUserSession;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InspectionSummaryDto>>> GetInspections(CancellationToken cancellationToken)
    {
        var inspections = await _inspectionDataService.GetInspectionsAsync(_currentUserSession.RenoCompanyID, cancellationToken);

        return inspections.Select(ToSummaryDto).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InspectionDetailDto>> GetInspection(Guid id, CancellationToken cancellationToken)
    {
        var inspection = await _inspectionDataService.GetInspectionDetailAsync(_currentUserSession.RenoCompanyID, id, cancellationToken);

        return inspection is null ? NotFound() : ToDetailDto(inspection);
    }

    private static InspectionSummaryDto ToSummaryDto(Inspection inspection)
    {
        return new InspectionSummaryDto(
            inspection.InspectionId,
            inspection.Title,
            inspection.InspectionDate,
            inspection.UpdatedAtUtc,
            inspection.InspectorName,
            inspection.PropertyId,
            inspection.CustomerId);
    }

    private static InspectionDetailDto ToDetailDto(Inspection inspection)
    {
        return new InspectionDetailDto(
            ToSummaryDto(inspection),
            inspection.GeneralNotes,
            ToPropertyDto(inspection.Property),
            ToCustomerDto(inspection.Customer),
            inspection.Property.Buildings
                .OrderBy(building => building.SortOrder)
                .ThenBy(building => building.Name)
                .Select(building => new BuildingDto(
                    building.BuildingId,
                    building.Name,
                    building.BuildingType?.Name ?? string.Empty,
                    building.Areas.Select(ToAreaDto).ToList()))
                .ToList(),
            inspection.Property.Areas.Select(ToAreaDto).ToList());
    }

    private static PropertyDto ToPropertyDto(Property property)
    {
        return new PropertyDto(
            property.PropertyId,
            property.Address.Street1,
            property.Address.Street2,
            property.Address.City,
            property.Address.State,
            property.Address.PostalCode);
    }

    private static CustomerDto? ToCustomerDto(Customer? customer)
    {
        return customer is null
            ? null
            : new CustomerDto(
                customer.CustomerId,
                customer.GivenName,
                customer.FamilyName,
                customer.CompanyName,
                customer.PrimaryPhone,
                customer.PrimaryEmailAddress);
    }

    private static AreaDto ToAreaDto(InspectionArea area)
    {
        return new AreaDto(
            area.InspectionAreaId,
            area.DisplayName,
            area.AreaType?.Name ?? string.Empty,
            area.AreaType?.Category?.Name ?? string.Empty,
            area.OverallRating,
            area.AreaNotes.Select(note => new NoteDto(
                note.InspectionAreaNoteId,
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
        Guid? CustomerId);

    public sealed record InspectionDetailDto(
        InspectionSummaryDto Inspection,
        string GeneralNotes,
        PropertyDto Property,
        CustomerDto? Customer,
        IReadOnlyList<BuildingDto> Buildings,
        IReadOnlyList<AreaDto> PropertyAreas);

    public sealed record PropertyDto(
        Guid Id,
        string Street1,
        string Street2,
        string City,
        string State,
        string PostalCode);

    public sealed record CustomerDto(
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
