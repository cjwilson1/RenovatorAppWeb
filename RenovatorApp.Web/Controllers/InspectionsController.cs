using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Infrastructure.Services;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

public sealed class InspectionsController : Controller
{
    private readonly InspectionDataService _inspectionDataService;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public InspectionsController(InspectionDataService inspectionDataService, IWebHostEnvironment webHostEnvironment)
    {
        _inspectionDataService = inspectionDataService;
        _webHostEnvironment = webHostEnvironment;
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
        var logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "MikeHandymanLogo.png");
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.DefaultTextStyle(text => text.FontSize(10));

                page.Header()
                    .Row(row =>
                    {
                        row.ConstantItem(95)
                            .Image(logoPath)
                            .FitArea();
                        row.RelativeItem()
                            .PaddingLeft(18)
                            .AlignMiddle()
                            .Text("RenovatorApp Inspection Report")
                            .FontSize(20)
                            .SemiBold()
                            .FontColor(Colors.Blue.Darken3);
                    });

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
                        column.Item().PaddingTop(10).Element(container => ComposeBuildings(container, inspection));
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

    private static void ComposeBuildings(IContainer container, Inspection inspection)
    {
        var buildings = inspection.Property.Buildings
            .OrderBy(building => building.SortOrder)
            .ThenBy(building => building.Name)
            .ToList();

        container.Column(column =>
        {
            column.Spacing(16);

            if (buildings.Count == 0)
            {
                column.Item().Text("No buildings found.").Italic();
                return;
            }

            foreach (var building in buildings)
            {
                var areas = inspection.Property.Areas
                    .Where(area => area.BuildingId == building.Id)
                    .OrderBy(area => area.SortOrder)
                    .ThenBy(area => area.DisplayName)
                    .ToList();

                column.Item()
                    .PaddingTop(8)
                    .Text(building.Name)
                    .FontSize(14)
                    .SemiBold();

                if (areas.Count == 0)
                {
                    column.Item().PaddingLeft(10).Text("No inspection areas found.").Italic();
                    continue;
                }

                foreach (var area in areas)
                {
                    column.Item().Element(areaContainer => ComposeInspectionArea(areaContainer, area));
                }
            }
        });
    }

    private static void ComposeInspectionArea(IContainer container, InspectionArea area)
    {
        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(10)
            .Column(column =>
            {
                column.Spacing(8);
                column.Item().Row(row =>
                {
                    row.RelativeItem()
                        .Text(area.DisplayName)
                        .FontSize(12)
                        .SemiBold();
                    row.ConstantItem(120)
                        .AlignRight()
                        .Text($"Rating: {area.OverallRating}")
                        .SemiBold();
                });

                var notes = area.AreaNotes
                    .OrderBy(note => note.CreatedAtUtc)
                    .ToList();

                if (notes.Count == 0)
                {
                    column.Item().Text("No notes found.").Italic();
                    return;
                }

                foreach (var note in notes)
                {
                    column.Item().Element(noteContainer => ComposeAreaNote(noteContainer, note));
                }
            });
    }

    private static void ComposeAreaNote(IContainer container, InspectionAreaNote note)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Text(string.IsNullOrWhiteSpace(note.Text) ? "No note text." : note.Text);
            column.Item().Element(ComposeEstimateItems(note.EstimateItems));
            column.Item().Element(ComposePhotos(note.Photos));
        });
    }

    private static Action<IContainer> ComposeEstimateItems(IReadOnlyCollection<InspectionAreaNoteEstimateItem> estimateItems)
    {
        return container =>
        {
            container.Column(column =>
            {
                column.Spacing(4);
                column.Item().Text("Estimate").SemiBold();

                if (estimateItems.Count == 0)
                {
                    column.Item().Text("No estimate items.").Italic();
                    return;
                }

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(80);
                        columns.ConstantColumn(70);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(EstimateHeaderCell).Text("Item");
                        header.Cell().Element(EstimateHeaderCell).AlignRight().Text("Cost");
                        header.Cell().Element(EstimateHeaderCell).AlignRight().Text("Hours");
                    });

                    foreach (var item in estimateItems.OrderBy(item => item.Name))
                    {
                        table.Cell().Element(EstimateBodyCell).Text(item.Name);
                        table.Cell().Element(EstimateBodyCell).AlignRight().Text(item.Cost.ToString("C"));
                        table.Cell().Element(EstimateBodyCell).AlignRight().Text(item.Hours.ToString("0.##"));
                    }
                });
            });
        };
    }

    private static Action<IContainer> ComposePhotos(IReadOnlyCollection<InspectionAreaNotePhoto> photos)
    {
        return container =>
        {
            container.Column(column =>
            {
                column.Spacing(6);
                column.Item().Text("Pictures").SemiBold();

                var availablePhotos = photos
                    .Where(photo => photo.Data.Length > 0)
                    .Take(3)
                    .ToList();

                if (availablePhotos.Count == 0)
                {
                    column.Item().Element(ComposeDummyPicture);
                    return;
                }

                column.Item().Row(row =>
                {
                    row.Spacing(8);

                    foreach (var photo in availablePhotos)
                    {
                        row.RelativeItem()
                            .Height(100)
                            .Border(1)
                            .BorderColor(Colors.Grey.Lighten2)
                            .Padding(3)
                            .Image(photo.Data)
                            .FitArea();
                    }
                });
            });
        };
    }

    private static void ComposeDummyPicture(IContainer container)
    {
        container
            .Height(100)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten1)
            .Background(Colors.Grey.Lighten4)
            .AlignCenter()
            .AlignMiddle()
            .Text("Picture placeholder")
            .FontColor(Colors.Grey.Darken1);
    }

    private static IContainer EstimateHeaderCell(IContainer container)
    {
        return container
            .Background(Colors.Grey.Lighten3)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten1)
            .Padding(4);
    }

    private static IContainer EstimateBodyCell(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Padding(4);
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
