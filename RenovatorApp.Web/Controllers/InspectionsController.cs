using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
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

    public async Task<IActionResult> ReportPdf(Guid id, CancellationToken cancellationToken)
    {
        var inspection = await _inspectionDataService.GetInspectionDetailAsync(id, cancellationToken);

        if (inspection is null)
        {
            return NotFound();
        }

        var propertyAddress = GetPropertyAddress(inspection.Property);
        var clientName = GetClientName(inspection.Client);
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.DefaultTextStyle(text => text.FontSize(10));

                page.Header()
                    .Text("RenovatorApp Inspection Report")
                    .FontSize(20)
                    .SemiBold()
                    .FontColor(Colors.Blue.Darken3);

                page.Content()
                    .PaddingVertical(20)
                    .Column(column =>
                    {
                        column.Spacing(10);
                        column.Item().Text(inspection.Title).FontSize(16).SemiBold();
                        column.Item().Text($"Inspection Date: {inspection.InspectionDate:d}");
                        column.Item().Text($"Inspector: {inspection.InspectorName}");
                        column.Item().Text($"Property: {(string.IsNullOrWhiteSpace(propertyAddress) ? "No property address" : propertyAddress)}");
                        column.Item().Text($"Client: {(string.IsNullOrWhiteSpace(clientName) ? "No client assigned" : clientName)}");
                        column.Item().PaddingTop(10).Text("PDF report layout details will be added next.").Italic();
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
            });
        });

        var pdfBytes = document.GeneratePdf();
        var fileName = $"{GetSafeFileName(inspection.Title)}-InspectionReport.pdf";

        return File(pdfBytes, "application/pdf", fileName);
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

    private static string GetSafeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeName = new string(value
            .Select(character => invalidCharacters.Contains(character) ? '-' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(safeName) ? "Inspection" : safeName;
    }
}
