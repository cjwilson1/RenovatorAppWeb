using Microsoft.AspNetCore.Mvc;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Infrastructure.Services;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

public sealed class InspectionsController : Controller
{
    private readonly InspectionDataService _inspectionDataService;

    public InspectionsController(InspectionDataService inspectionDataService)
    {
        _inspectionDataService = inspectionDataService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var inspections = await _inspectionDataService.GetInspectionsAsync(cancellationToken);
        var model = inspections.Select(ToListItem).ToList();

        return View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var inspection = await _inspectionDataService.GetInspectionDetailAsync(id, cancellationToken);

        if (inspection is null)
        {
            return NotFound();
        }

        return View(new InspectionDetailViewModel
        {
            Inspection = inspection,
            PropertyAddress = GetPropertyAddress(inspection.Property),
            ClientName = GetClientName(inspection.Client)
        });
    }

    private static InspectionListItemViewModel ToListItem(Inspection inspection)
    {
        return new InspectionListItemViewModel(
            inspection.Id,
            inspection.Title,
            inspection.InspectionDate,
            inspection.InspectorName,
            GetPropertyAddress(inspection.Property),
            GetClientName(inspection.Client));
    }

    private static string GetPropertyAddress(Property? property)
    {
        if (property?.Address is null)
        {
            return string.Empty;
        }

        var address = property.Address;
        var cityStateZip = string.Join(" ", new[]
        {
            string.Join(", ", new[] { address.City, address.State }.Where(value => !string.IsNullOrWhiteSpace(value))),
            address.PostalCode
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.Join(" - ", new[]
        {
            string.Join(" ", new[] { address.Street1, address.Street2 }.Where(value => !string.IsNullOrWhiteSpace(value))),
            cityStateZip
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string GetClientName(Client? client)
    {
        if (client is null)
        {
            return string.Empty;
        }

        var name = $"{client.FirstName} {client.LastName}".Trim();

        return string.IsNullOrWhiteSpace(name) ? client.CompanyName : name;
    }
}
