using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Infrastructure.Services;
using RenovatorApp.Web.Services;
using RenovatorApp.Web.ViewModels;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RenovatorApp.Web.Controllers;

public sealed class InspectionsController : Controller
{
    private readonly InspectionDataService _inspectionDataService;
    private readonly RenovatorAppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly CurrentUserSession _currentUserSession;

    public InspectionsController(
        InspectionDataService inspectionDataService,
        RenovatorAppDbContext dbContext,
        IConfiguration configuration,
        IWebHostEnvironment webHostEnvironment,
        CurrentUserSession currentUserSession)
    {
        _inspectionDataService = inspectionDataService;
        _dbContext = dbContext;
        _configuration = configuration;
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

        var mileageTrackingRecords = await _dbContext.MileageTracking
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .Include(trip => trip.Waypoints)
            .Where(trip => trip.InspectionId == id)
            .OrderBy(trip => trip.TrackingStartedAtUtc)
            .ToListAsync(cancellationToken);
        var mileageTrackingViewModels = mileageTrackingRecords
            .Select(trip => new InspectionMileageTrackingViewModel
            {
                UniqueId = trip.MileageTrackingID,
                TrackingStartedAtUtc = trip.TrackingStartedAtUtc,
                TotalTime = trip.TotalTime,
                TotalMileage = trip.TotalMileage,
                MapImageUrl = BuildMileageStaticMapImageUrl(trip.Waypoints),
                Waypoints = trip.Waypoints
                    .OrderBy(waypoint => waypoint.WaypointTime)
                    .Select(waypoint => new InspectionMileageTrackingWaypointViewModel
                    {
                        WaypointTimeUtc = waypoint.WaypointTime,
                        CumulativeMiles = waypoint.CumulativeMiles,
                        GpsCoordinates = waypoint.GpsCoordinates,
                        Location = waypoint.Location ?? string.Empty
                    })
                    .ToList()
            })
            .ToList();
        var mileageTrackingAttachRecords = await _dbContext.MileageTracking
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .Include(trip => trip.Inspection)
            .OrderBy(trip => trip.TrackingStartedAtUtc)
            .Select(trip => new InspectionMileageTrackingAttachViewModel
            {
                UniqueId = trip.MileageTrackingID,
                TrackingStartedAtUtc = trip.TrackingStartedAtUtc,
                TotalTime = trip.TotalTime,
                TotalMileage = trip.TotalMileage,
                InspectionId = trip.InspectionId,
                InspectionTitle = trip.Inspection == null ? string.Empty : trip.Inspection.Title
            })
            .ToListAsync(cancellationToken);
        var attachDocumentRows = await _dbContext.Documents
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .Include(document => document.Customer)
            .Include(document => document.Inspection)
            .Include(document => document.DocumentType)
            .OrderByDescending(document => document.CreateDate)
            .ThenBy(document => document.DocumentName)
            .Select(document => new
            {
                document.DocumentId,
                document.DocumentName,
                document.CustomerId,
                document.InspectionId,
                CustomerGivenName = document.Customer == null ? string.Empty : document.Customer.GivenName,
                CustomerFamilyName = document.Customer == null ? string.Empty : document.Customer.FamilyName,
                InspectionTitle = document.Inspection == null ? string.Empty : document.Inspection.Title,
                DocumentType = document.DocumentType == null ? string.Empty : document.DocumentType.Name,
                document.CreateDate
            })
            .ToListAsync(cancellationToken);
        var attachDocuments = attachDocumentRows
            .Select(document => new InspectionAttachDocumentViewModel
            {
                DocumentId = document.DocumentId,
                DocumentName = document.DocumentName,
                CustomerName = string.Join(" ", new[] { document.CustomerGivenName, document.CustomerFamilyName }
                    .Where(value => !string.IsNullOrWhiteSpace(value))),
                InspectionTitle = document.InspectionTitle,
                DocumentType = document.DocumentType,
                CreateDate = document.CreateDate,
                IsAttached = document.CustomerId.HasValue || document.InspectionId.HasValue
            })
            .ToList();

        return View(new InspectionDetailViewModel
        {
            Inspection = inspection,
            PropertyAddress = GetPropertyAddress(inspection.Property),
            CustomerName = GetCustomerName(inspection.Customer),
            DefaultReportName = BuildDefaultReportName(inspection.Title, DateTime.Now),
            Documents = inspection.Documents
                .OrderByDescending(document => document.CreateDate)
                .ThenBy(document => document.DocumentName)
                .Select(document => new InspectionDocumentViewModel
                {
                    DocumentId = document.DocumentId,
                    DocumentName = document.DocumentName,
                    DocumentType = document.DocumentType?.Name ?? string.Empty,
                    Filename = document.Filename,
                    CreateDate = document.CreateDate
                })
                .ToList(),
            AttachDocuments = attachDocuments,
            MileageTrackingRecords = mileageTrackingViewModels,
            MileageTrackingAttachRecords = mileageTrackingAttachRecords
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _dbContext.Inspections
            .Where(inspection => inspection.InspectionId == id && inspection.RenoCompanyID == _currentUserSession.RenoCompanyID)
            .ExecuteDeleteAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DetachMileage(Guid id, Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await _dbContext.MileageTracking
            .ForCompany(_currentUserSession.RenoCompanyID)
            .FirstOrDefaultAsync(item => item.MileageTrackingID == tripId && item.InspectionId == id, cancellationToken);

        if (trip is not null)
        {
            trip.InspectionId = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDocument(Guid id, Guid documentId, string documentName, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .ForCompany(_currentUserSession.RenoCompanyID)
            .FirstOrDefaultAsync(item => item.DocumentId == documentId && item.InspectionId == id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        document.DocumentName = Clean(documentName);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DetachDocument(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .ForCompany(_currentUserSession.RenoCompanyID)
            .FirstOrDefaultAsync(item => item.DocumentId == documentId && item.InspectionId == id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        document.InspectionId = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocument(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .ForCompany(_currentUserSession.RenoCompanyID)
            .FirstOrDefaultAsync(item => item.DocumentId == documentId && item.InspectionId == id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AttachDocument(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var inspectionExists = await _dbContext.Inspections
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .AnyAsync(inspection => inspection.InspectionId == id, cancellationToken);

        if (!inspectionExists)
        {
            return NotFound();
        }

        var document = await _dbContext.Documents
            .ForCompany(_currentUserSession.RenoCompanyID)
            .FirstOrDefaultAsync(item => item.DocumentId == documentId, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        document.CustomerId = null;
        document.InspectionId = id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AttachMileage(Guid id, Guid tripId, CancellationToken cancellationToken)
    {
        var inspectionExists = await _dbContext.Inspections
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .AnyAsync(inspection => inspection.InspectionId == id, cancellationToken);

        if (!inspectionExists)
        {
            return NotFound();
        }

        var trip = await _dbContext.MileageTracking
            .ForCompany(_currentUserSession.RenoCompanyID)
            .FirstOrDefaultAsync(item => item.MileageTrackingID == tripId, cancellationToken);

        if (trip is null)
        {
            return NotFound();
        }

        trip.InspectionId = id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet("Inspections/New")]
    public async Task<IActionResult> New(
        string? customerSearch,
        int customerPage = 1,
        int customerRows = 15,
        bool showCustomerPicker = false,
        CancellationToken cancellationToken = default)
    {
        var inspectors = await GetInspectorPickerItemsAsync(cancellationToken);

        return View("Edit", new InspectionEditViewModel
        {
            InspectionDate = DateTime.Today,
            InspectorName = inspectors.FirstOrDefault(inspector => inspector.IsDefault)?.FullName ?? string.Empty,
            Inspectors = inspectors,
            Parts = await GetPartPickerItemsAsync(cancellationToken),
            CustomerPicker = await GetCustomerPickerAsync(customerSearch, customerPage, customerRows, showCustomerPicker, cancellationToken),
            ActiveTab = showCustomerPicker ? "customer" : "general"
        });
    }

    public async Task<IActionResult> Edit(Guid id, string? activeTab = null, CancellationToken cancellationToken = default)
    {
        var inspection = await _inspectionDataService.GetInspectionDetailAsync(_currentUserSession.RenoCompanyID, id, cancellationToken);

        if (inspection is null)
        {
            return NotFound();
        }

        return View(new InspectionEditViewModel
        {
            Id = inspection.InspectionId,
            Title = inspection.Title,
            InspectionDate = inspection.InspectionDate,
            InspectorName = inspection.InspectorName,
            GeneralNotes = inspection.GeneralNotes,
            PropertyId = inspection.PropertyId,
            PropertyAddress = ToPropertyAddressEditViewModel(inspection.Property.Address),
            Customer = ToCustomerEditViewModel(inspection.Customer),
            Buildings = ToBuildingEditViewModels(inspection.Property),
            Inspectors = await GetInspectorPickerItemsAsync(cancellationToken),
            Parts = await GetPartPickerItemsAsync(cancellationToken),
            CustomerPicker = await GetCustomerPickerAsync(null, 1, 15, false, cancellationToken),
            ActiveTab = NormalizeInspectionEditTab(activeTab)
        });
    }

    [HttpGet("Inspections/Edit/{id:guid}/FindCustomer")]
    public async Task<IActionResult> EditFindCustomer(
        Guid id,
        string? customerSearch,
        int customerPage = 1,
        int customerRows = 15,
        CancellationToken cancellationToken = default)
    {
        var inspection = await _inspectionDataService.GetInspectionDetailAsync(_currentUserSession.RenoCompanyID, id, cancellationToken);

        if (inspection is null)
        {
            return NotFound();
        }

        return View("Edit", new InspectionEditViewModel
        {
            Id = inspection.InspectionId,
            Title = inspection.Title,
            InspectionDate = inspection.InspectionDate,
            InspectorName = inspection.InspectorName,
            GeneralNotes = inspection.GeneralNotes,
            PropertyId = inspection.PropertyId,
            PropertyAddress = ToPropertyAddressEditViewModel(inspection.Property.Address),
            Customer = ToCustomerEditViewModel(inspection.Customer),
            Buildings = ToBuildingEditViewModels(inspection.Property),
            Inspectors = await GetInspectorPickerItemsAsync(cancellationToken),
            Parts = await GetPartPickerItemsAsync(cancellationToken),
            CustomerPicker = await GetCustomerPickerAsync(customerSearch, customerPage, customerRows, true, cancellationToken),
            ActiveTab = "customer"
        });
    }

    [HttpGet("Inspections/Edit/{id:guid}/ChooseProperty")]
    public async Task<IActionResult> ChooseProperty(
        Guid id,
        string? propertySearch,
        int propertyPage = 1,
        int propertyRows = 15,
        CancellationToken cancellationToken = default)
    {
        var model = await GetPropertyPickerAsync(id, propertySearch, propertyPage, propertyRows, cancellationToken);

        return model is null ? NotFound() : View(model);
    }

    [HttpPost("Inspections/CreateCustomer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCustomer(InspectionNewCustomerViewModel update, CancellationToken cancellationToken)
    {
        var renoCompanyId = _currentUserSession.RenoCompanyID;
        var customer = new Customer
        {
            RenoCompanyID = renoCompanyId,
            Active = true,
            CreatedDate = DateTime.UtcNow,
            LastEditDate = DateTime.UtcNow,
            GivenName = Clean(update.GivenName),
            FamilyName = Clean(update.FamilyName),
            PrimaryEmailAddress = Clean(update.PrimaryEmailAddress),
            PrimaryPhone = Clean(update.PrimaryPhone),
            MobilePhone = Clean(update.MobilePhone),
            Fax = Clean(update.Fax),
            CompanyName = Clean(update.CompanyName),
            Website = Clean(update.Website)
        };

        customer.DisplayName = BuildCustomerDisplayName(customer);
        customer.FullyQualifiedName = customer.DisplayName;

        var billAddress = BuildNewCustomerBillAddress(update.BillAddress, renoCompanyId);
        if (billAddress is not null)
        {
            customer.BillAddress = billAddress;
            customer.BillAddressId = billAddress.AddressId;
        }

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Json(new
        {
            customerId = customer.CustomerId,
            firstName = customer.GivenName,
            lastName = customer.FamilyName,
            companyName = customer.CompanyName,
            phone = customer.PrimaryPhone,
            email = customer.PrimaryEmailAddress,
            street1 = customer.BillAddress?.Street1 ?? string.Empty,
            street2 = customer.BillAddress?.Street2 ?? string.Empty,
            city = customer.BillAddress?.City ?? string.Empty,
            state = customer.BillAddress?.State ?? string.Empty,
            postalCode = customer.BillAddress?.PostalCode ?? string.Empty,
            notes = customer.Notes
        });
    }

    [HttpPost("Inspections/Edit/{id:guid}/ChooseProperty")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectProperty(Guid id, Guid propertyId, CancellationToken cancellationToken)
    {
        var renoCompanyId = _currentUserSession.RenoCompanyID;
        var inspection = await _dbContext.Inspections
            .ForCompany(renoCompanyId)
            .FirstOrDefaultAsync(item => item.InspectionId == id, cancellationToken);

        if (inspection?.CustomerId is null)
        {
            return NotFound();
        }

        var isLinkedToCustomer = await _dbContext.Properties
            .AsNoTracking()
            .ForCompany(renoCompanyId)
            .AnyAsync(property => property.PropertyId == propertyId
                && property.Customers.Any(customer => customer.CustomerId == inspection.CustomerId.Value), cancellationToken);

        if (!isLinkedToCustomer)
        {
            return NotFound();
        }

        inspection.PropertyId = propertyId;
        inspection.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Edit), new { id, activeTab = "property" });
    }

    [HttpPost("Inspections/Edit/{id:guid}/ChooseProperty/Add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProperty(
        Guid id,
        InspectionPropertyAddressEditViewModel newProperty,
        string? propertySearch,
        int propertyPage = 1,
        int propertyRows = 15,
        bool embedded = false,
        CancellationToken cancellationToken = default)
    {
        var renoCompanyId = _currentUserSession.RenoCompanyID;
        var inspection = await _dbContext.Inspections
            .AsNoTracking()
            .ForCompany(renoCompanyId)
            .FirstOrDefaultAsync(item => item.InspectionId == id, cancellationToken);

        if (inspection?.CustomerId is null)
        {
            return NotFound();
        }

        var customer = await _dbContext.Customers
            .ForCompany(renoCompanyId)
            .Include(item => item.Properties)
            .FirstOrDefaultAsync(item => item.CustomerId == inspection.CustomerId.Value, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        var property = new Property
        {
            RenoCompanyID = renoCompanyId,
            Address = new Address
            {
                RenoCompanyID = renoCompanyId,
                Street1 = Clean(newProperty.Street1),
                Street2 = Clean(newProperty.Street2),
                City = Clean(newProperty.City),
                State = Clean(newProperty.State),
                PostalCode = Clean(newProperty.PostalCode)
            }
        };

        customer.Properties.Add(property);
        _dbContext.Properties.Add(property);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(ChooseProperty), new
        {
            id,
            propertySearch,
            propertyPage,
            propertyRows,
            embedded
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid? id, InspectionEditViewModel update, CancellationToken cancellationToken)
    {
        if (id.HasValue)
        {
            if (string.IsNullOrWhiteSpace(update.Title))
            {
                ModelState.AddModelError(nameof(update.Title), "Title is required.");
                return View(await BuildInspectionEditViewModelAsync(id.Value, update, cancellationToken));
            }

            var customerMatchModel = await BuildCustomerMatchDialogModelIfNeededAsync(id.Value, update, cancellationToken);

            if (customerMatchModel is not null)
            {
                return View(customerMatchModel);
            }

            var saved = await SaveInspectionEditAsync(id.Value, update, cancellationToken);

            if (!saved)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Edit), new { id = id.Value, activeTab = NormalizeInspectionEditTab(update.ActiveTab) });
        }

        if (string.IsNullOrWhiteSpace(update.Title))
        {
            ModelState.AddModelError(nameof(update.Title), "Title is required.");
            return View(await BuildUnsavedInspectionEditViewModelAsync(update, cancellationToken));
        }

        var unsavedCustomerMatchModel = await BuildCustomerMatchDialogModelIfNeededAsync(null, update, cancellationToken);

        if (unsavedCustomerMatchModel is not null)
        {
            return View(unsavedCustomerMatchModel);
        }

        var renoCompanyId = _currentUserSession.RenoCompanyID;
        var now = DateTime.UtcNow;
        var propertyAddress = update.PropertyAddress ?? new InspectionPropertyAddressEditViewModel();
        var property = new Property
        {
            RenoCompanyID = renoCompanyId,
            Address = new Address
            {
                RenoCompanyID = renoCompanyId,
                Street1 = Clean(propertyAddress.Street1),
                Street2 = Clean(propertyAddress.Street2),
                City = Clean(propertyAddress.City),
                State = Clean(propertyAddress.State),
                PostalCode = Clean(propertyAddress.PostalCode)
            }
        };
        var customerId = await GetOrCreateInspectionCustomerIdAsync(update.Customer, update.ForceNewCustomer, renoCompanyId, cancellationToken);
        var inspection = new Inspection
        {
            RenoCompanyID = renoCompanyId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Title = Clean(update.Title),
            InspectionDate = NormalizeDate(update.InspectionDate),
            InspectorName = Clean(update.InspectorName),
            GeneralNotes = Clean(update.GeneralNotes),
            PropertyId = property.PropertyId,
            Property = property,
            CustomerId = customerId
        };

        _dbContext.Inspections.Add(inspection);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await LinkCustomerPropertyAsync(customerId, property.PropertyId, renoCompanyId, cancellationToken);

        return RedirectToAction(nameof(Edit), new { id = inspection.InspectionId });
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
            InspectionId = inspection.InspectionId,
            CreateDate = DateTime.UtcNow,
            DocumentTypeId = DocumentType.InspectionId,
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
                    .Where(area => area.BuildingId == building.BuildingId)
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
            inspection.InspectionId,
            inspection.Title,
            inspection.InspectionDate,
            inspection.InspectorName,
            GetPropertyAddress(inspection.Property),
            GetCustomerName(inspection.Customer));
    }

    private string BuildMileageStaticMapImageUrl(IEnumerable<MileageTrackingWaypoint> waypoints)
    {
        var apiKey = _configuration["Geoapify:ApiKey"]
            ?? _configuration["GEOAPIFY_API_KEY"]
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return string.Empty;
        }

        var orderedMapWaypoints = waypoints
            .OrderBy(waypoint => waypoint.WaypointTime)
            .Select((waypoint, index) => new
            {
                Waypoint = waypoint,
                Number = index + 1
            })
            .Where(item => item.Number != 1 || item.Waypoint.CumulativeMiles > 0)
            .Select(item => new
            {
                Coordinate = TryParseGpsCoordinates(item.Waypoint.GpsCoordinates),
                item.Number
            })
            .Where(item => item.Coordinate.HasValue)
            .Select(item => new MapWaypoint(item.Coordinate!.Value, item.Number))
            .ToList();
        var markerWaypoints = orderedMapWaypoints
            .Where(waypoint => waypoint.Number != 1)
            .ToList();

        if (markerWaypoints.Count == 0)
        {
            return string.Empty;
        }

        var query = new List<string>
        {
            "style=osm-bright",
            "width=900",
            "height=360",
            "scaleFactor=2",
            "format=png"
        };

        var markers = markerWaypoints
            .GroupBy(waypoint => waypoint.Coordinate)
            .Select((group, index) =>
            {
                var coordinate = group.Key;
                var pinColor = index == 0 ? "%23198754" : "%23d92d20";
                var label = string.Join(",", group.Select(waypoint => waypoint.Number));
                var contentsize = label.Length > 3 ? 10 : 14;

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "lonlat:{0:0.##############},{1:0.##############};type:material;color:{2};size:24;text:{3};contentsize:{4};contentcolor:%23ffffff;whitecircle:no",
                    coordinate.Longitude,
                    coordinate.Latitude,
                    pinColor,
                    Uri.EscapeDataString(label),
                    contentsize);
            });

        query.Add($"marker={string.Join('|', markers)}");

        if (orderedMapWaypoints.Count > 1)
        {
            var lineCoordinates = string.Join(
                ',',
                orderedMapWaypoints.Select(waypoint =>
                {
                    var coordinate = waypoint.Coordinate;

                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:0.##############},{1:0.##############}",
                        coordinate.Longitude,
                        coordinate.Latitude);
                }));

            query.Add($"geometry={Uri.EscapeDataString($"polyline:{lineCoordinates};linecolor:#0d6efd;linewidth:4")}");
        }

        query.Add($"apiKey={Uri.EscapeDataString(apiKey)}");

        return $"https://maps.geoapify.com/v1/staticmap?{string.Join('&', query)}";
    }

    private static MapCoordinate? TryParseGpsCoordinates(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Regex.Match(
            value,
            @"(?<latitude>-?\d+(?:\.\d+)?)\s*,\s*(?<longitude>-?\d+(?:\.\d+)?)",
            RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return null;
        }

        if (!double.TryParse(match.Groups["latitude"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(match.Groups["longitude"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
            || latitude is < -90 or > 90
            || longitude is < -180 or > 180)
        {
            return null;
        }

        return new MapCoordinate(latitude, longitude);
    }

    private readonly record struct MapCoordinate(double Latitude, double Longitude);

    private readonly record struct MapWaypoint(MapCoordinate Coordinate, int Number);

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
                Id = inspector.InspectorId,
                FullName = GetInspectorFullName(inspector),
                Email = inspector.Email,
                Phone = inspector.Phone,
                HourlyRate = inspector.HourlyRate,
                IsDefault = inspector.IsDefault
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

    private async Task<InspectionCustomerPickerViewModel> GetCustomerPickerAsync(
        string? search,
        int page,
        int rows,
        bool openOnLoad,
        CancellationToken cancellationToken)
    {
        var allowedRows = new[] { 5, 10, 15, 25, 50, 100 };
        var pageSize = allowedRows.Contains(rows) ? rows : 15;
        var normalizedSearch = (search ?? string.Empty).Trim();
        var query = _dbContext.Customers
            .AsNoTracking()
            .Include(customer => customer.BillAddress)
            .ForCompany(_currentUserSession.RenoCompanyID);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(customer =>
                EF.Functions.ILike(customer.DisplayName, pattern)
                || EF.Functions.ILike(customer.CompanyName, pattern)
                || EF.Functions.ILike(customer.GivenName, pattern)
                || EF.Functions.ILike(customer.FamilyName, pattern)
                || EF.Functions.ILike(customer.PrimaryPhone, pattern));
        }

        var totalCustomers = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCustomers / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var customers = await query
            .OrderBy(customer => customer.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(customer => new InspectionCustomerPickerItemViewModel
            {
                CustomerId = customer.CustomerId,
                FirstName = customer.GivenName,
                LastName = customer.FamilyName,
                CompanyName = customer.CompanyName,
                Phone = customer.PrimaryPhone,
                Email = customer.PrimaryEmailAddress,
                Street1 = customer.BillAddress == null ? string.Empty : customer.BillAddress.Street1,
                Street2 = customer.BillAddress == null ? string.Empty : customer.BillAddress.Street2,
                City = customer.BillAddress == null ? string.Empty : customer.BillAddress.City,
                State = customer.BillAddress == null ? string.Empty : customer.BillAddress.State,
                PostalCode = customer.BillAddress == null ? string.Empty : customer.BillAddress.PostalCode,
                Notes = customer.Notes
            })
            .ToListAsync(cancellationToken);

        return new InspectionCustomerPickerViewModel
        {
            Customers = customers,
            Page = page,
            PageSize = pageSize,
            TotalCustomers = totalCustomers,
            TotalPages = totalPages,
            Search = normalizedSearch,
            OpenOnLoad = openOnLoad
        };
    }

    private async Task<InspectionPropertyPickerViewModel?> GetPropertyPickerAsync(
        Guid inspectionId,
        string? search,
        int page,
        int rows,
        CancellationToken cancellationToken)
    {
        var renoCompanyId = _currentUserSession.RenoCompanyID;
        var inspection = await _dbContext.Inspections
            .AsNoTracking()
            .ForCompany(renoCompanyId)
            .FirstOrDefaultAsync(item => item.InspectionId == inspectionId, cancellationToken);

        if (inspection?.CustomerId is null)
        {
            return null;
        }

        var customerExists = await _dbContext.Customers
            .AsNoTracking()
            .ForCompany(renoCompanyId)
            .AnyAsync(customer => customer.CustomerId == inspection.CustomerId.Value, cancellationToken);

        if (!customerExists)
        {
            return null;
        }

        var allowedRows = new[] { 5, 10, 15, 25, 50, 100 };
        var pageSize = allowedRows.Contains(rows) ? rows : 15;
        var normalizedSearch = (search ?? string.Empty).Trim();
        var query = _dbContext.Properties
            .AsNoTracking()
            .ForCompany(renoCompanyId)
            .Include(property => property.Address)
            .Where(property => property.Customers.Any(customer => customer.CustomerId == inspection.CustomerId.Value));

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(property =>
                EF.Functions.ILike(property.Name, pattern)
                || EF.Functions.ILike(property.Address.Street1, pattern)
                || EF.Functions.ILike(property.Address.Street2, pattern)
                || EF.Functions.ILike(property.Address.City, pattern)
                || EF.Functions.ILike(property.Address.State, pattern)
                || EF.Functions.ILike(property.Address.PostalCode, pattern));
        }

        var totalProperties = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalProperties / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var properties = await query
            .OrderBy(property => property.Address.Street1)
            .ThenBy(property => property.Address.City)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(property => new InspectionPropertyPickerItemViewModel
            {
                PropertyId = property.PropertyId,
                PropertyName = property.Name,
                Street1 = property.Address.Street1,
                Street2 = property.Address.Street2,
                City = property.Address.City,
                State = property.Address.State,
                PostalCode = property.Address.PostalCode
            })
            .ToListAsync(cancellationToken);

        return new InspectionPropertyPickerViewModel
        {
            InspectionId = inspection.InspectionId,
            CustomerId = inspection.CustomerId.Value,
            SelectedPropertyId = inspection.PropertyId,
            Properties = properties,
            Page = page,
            PageSize = pageSize,
            TotalProperties = totalProperties,
            TotalPages = totalPages,
            Search = normalizedSearch
        };
    }

    private async Task LinkCustomerPropertyAsync(Guid? customerId, Guid propertyId, Guid renoCompanyId, CancellationToken cancellationToken)
    {
        if (!customerId.HasValue)
        {
            return;
        }

        var customer = await _dbContext.Customers
            .ForCompany(renoCompanyId)
            .Include(item => item.Properties)
            .FirstOrDefaultAsync(item => item.CustomerId == customerId.Value, cancellationToken);

        if (customer is null || customer.Properties.Any(property => property.PropertyId == propertyId))
        {
            return;
        }

        var property = await _dbContext.Properties
            .ForCompany(renoCompanyId)
            .FirstOrDefaultAsync(item => item.PropertyId == propertyId, cancellationToken);

        if (property is null)
        {
            return;
        }

        customer.Properties.Add(property);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Address? BuildNewCustomerBillAddress(InspectionNewCustomerAddressViewModel? update, Guid renoCompanyId)
    {
        update ??= new InspectionNewCustomerAddressViewModel();

        if (string.IsNullOrWhiteSpace(update.Street1)
            && string.IsNullOrWhiteSpace(update.Street2)
            && string.IsNullOrWhiteSpace(update.Street3)
            && string.IsNullOrWhiteSpace(update.City)
            && string.IsNullOrWhiteSpace(update.State)
            && string.IsNullOrWhiteSpace(update.PostalCode)
            && string.IsNullOrWhiteSpace(update.Country))
        {
            return null;
        }

        return new Address
        {
            RenoCompanyID = renoCompanyId,
            Street1 = Clean(update.Street1),
            Street2 = Clean(update.Street2),
            Street3 = Clean(update.Street3),
            City = Clean(update.City),
            State = Clean(update.State),
            PostalCode = Clean(update.PostalCode),
            Country = Clean(update.Country)
        };
    }

    private async Task<Guid?> GetValidCustomerIdAsync(Guid? customerId, CancellationToken cancellationToken)
    {
        if (!customerId.HasValue)
        {
            return null;
        }

        var exists = await _dbContext.Customers
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .AnyAsync(customer => customer.CustomerId == customerId.Value, cancellationToken);

        return exists ? customerId.Value : null;
    }

    private async Task<Guid?> GetOrCreateInspectionCustomerIdAsync(
        InspectionCustomerEditViewModel? update,
        bool forceNewCustomer,
        Guid renoCompanyId,
        CancellationToken cancellationToken)
    {
        if (update is null || IsCustomerEmpty(update))
        {
            return null;
        }

        if (update.CustomerId.HasValue && !forceNewCustomer)
        {
            return await GetValidCustomerIdAsync(update.CustomerId, cancellationToken);
        }

        var customer = new Customer
        {
            RenoCompanyID = renoCompanyId,
            Active = true,
            CreatedDate = DateTime.UtcNow
        };

        ApplyCustomerUpdate(customer, update, renoCompanyId);
        _dbContext.Customers.Add(customer);

        return customer.CustomerId;
    }

    private async Task<InspectionEditViewModel?> BuildCustomerMatchDialogModelIfNeededAsync(
        Guid? inspectionId,
        InspectionEditViewModel update,
        CancellationToken cancellationToken)
    {
        if (update.ForceNewCustomer || update.Customer is null || IsCustomerEmpty(update.Customer) || update.Customer.CustomerId.HasValue)
        {
            return null;
        }

        var matches = await FindCustomerMatchesAsync(update.Customer, cancellationToken);
        var exactMatch = matches.FirstOrDefault(match => match.IsExact);

        if (exactMatch is not null)
        {
            update.Customer.CustomerId = exactMatch.Customer.CustomerId;
            return null;
        }

        var closeMatches = matches
            .Where(match => match.IsClose)
            .Select(match => ToCustomerMatchViewModel(match.Customer))
            .ToList();

        if (closeMatches.Count == 0)
        {
            return null;
        }

        return inspectionId.HasValue
            ? await BuildInspectionEditViewModelAsync(inspectionId.Value, update, cancellationToken, true, closeMatches)
            : await BuildUnsavedInspectionEditViewModelAsync(update, cancellationToken, true, closeMatches);
    }

    private async Task<IReadOnlyList<CustomerMatchCandidate>> FindCustomerMatchesAsync(
        InspectionCustomerEditViewModel enteredCustomer,
        CancellationToken cancellationToken)
    {
        var renoCompanyId = _currentUserSession.RenoCompanyID;
        var firstName = Clean(enteredCustomer.FirstName);
        var lastName = Clean(enteredCustomer.LastName);
        var companyName = Clean(enteredCustomer.CompanyName);
        var phone = Clean(enteredCustomer.Phone);
        var email = Clean(enteredCustomer.Email);
        var street1 = Clean(enteredCustomer.Street1);
        var query = _dbContext.Customers
            .AsNoTracking()
            .Include(customer => customer.BillAddress)
            .ForCompany(renoCompanyId);

        query = query.Where(customer =>
            (!string.IsNullOrWhiteSpace(phone) && EF.Functions.ILike(customer.PrimaryPhone, $"%{phone}%"))
            || (!string.IsNullOrWhiteSpace(email) && EF.Functions.ILike(customer.PrimaryEmailAddress, $"%{email}%"))
            || (!string.IsNullOrWhiteSpace(firstName) && EF.Functions.ILike(customer.GivenName, $"%{firstName}%"))
            || (!string.IsNullOrWhiteSpace(lastName) && EF.Functions.ILike(customer.FamilyName, $"%{lastName}%"))
            || (!string.IsNullOrWhiteSpace(companyName) && EF.Functions.ILike(customer.CompanyName, $"%{companyName}%"))
            || (!string.IsNullOrWhiteSpace(street1) && customer.BillAddress != null && EF.Functions.ILike(customer.BillAddress.Street1, $"%{street1}%")));

        var candidates = await query
            .OrderBy(customer => customer.DisplayName)
            .Take(50)
            .ToListAsync(cancellationToken);

        return candidates
            .Select(customer => new CustomerMatchCandidate(
                customer,
                IsExactCustomerMatch(enteredCustomer, customer),
                GetCustomerMatchScore(enteredCustomer, customer) >= 3))
            .Where(match => match.IsExact || match.IsClose)
            .ToList();
    }

    private static bool IsExactCustomerMatch(InspectionCustomerEditViewModel enteredCustomer, Customer customer)
    {
        var significantComparisons = new[]
        {
            IsEnteredValueExact(enteredCustomer.FirstName, customer.GivenName),
            IsEnteredValueExact(enteredCustomer.LastName, customer.FamilyName),
            IsEnteredValueExact(enteredCustomer.Phone, customer.PrimaryPhone),
            IsEnteredValueExact(enteredCustomer.Email, customer.PrimaryEmailAddress),
            IsEnteredValueExact(enteredCustomer.Street1, customer.BillAddress?.Street1)
        };

        return significantComparisons.Count(value => value) >= 3
            && IsEnteredValueExact(enteredCustomer.FirstName, customer.GivenName)
            && IsEnteredValueExact(enteredCustomer.LastName, customer.FamilyName);
    }

    private static int GetCustomerMatchScore(InspectionCustomerEditViewModel enteredCustomer, Customer customer)
    {
        var score = 0;

        score += IsEnteredValueExact(enteredCustomer.Phone, customer.PrimaryPhone) ? 3 : 0;
        score += IsEnteredValueExact(enteredCustomer.Email, customer.PrimaryEmailAddress) ? 3 : 0;
        score += IsEnteredValueExact(enteredCustomer.FirstName, customer.GivenName) ? 1 : 0;
        score += IsEnteredValueExact(enteredCustomer.LastName, customer.FamilyName) ? 2 : 0;
        score += IsEnteredValueExact(enteredCustomer.Street1, customer.BillAddress?.Street1) ? 2 : 0;
        score += IsEnteredValueExact(enteredCustomer.CompanyName, customer.CompanyName) ? 1 : 0;

        return score;
    }

    private static bool IsEnteredValueExact(string? enteredValue, string? existingValue)
    {
        var entered = NormalizeCustomerMatchValue(enteredValue);

        return !string.IsNullOrWhiteSpace(entered)
            && entered == NormalizeCustomerMatchValue(existingValue);
    }

    private static string NormalizeCustomerMatchValue(string? value)
    {
        return new string((value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static InspectionCustomerMatchViewModel ToCustomerMatchViewModel(Customer customer)
    {
        return new InspectionCustomerMatchViewModel
        {
            CustomerId = customer.CustomerId,
            FirstName = customer.GivenName,
            LastName = customer.FamilyName,
            CompanyName = customer.CompanyName,
            Phone = customer.PrimaryPhone,
            Email = customer.PrimaryEmailAddress,
            Street1 = customer.BillAddress?.Street1 ?? string.Empty,
            City = customer.BillAddress?.City ?? string.Empty,
            State = customer.BillAddress?.State ?? string.Empty,
            PostalCode = customer.BillAddress?.PostalCode ?? string.Empty
        };
    }

    private static void ApplyCustomerUpdate(Customer customer, InspectionCustomerEditViewModel update, Guid renoCompanyId)
    {
        customer.GivenName = Clean(update.FirstName);
        customer.FamilyName = Clean(update.LastName);
        customer.CompanyName = Clean(update.CompanyName);
        customer.PrimaryPhone = Clean(update.Phone);
        customer.PrimaryEmailAddress = Clean(update.Email);
        customer.Notes = Clean(update.Notes);
        customer.DisplayName = BuildCustomerDisplayName(customer);
        customer.FullyQualifiedName = customer.DisplayName;
        customer.LastEditDate = DateTime.UtcNow;
        UpdateCustomerBillAddress(customer, update, renoCompanyId);
    }

    private async Task<bool> SaveInspectionEditAsync(Guid id, InspectionEditViewModel update, CancellationToken cancellationToken)
    {
        var renoCompanyId = _currentUserSession.RenoCompanyID;
        var inspection = await _dbContext.Inspections
            .ForCompany(renoCompanyId)
            .Include(item => item.Property)
                .ThenInclude(property => property.Address)
            .Include(item => item.Customer)
                .ThenInclude(customer => customer!.BillAddress)
            .Include(item => item.Property)
                .ThenInclude(property => property.Areas)
                    .ThenInclude(area => area.AreaNotes)
                        .ThenInclude(note => note.EstimateItems)
            .Include(item => item.Property)
                .ThenInclude(property => property.Areas)
                    .ThenInclude(area => area.AreaNotes)
                        .ThenInclude(note => note.Photos)
            .FirstOrDefaultAsync(item => item.InspectionId == id, cancellationToken);

        if (inspection is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        inspection.Title = Clean(update.Title);
        inspection.InspectionDate = NormalizeDate(update.InspectionDate);
        inspection.InspectorName = Clean(update.InspectorName);
        inspection.GeneralNotes = Clean(update.GeneralNotes);
        inspection.UpdatedAtUtc = now;

        UpdatePropertyAddress(inspection.Property, update.PropertyAddress, renoCompanyId);
        await UpdateInspectionCustomerAsync(inspection, update.Customer, renoCompanyId, cancellationToken);
        UpdateInspectionAreas(inspection.Property.Areas, update.Buildings, renoCompanyId, now);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await LinkCustomerPropertyAsync(inspection.CustomerId, inspection.PropertyId, renoCompanyId, cancellationToken);
        return true;
    }

    private static void UpdatePropertyAddress(Property property, InspectionPropertyAddressEditViewModel? update, Guid renoCompanyId)
    {
        update ??= new InspectionPropertyAddressEditViewModel();
        property.Address ??= new Address
        {
            RenoCompanyID = renoCompanyId,
            PropertyId = property.PropertyId
        };

        property.Address.RenoCompanyID = renoCompanyId;
        property.Address.PropertyId = property.PropertyId;
        property.Address.Street1 = Clean(update.Street1);
        property.Address.Street2 = Clean(update.Street2);
        property.Address.City = Clean(update.City);
        property.Address.State = Clean(update.State);
        property.Address.PostalCode = Clean(update.PostalCode);
    }

    private async Task UpdateInspectionCustomerAsync(
        Inspection inspection,
        InspectionCustomerEditViewModel? update,
        Guid renoCompanyId,
        CancellationToken cancellationToken)
    {
        if (update is null || IsCustomerEmpty(update))
        {
            inspection.CustomerId = null;
            inspection.Customer = null;
            return;
        }

        Customer? customer = null;

        if (update.CustomerId.HasValue && !inspection.CustomerId.HasValue && !inspection.CustomerId.Equals(update.CustomerId))
        {
            customer = await _dbContext.Customers
                .Include(item => item.BillAddress)
                .ForCompany(renoCompanyId)
                .FirstOrDefaultAsync(item => item.CustomerId == update.CustomerId.Value, cancellationToken);
        }
        else if (update.CustomerId.HasValue)
        {
            customer = await _dbContext.Customers
                .Include(item => item.BillAddress)
                .ForCompany(renoCompanyId)
                .FirstOrDefaultAsync(item => item.CustomerId == update.CustomerId.Value, cancellationToken);
        }

        if (customer is null)
        {
            customer = new Customer
            {
                RenoCompanyID = renoCompanyId,
                Active = true,
                CreatedDate = DateTime.UtcNow
            };
            _dbContext.Customers.Add(customer);
        }

        customer.GivenName = Clean(update.FirstName);
        customer.FamilyName = Clean(update.LastName);
        customer.CompanyName = Clean(update.CompanyName);
        customer.PrimaryPhone = Clean(update.Phone);
        customer.PrimaryEmailAddress = Clean(update.Email);
        customer.Notes = Clean(update.Notes);
        customer.DisplayName = BuildCustomerDisplayName(customer);
        customer.FullyQualifiedName = customer.DisplayName;
        customer.LastEditDate = DateTime.UtcNow;
        UpdateCustomerBillAddress(customer, update, renoCompanyId);

        inspection.Customer = customer;
        inspection.CustomerId = customer.CustomerId;
    }

    private static void UpdateCustomerBillAddress(Customer customer, InspectionCustomerEditViewModel update, Guid renoCompanyId)
    {
        if (IsCustomerAddressEmpty(update))
        {
            customer.BillAddress = null;
            customer.BillAddressId = null;
            return;
        }

        customer.BillAddress ??= new Address
        {
            RenoCompanyID = renoCompanyId
        };
        customer.BillAddress.RenoCompanyID = renoCompanyId;
        customer.BillAddress.Street1 = Clean(update.Street1);
        customer.BillAddress.Street2 = Clean(update.Street2);
        customer.BillAddress.City = Clean(update.City);
        customer.BillAddress.State = Clean(update.State);
        customer.BillAddress.PostalCode = Clean(update.PostalCode);
    }

    private void UpdateInspectionAreas(
        IEnumerable<InspectionArea> areas,
        IReadOnlyList<InspectionBuildingEditViewModel> postedBuildings,
        Guid renoCompanyId,
        DateTime now)
    {
        var postedAreas = postedBuildings
            .SelectMany(building => building.Areas)
            .GroupBy(area => area.Id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var area in areas)
        {
            if (!postedAreas.TryGetValue(area.InspectionAreaId, out var postedArea))
            {
                continue;
            }

            area.OverallRating = postedArea.OverallRating;

            var postedNotes = postedArea.Notes
                .GroupBy(note => note.Id)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var note in area.AreaNotes)
            {
                if (!postedNotes.TryGetValue(note.InspectionAreaNoteId, out var postedNote))
                {
                    continue;
                }

                UpdateEstimateItems(note, postedNote.EstimateItems, renoCompanyId, now);
                UpdateCroppedPhotos(note, postedNote.Photos, now);
            }
        }
    }

    private void UpdateEstimateItems(
        InspectionAreaNote note,
        IReadOnlyList<InspectionAreaNoteEstimateItemEditViewModel> postedItems,
        Guid renoCompanyId,
        DateTime now)
    {
        var postedById = postedItems
            .Where(item => item.Id != Guid.Empty)
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var removedItems = note.EstimateItems
            .Where(item => !postedById.ContainsKey(item.InspectionAreaNoteEstimateItemId))
            .ToList();

        _dbContext.InspectionAreaNoteEstimateItems.RemoveRange(removedItems);

        foreach (var postedItem in postedItems.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            var item = note.EstimateItems.FirstOrDefault(existing => existing.InspectionAreaNoteEstimateItemId == postedItem.Id);

            if (item is null)
            {
                item = new InspectionAreaNoteEstimateItem
                {
                    InspectionAreaNoteEstimateItemId = postedItem.Id == Guid.Empty ? Guid.NewGuid() : postedItem.Id,
                    RenoCompanyID = renoCompanyId,
                    PropertyId = note.PropertyId,
                    BuildingId = note.BuildingId,
                    AreaId = note.AreaId,
                    AreaNoteId = note.InspectionAreaNoteId,
                    CreatedAtUtc = now
                };
                note.EstimateItems.Add(item);
            }

            item.UpdatedAtUtc = now;
            item.Name = Clean(postedItem.Name);
            item.Cost = postedItem.Cost;
            item.Hours = postedItem.Hours;
        }
    }

    private static void UpdateCroppedPhotos(
        InspectionAreaNote note,
        IReadOnlyList<InspectionAreaNotePhotoEditViewModel> postedPhotos,
        DateTime now)
    {
        var postedById = postedPhotos
            .Where(photo => photo.Id != Guid.Empty && !string.IsNullOrWhiteSpace(photo.CroppedDataUrl))
            .GroupBy(photo => photo.Id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var photo in note.Photos)
        {
            if (!postedById.TryGetValue(photo.InspectionAreaNotePhotoId, out var postedPhoto)
                || !TryParseDataUrl(postedPhoto.CroppedDataUrl, out var contentType, out var data))
            {
                continue;
            }

            photo.ContentType = contentType;
            photo.Data = data;
        }
    }

    private static bool TryParseDataUrl(string dataUrl, out string contentType, out byte[] data)
    {
        contentType = string.Empty;
        data = [];

        const string marker = ";base64,";
        var markerIndex = dataUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (!dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || markerIndex < 0)
        {
            return false;
        }

        contentType = dataUrl[5..markerIndex];
        var base64 = dataUrl[(markerIndex + marker.Length)..];

        try
        {
            data = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            data = [];
            return false;
        }
    }

    private static bool IsCustomerEmpty(InspectionCustomerEditViewModel customer)
    {
        return string.IsNullOrWhiteSpace(customer.FirstName)
            && string.IsNullOrWhiteSpace(customer.LastName)
            && string.IsNullOrWhiteSpace(customer.CompanyName)
            && string.IsNullOrWhiteSpace(customer.Phone)
            && string.IsNullOrWhiteSpace(customer.Email)
            && string.IsNullOrWhiteSpace(customer.Notes)
            && IsCustomerAddressEmpty(customer);
    }

    private static bool IsCustomerAddressEmpty(InspectionCustomerEditViewModel customer)
    {
        return string.IsNullOrWhiteSpace(customer.Street1)
            && string.IsNullOrWhiteSpace(customer.Street2)
            && string.IsNullOrWhiteSpace(customer.City)
            && string.IsNullOrWhiteSpace(customer.State)
            && string.IsNullOrWhiteSpace(customer.PostalCode);
    }

    private static string BuildCustomerDisplayName(Customer customer)
    {
        var name = $"{customer.GivenName} {customer.FamilyName}".Trim();

        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return string.IsNullOrWhiteSpace(customer.CompanyName) ? "Unnamed Customer" : customer.CompanyName;
    }

    private sealed record CustomerMatchCandidate(Customer Customer, bool IsExact, bool IsClose);

    private static string Clean(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    private async Task<InspectionEditViewModel> BuildUnsavedInspectionEditViewModelAsync(
        InspectionEditViewModel update,
        CancellationToken cancellationToken,
        bool showCustomerMatchDialog = false,
        IReadOnlyList<InspectionCustomerMatchViewModel>? customerMatches = null)
    {
        return new InspectionEditViewModel
        {
            Title = update.Title,
            InspectionDate = update.InspectionDate,
            InspectorName = update.InspectorName,
            GeneralNotes = update.GeneralNotes,
            PropertyId = update.PropertyId,
            PropertyAddress = update.PropertyAddress,
            Customer = update.Customer,
            Buildings = update.Buildings,
            Inspectors = await GetInspectorPickerItemsAsync(cancellationToken),
            Parts = await GetPartPickerItemsAsync(cancellationToken),
            CustomerPicker = await GetCustomerPickerAsync(null, 1, 15, false, cancellationToken),
            ForceNewCustomer = update.ForceNewCustomer,
            ShowCustomerMatchDialog = showCustomerMatchDialog,
            CustomerMatches = customerMatches ?? [],
            ActiveTab = NormalizeInspectionEditTab(update.ActiveTab)
        };
    }

    private async Task<InspectionEditViewModel> BuildInspectionEditViewModelAsync(
        Guid id,
        InspectionEditViewModel update,
        CancellationToken cancellationToken,
        bool showCustomerMatchDialog = false,
        IReadOnlyList<InspectionCustomerMatchViewModel>? customerMatches = null)
    {
        return new InspectionEditViewModel
        {
            Id = id,
            Title = update.Title,
            InspectionDate = update.InspectionDate,
            InspectorName = update.InspectorName,
            GeneralNotes = update.GeneralNotes,
            PropertyId = update.PropertyId,
            PropertyAddress = update.PropertyAddress ?? new InspectionPropertyAddressEditViewModel(),
            Customer = update.Customer ?? new InspectionCustomerEditViewModel(),
            Buildings = update.Buildings,
            Inspectors = await GetInspectorPickerItemsAsync(cancellationToken),
            Parts = await GetPartPickerItemsAsync(cancellationToken),
            CustomerPicker = await GetCustomerPickerAsync(null, 1, 15, false, cancellationToken),
            ForceNewCustomer = update.ForceNewCustomer,
            ShowCustomerMatchDialog = showCustomerMatchDialog,
            CustomerMatches = customerMatches ?? [],
            ActiveTab = NormalizeInspectionEditTab(update.ActiveTab)
        };
    }

    private static string NormalizeInspectionEditTab(string? activeTab)
    {
        return activeTab is "customer" or "property" or "buildings" ? activeTab : "general";
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
                Id = building.BuildingId,
                Name = building.Name,
                BuildingTypeName = string.IsNullOrWhiteSpace(building.BuildingType?.Name)
                    ? "No building type"
                    : building.BuildingType.Name,
                Areas = property.Areas
                    .Where(area => area.BuildingId == building.BuildingId)
                    .OrderBy(area => area.SortOrder)
                    .ThenBy(area => area.DisplayName)
                    .Select(area => new InspectionAreaEditViewModel
                    {
                        Id = area.InspectionAreaId,
                        DisplayName = area.DisplayName,
                        AreaTypeName = string.IsNullOrWhiteSpace(area.AreaType?.Name)
                            ? "No area type"
                            : area.AreaType.Name,
                        OverallRating = area.OverallRating,
                        Notes = area.AreaNotes
                            .OrderBy(note => note.CreatedAtUtc)
                            .Select(note => new InspectionAreaNoteEditViewModel
                            {
                                Id = note.InspectionAreaNoteId,
                                Text = note.Text,
                                EstimateCost = note.EstimateItems.Sum(item => item.Cost),
                                EstimateHours = note.EstimateItems.Sum(item => item.Hours),
                                EstimateItems = note.EstimateItems
                                    .OrderBy(item => item.Name)
                                    .Select(item => new InspectionAreaNoteEstimateItemEditViewModel
                                    {
                                        Id = item.InspectionAreaNoteEstimateItemId,
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
            Id = photo.InspectionAreaNotePhotoId,
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

