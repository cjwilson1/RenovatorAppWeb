using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Infrastructure.Services;
using RenovatorApp.Web.Services;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

public sealed class InspectionsController : Controller
{
    private readonly InspectionDataService _inspectionDataService;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly CurrentUserSession _currentUserSession;

    public InspectionsController(
        InspectionDataService inspectionDataService,
        IWebHostEnvironment webHostEnvironment,
        CurrentUserSession currentUserSession)
    {
        _inspectionDataService = inspectionDataService;
        _webHostEnvironment = webHostEnvironment;
        _currentUserSession = currentUserSession;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var inspections = await _inspectionDataService.GetInspectionsAsync(_currentUserSession.RenoCompanyID, cancellationToken);
        var model = inspections.Select(ToListItem).ToList();

        return View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var inspection = await _inspectionDataService.GetInspectionDetailAsync(_currentUserSession.RenoCompanyID, id, cancellationToken);

        if (inspection is null)
        {
            return NotFound();
        }

        return View(new InspectionDetailViewModel
        {
            Inspection = inspection,
            PropertyAddress = GetPropertyAddress(inspection.Property),
            CustomerName = GetCustomerName(inspection.Customer),
            DefaultReportName = BuildDefaultReportName(inspection.Title, DateTime.Now)
        });
    }

    [HttpGet("Inspections/New")]
    public async Task<IActionResult> New(CancellationToken cancellationToken)
    {
        return View("Edit", new InspectionEditViewModel
        {
            InspectionDate = DateTime.Today,
            Inspectors = await GetInspectorPickerItemsAsync(cancellationToken),
            Parts = await GetPartPickerItemsAsync(cancellationToken)
        });
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var inspection = await _inspectionDataService.GetInspectionDetailAsync(_currentUserSession.RenoCompanyID, id, cancellationToken);

        if (inspection is null)
        {
            return NotFound();
        }

        return View(new InspectionEditViewModel
        {
            Id = inspection.Id,
            Title = inspection.Title,
            InspectionDate = inspection.InspectionDate,
            InspectorName = inspection.InspectorName,
            GeneralNotes = inspection.GeneralNotes,
            PropertyAddress = ToPropertyAddressEditViewModel(inspection.Property.Address),
            Customer = ToCustomerEditViewModel(inspection.Customer),
            Buildings = ToBuildingEditViewModels(inspection.Property),
            Inspectors = await GetInspectorPickerItemsAsync(cancellationToken),
            Parts = await GetPartPickerItemsAsync(cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(Guid? id)
    {
        if (id.HasValue)
        {
            return RedirectToAction(nameof(Details), new { id = id.Value });
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportPdf(Guid id, string reportName, CancellationToken cancellationToken)
    {
        var inspection = await _inspectionDataService.GetInspectionDetailAsync(_currentUserSession.RenoCompanyID, id, cancellationToken);

        if (inspection is null)
        {
            return NotFound();
        }

        if (inspection.CustomerId is null)
        {
            return BadRequest("Inspection reports must have a customer before they can be saved as customer documents.");
        }

        var propertyAddress = GetPropertyAddress(inspection.Property);
        var customerName = GetCustomerName(inspection.Customer);
        var logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "MikeHandymanLogo.png");
        var estimateItems = GetEstimateItems(inspection);
        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.DefaultTextStyle(text => text.FontSize(10));

                page.Header().Element(container => ComposeReportHeader(container, logoPath));

                page.Content()
                    .PaddingVertical(20)
                    .Column(column =>
                    {
                        column.Spacing(10);
                        column.Item().Text(inspection.Title).FontSize(16).SemiBold();
                        column.Item().Text(text =>
                        {
                            text.Span("Inspection Date: ").SemiBold();
                            text.Span(inspection.InspectionDate.ToString("d"));
                        });
                        column.Item().Text($"Inspector: {inspection.InspectorName}");
                        column.Item().Text($"Property: {(string.IsNullOrWhiteSpace(propertyAddress) ? "No property address" : propertyAddress)}");
                        column.Item().Text($"Customer: {(string.IsNullOrWhiteSpace(customerName) ? "No customer assigned" : customerName)}");
                        column.Item().PaddingTop(10).Element(container => ComposeBuildings(container, inspection));
                        column.Item().PaddingTop(8).Element(container => ComposeSummary(container, estimateItems));
                    });

                page.Footer().Element(ComposePageFooter);
            });

            container.Page(page =>
            {
                page.Margin(40);
                page.DefaultTextStyle(text => text.FontSize(10));

                page.Header().Element(container => ComposeReportHeader(container, logoPath));

                page.Content()
                    .PaddingVertical(20)
                    .Column(column =>
                    {
                        column.Spacing(12);
                        column.Item().Text("Parts List").FontSize(16).SemiBold();
                        column.Item().Element(container => ComposePartsList(container, estimateItems));
                    });

                page.Footer().Element(container => ComposePartsFooter(container, estimateItems));
            });
        });

        var pdfBytes = document.GeneratePdf();
        const string extension = ".pdf";
        var documentName = GetSafeReportName(reportName);
        var fileName = $"{documentName}{extension}";
        var documentsDirectory = Path.Combine(_webHostEnvironment.ContentRootPath, "Documents", "Inspections");
        Directory.CreateDirectory(documentsDirectory);

        var documentPath = GetAvailableDocumentPath(documentsDirectory, fileName);
        fileName = Path.GetFileName(documentPath);
        await System.IO.File.WriteAllBytesAsync(documentPath, pdfBytes, cancellationToken);

        await _inspectionDataService.AddDocumentAsync(_currentUserSession.RenoCompanyID, new RenovatorApp.Infrastructure.Models.Document
        {
            DocumentName = documentName,
            CustomerId = inspection.CustomerId,
            CreateDate = DateTime.UtcNow,
            DocumentType = "inspection",
            Filename = fileName,
            Extension = extension,
            Path = documentPath
        }, cancellationToken);

        return File(pdfBytes, "application/pdf", fileName);
    }

    private static void ComposeReportHeader(IContainer container, string logoPath)
    {
        container.Row(row =>
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
    }

    private static void ComposePageFooter(IContainer container)
    {
        container
            .AlignCenter()
            .Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
    }

    private static void ComposeSummary(IContainer container, IReadOnlyCollection<InspectionAreaNoteEstimateItem> estimateItems)
    {
        var totalCost = estimateItems.Sum(item => item.Cost);
        var totalHours = estimateItems.Sum(item => item.Hours);

        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten4)
            .Padding(12)
            .Column(column =>
            {
                column.Spacing(6);
                column.Item().Text("Summary").FontSize(14).SemiBold();
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Total cost:");
                    row.ConstantItem(100).AlignRight().Text(FormatCurrency(totalCost)).SemiBold();
                });
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Total hours:");
                    row.ConstantItem(100).AlignRight().Text(totalHours.ToString("0.##")).SemiBold();
                });
            });
    }

    private static void ComposePartsList(IContainer container, IReadOnlyCollection<InspectionAreaNoteEstimateItem> estimateItems)
    {
        if (estimateItems.Count == 0)
        {
            container.Text("No parts found.").Italic();
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(100);
            });

            table.Header(header =>
            {
                header.Cell().Element(EstimateHeaderCell).Text("Name");
                header.Cell().Element(EstimateHeaderCell).AlignRight().Text("Cost");
            });

            foreach (var item in estimateItems.OrderBy(item => item.Name))
            {
                table.Cell().Element(EstimateBodyCell).Text(item.Name);
                table.Cell().Element(EstimateBodyCell).AlignRight().Text(FormatCurrency(item.Cost));
            }
        });
    }

    private static void ComposePartsFooter(IContainer container, IReadOnlyCollection<InspectionAreaNoteEstimateItem> estimateItems)
    {
        var totalPartsCost = estimateItems.Sum(item => item.Cost);

        container
            .BorderTop(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingTop(8)
            .Row(row =>
            {
                row.RelativeItem().Text("Total parts cost:").SemiBold();
                row.ConstantItem(120).AlignRight().Text(FormatCurrency(totalPartsCost)).SemiBold();
            });
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
                        .Text($"Rating: {FormatRating(area.OverallRating)}")
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
                        table.Cell().Element(EstimateBodyCell).AlignRight().Text(FormatCurrency(item.Cost));
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

    private static string FormatRating(int rating)
    {
        return rating == 0 ? "n/a" : rating.ToString();
    }

    private static string FormatCurrency(decimal value)
    {
        return $"${value:N2}";
    }

    private static IReadOnlyList<InspectionAreaNoteEstimateItem> GetEstimateItems(Inspection inspection)
    {
        return inspection.Property.Areas
            .SelectMany(area => area.AreaNotes)
            .SelectMany(note => note.EstimateItems)
            .ToList();
    }

    private static InspectionListItemViewModel ToListItem(Inspection inspection)
    {
        return new InspectionListItemViewModel(
            inspection.Id,
            inspection.Title,
            inspection.InspectionDate,
            inspection.InspectorName,
            GetPropertyAddress(inspection.Property),
            GetCustomerName(inspection.Customer));
    }

    private static InspectionPropertyAddressEditViewModel ToPropertyAddressEditViewModel(Address? address)
    {
        if (address is null)
        {
            return new InspectionPropertyAddressEditViewModel();
        }

        return new InspectionPropertyAddressEditViewModel
        {
            Street1 = address.Street1,
            Street2 = address.Street2,
            City = address.City,
            State = address.State,
            PostalCode = address.PostalCode
        };
    }

    private async Task<IReadOnlyList<InspectorPickerItemViewModel>> GetInspectorPickerItemsAsync(CancellationToken cancellationToken)
    {
        var inspectors = await _inspectionDataService.GetInspectorsAsync(_currentUserSession.RenoCompanyID, cancellationToken);

        return inspectors
            .Select(inspector => new InspectorPickerItemViewModel
            {
                Id = inspector.Id,
                FullName = GetInspectorFullName(inspector),
                Email = inspector.Email,
                Phone = inspector.Phone,
                HourlyRate = inspector.HourlyRate
            })
            .ToList();
    }

    private static string GetInspectorFullName(Inspector inspector)
    {
        var fullName = $"{inspector.FirstName} {inspector.LastName}".Trim();

        return string.IsNullOrWhiteSpace(fullName) ? "Unnamed Inspector" : fullName;
    }

    private async Task<IReadOnlyList<PartPickerItemViewModel>> GetPartPickerItemsAsync(CancellationToken cancellationToken)
    {
        var parts = await _inspectionDataService.GetPartsAsync(_currentUserSession.RenoCompanyID, cancellationToken);

        return parts
            .Select(part => new PartPickerItemViewModel
            {
                Id = part.PartId,
                Name = part.Name,
                Description = part.Description,
                SourceName = part.PartSource?.Name ?? string.Empty,
                Sku = part.Sku,
                Manufacturer = part.Manufacturer,
                Cost = part.Cost
            })
            .ToList();
    }

    private static InspectionCustomerEditViewModel ToCustomerEditViewModel(Customer? customer)
    {
        if (customer is null)
        {
            return new InspectionCustomerEditViewModel();
        }

        return new InspectionCustomerEditViewModel
        {
            CustomerId = customer.CustomerId,
            FirstName = customer.GivenName,
            LastName = customer.FamilyName,
            CompanyName = customer.CompanyName,
            Phone = customer.PrimaryPhone,
            Email = customer.PrimaryEmailAddress,
            Street1 = customer.BillAddress?.Street1 ?? string.Empty,
            Street2 = customer.BillAddress?.Street2 ?? string.Empty,
            City = customer.BillAddress?.City ?? string.Empty,
            State = customer.BillAddress?.State ?? string.Empty,
            PostalCode = customer.BillAddress?.PostalCode ?? string.Empty,
            Notes = customer.Notes
        };
    }

    private static IReadOnlyList<InspectionBuildingEditViewModel> ToBuildingEditViewModels(Property? property)
    {
        if (property is null)
        {
            return [];
        }

        return property.Buildings
            .OrderBy(building => building.SortOrder)
            .ThenBy(building => building.Name)
            .Select(building => new InspectionBuildingEditViewModel
            {
                Id = building.Id,
                Name = building.Name,
                BuildingTypeName = string.IsNullOrWhiteSpace(building.BuildingType?.Name)
                    ? "No building type"
                    : building.BuildingType.Name,
                Areas = property.Areas
                    .Where(area => area.BuildingId == building.Id)
                    .OrderBy(area => area.SortOrder)
                    .ThenBy(area => area.DisplayName)
                    .Select(area => new InspectionAreaEditViewModel
                    {
                        Id = area.Id,
                        DisplayName = area.DisplayName,
                        AreaTypeName = string.IsNullOrWhiteSpace(area.AreaType?.Name)
                            ? "No area type"
                            : area.AreaType.Name,
                        OverallRating = area.OverallRating,
                        Notes = area.AreaNotes
                            .OrderBy(note => note.CreatedAtUtc)
                            .Select(note => new InspectionAreaNoteEditViewModel
                            {
                                Id = note.Id,
                                Text = note.Text,
                                EstimateCost = note.EstimateItems.Sum(item => item.Cost),
                                EstimateHours = note.EstimateItems.Sum(item => item.Hours),
                                EstimateItems = note.EstimateItems
                                    .OrderBy(item => item.Name)
                                    .Select(item => new InspectionAreaNoteEstimateItemEditViewModel
                                    {
                                        Id = item.Id,
                                        Name = item.Name,
                                        Cost = item.Cost,
                                        Hours = item.Hours,
                                        IsNew = false
                                    })
                                    .ToList(),
                                Photos = note.Photos
                                    .Where(photo => photo.Data.Length > 0)
                                    .OrderBy(photo => photo.CreatedAtUtc)
                                    .Select(ToPhotoEditViewModel)
                                    .ToList()
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();
    }

    private static InspectionAreaNotePhotoEditViewModel ToPhotoEditViewModel(InspectionAreaNotePhoto photo)
    {
        var dimensions = GetImageDimensions(photo.Data);

        return new InspectionAreaNotePhotoEditViewModel
        {
            Id = photo.Id,
            FileName = photo.FileName,
            ContentType = photo.ContentType,
            ImageType = GetImageType(photo.FileName, photo.ContentType, photo.Data),
            SizeBytes = photo.Data.LongLength,
            WidthPixels = dimensions.Width,
            HeightPixels = dimensions.Height,
            Data = photo.Data
        };
    }

    private static string GetImageType(string fileName, string contentType, byte[] data)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var type = contentType.Split('/').LastOrDefault();

            if (!string.IsNullOrWhiteSpace(type))
            {
                return type.Equals("jpeg", StringComparison.OrdinalIgnoreCase) ? "jpg" : type.ToLowerInvariant();
            }
        }

        var extension = Path.GetExtension(fileName).TrimStart('.');

        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.Equals("jpeg", StringComparison.OrdinalIgnoreCase) ? "jpg" : extension.ToLowerInvariant();
        }

        if (IsPng(data))
        {
            return "png";
        }

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return "jpg";
        }

        if (data.Length >= 6 && data.AsSpan(0, 3).SequenceEqual("GIF"u8))
        {
            return "gif";
        }

        if (data.Length >= 12 && data.AsSpan(0, 4).SequenceEqual("RIFF"u8) && data.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return "webp";
        }

        return "unknown";
    }

    private static (int? Width, int? Height) GetImageDimensions(byte[] data)
    {
        return TryGetPngDimensions(data)
            ?? TryGetJpegDimensions(data)
            ?? TryGetGifDimensions(data)
            ?? TryGetWebPDimensions(data)
            ?? ((int?)null, null);
    }

    private static (int? Width, int? Height)? TryGetPngDimensions(byte[] data)
    {
        if (data.Length < 24 || !IsPng(data))
        {
            return null;
        }

        return (ReadBigEndianInt32(data, 16), ReadBigEndianInt32(data, 20));
    }

    private static bool IsPng(byte[] data)
    {
        return data.Length >= 8
            && data[0] == 137
            && data[1] == 80
            && data[2] == 78
            && data[3] == 71
            && data[4] == 13
            && data[5] == 10
            && data[6] == 26
            && data[7] == 10;
    }

    private static (int? Width, int? Height)? TryGetJpegDimensions(byte[] data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return null;
        }

        var index = 2;

        while (index + 8 < data.Length)
        {
            if (data[index] != 0xFF)
            {
                index++;
                continue;
            }

            while (index < data.Length && data[index] == 0xFF)
            {
                index++;
            }

            if (index >= data.Length)
            {
                return null;
            }

            var marker = data[index++];

            if (marker is 0xD8 or 0xD9 or 0x01)
            {
                continue;
            }

            if (index + 2 > data.Length)
            {
                return null;
            }

            var segmentLength = ReadBigEndianInt16(data, index);

            if (segmentLength < 2 || index + segmentLength > data.Length)
            {
                return null;
            }

            if (marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or >= 0xC9 and <= 0xCB or >= 0xCD and <= 0xCF)
            {
                return (ReadBigEndianInt16(data, index + 5), ReadBigEndianInt16(data, index + 3));
            }

            index += segmentLength;
        }

        return null;
    }

    private static (int? Width, int? Height)? TryGetGifDimensions(byte[] data)
    {
        if (data.Length < 10 || !data.AsSpan(0, 3).SequenceEqual("GIF"u8))
        {
            return null;
        }

        return (ReadLittleEndianInt16(data, 6), ReadLittleEndianInt16(data, 8));
    }

    private static (int? Width, int? Height)? TryGetWebPDimensions(byte[] data)
    {
        if (data.Length < 30 || !data.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !data.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return null;
        }

        if (data.AsSpan(12, 4).SequenceEqual("VP8X"u8) && data.Length >= 30)
        {
            return (1 + ReadLittleEndianInt24(data, 24), 1 + ReadLittleEndianInt24(data, 27));
        }

        if (data.AsSpan(12, 4).SequenceEqual("VP8 "u8) && data.Length >= 30)
        {
            return (ReadLittleEndianInt16(data, 26) & 0x3FFF, ReadLittleEndianInt16(data, 28) & 0x3FFF);
        }

        return null;
    }

    private static int ReadBigEndianInt32(byte[] data, int index)
    {
        return data[index] << 24 | data[index + 1] << 16 | data[index + 2] << 8 | data[index + 3];
    }

    private static int ReadBigEndianInt16(byte[] data, int index)
    {
        return data[index] << 8 | data[index + 1];
    }

    private static int ReadLittleEndianInt16(byte[] data, int index)
    {
        return data[index] | data[index + 1] << 8;
    }

    private static int ReadLittleEndianInt24(byte[] data, int index)
    {
        return data[index] | data[index + 1] << 8 | data[index + 2] << 16;
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

    private static string GetCustomerName(Customer? customer)
    {
        if (customer is null)
        {
            return string.Empty;
        }

        var name = $"{customer.GivenName} {customer.FamilyName}".Trim();

        return string.IsNullOrWhiteSpace(name) ? customer.CompanyName : name;
    }

    private static string BuildDefaultReportName(string inspectionName, DateTime dateTime)
    {
        return $"{GetSafeReportName(inspectionName)}_{dateTime:yyyyddMM_HHmm}";
    }

    private static string GetSafeReportName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeName = new string(value
            .Where(character => !invalidCharacters.Contains(character))
            .Where(character => !char.IsPunctuation(character) || character is '_' or '-')
            .ToArray());

        return string.IsNullOrWhiteSpace(safeName) ? "Inspection" : safeName;
    }

    private static string GetAvailableDocumentPath(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);

        if (!System.IO.File.Exists(path))
        {
            return path;
        }

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var counter = 2;

        do
        {
            path = Path.Combine(directory, $"{nameWithoutExtension}_{counter}{extension}");
            counter++;
        }
        while (System.IO.File.Exists(path));

        return path;
    }
}

