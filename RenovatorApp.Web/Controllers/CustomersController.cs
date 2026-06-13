using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.Services;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

public sealed class CustomersController : Controller
{
    private const int PageSize = 10;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private readonly RenovatorAppDbContext _dbContext;
    private readonly CurrentUserSession _currentUserSession;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public CustomersController(
        RenovatorAppDbContext dbContext,
        CurrentUserSession currentUserSession,
        IWebHostEnvironment webHostEnvironment)
    {
        _dbContext = dbContext;
        _currentUserSession = currentUserSession;
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task<IActionResult> Index(string? search, bool showInactive = false, int page = 1, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = (search ?? string.Empty).Trim();
        var lastSyncDateUtc = await GetLastQuickBooksSyncDateUtcAsync(cancellationToken);
        var query = _dbContext.Customers.AsNoTracking().ForCompany(_currentUserSession.RenoCompanyID);

        if (!showInactive)
        {
            query = query.Where(customer => customer.Active);
        }

        if (normalizedSearch.Length >= 2)
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(customer =>
                EF.Functions.ILike(customer.DisplayName, pattern)
                || EF.Functions.ILike(customer.CompanyName, pattern)
                || EF.Functions.ILike(customer.GivenName, pattern)
                || EF.Functions.ILike(customer.FamilyName, pattern)
                || EF.Functions.ILike(customer.PrimaryEmailAddress, pattern)
                || EF.Functions.ILike(customer.PrimaryPhone, pattern));
        }

        var totalCustomers = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCustomers / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var customers = await query
            .OrderBy(customer => customer.DisplayName)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(customer => ToRowViewModel(customer))
            .ToListAsync(cancellationToken);

        return View(new CustomersIndexViewModel
        {
            Customers = customers,
            Page = page,
            PageSize = PageSize,
            TotalCustomers = totalCustomers,
            TotalPages = totalPages,
            Search = normalizedSearch,
            ShowInactive = showInactive,
            LastQuickBooksSyncDateUtc = lastSyncDateUtc,
            StatusMessage = TempData["CustomersStatus"] as string ?? string.Empty
        });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .Include(item => item.BillAddress)
            .Include(item => item.Documents)
                .ThenInclude(document => document.DocumentType)
            .Include(item => item.Properties)
                .ThenInclude(property => property.Address)
            .Include(item => item.ShipAddress)
            .FirstOrDefaultAsync(item => item.CustomerId == id && item.RenoCompanyID == _currentUserSession.RenoCompanyID, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        return View(ToDetailViewModel(customer));
    }

    public async Task<IActionResult> New(bool embedded = false, CancellationToken cancellationToken = default)
    {
        var defaultState = await GetDefaultStateAsync(cancellationToken);

        return View(new CustomerDetailViewModel
        {
            CustomerId = Guid.NewGuid(),
            Active = "Yes",
            Taxable = "No",
            Job = "No",
            BillWithParent = "No",
            CreatedDate = DateTime.UtcNow,
            BillAddress = new CustomerAddressViewModel
            {
                State = defaultState
            },
            ShipAddress = new CustomerAddressViewModel
            {
                State = defaultState
            },
            StateOptions = StateOptionsProvider.GetStates()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(CustomerDetailUpdateViewModel update, bool embedded = false, CancellationToken cancellationToken = default)
    {
        update.BillAddress ??= new CustomerAddressUpdateViewModel();
        if (update.IncludeBillingAddressAsCustomerProperty && IsAddressEmpty(update.BillAddress))
        {
            ModelState.AddModelError(
                nameof(update.IncludeBillingAddressAsCustomerProperty),
                "Enter a billing address before adding it as a customer property.");
        }

        if (!ModelState.IsValid)
        {
            return View(ToNewDetailViewModel(update));
        }

        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            CustomerId = update.CustomerId == Guid.Empty ? Guid.NewGuid() : update.CustomerId,
            RenoCompanyID = _currentUserSession.RenoCompanyID,
            Active = true,
            CreatedDate = now,
            LastEditDate = now
        };

        ApplyUpdate(customer, update);
        EnsureCustomerDisplayName(customer);
        TrackCustomerAddresses(customer);

        if (update.IncludeBillingAddressAsCustomerProperty && customer.BillAddress is not null)
        {
            AddBillingAddressProperty(customer);
        }

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (embedded)
        {
            ViewData["CustomerSaved"] = true;
            var model = ToDetailViewModel(customer);
            model.StateOptions = StateOptionsProvider.GetStates();
            return View(model);
        }

        TempData["CustomersStatus"] = "Customer added.";
        return RedirectToAction(nameof(Details), new { id = customer.CustomerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Details(Guid id, CustomerDetailUpdateViewModel update, CancellationToken cancellationToken)
    {
        if (id != update.CustomerId)
        {
            return BadRequest();
        }

        var customer = await _dbContext.Customers
            .Include(item => item.BillAddress)
            .Include(item => item.Documents)
                .ThenInclude(document => document.DocumentType)
            .Include(item => item.Properties)
                .ThenInclude(property => property.Address)
            .Include(item => item.ShipAddress)
            .FirstOrDefaultAsync(item => item.CustomerId == id && item.RenoCompanyID == _currentUserSession.RenoCompanyID, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(ToDetailViewModel(customer, update));
        }

        ApplyUpdate(customer, update);
        EnsureCustomerDisplayName(customer);
        TrackCustomerAddresses(customer);
        customer.LastEditDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["CustomersStatus"] = "Customer updated.";

        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Document(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.DocumentId == documentId && item.CustomerId == id && item.RenoCompanyID == _currentUserSession.RenoCompanyID, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        var documentPath = Path.GetFullPath(document.Path);

        if (!System.IO.File.Exists(documentPath))
        {
            return NotFound();
        }

        if (!ContentTypeProvider.TryGetContentType(document.Filename, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return PhysicalFile(documentPath, contentType);
    }

    [HttpPost("Customers/{id:guid}/DataSheetPdf")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DataSheetPdf(Guid id, string reportName, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .Include(item => item.BillAddress)
            .Include(item => item.Documents)
                .ThenInclude(document => document.DocumentType)
            .Include(item => item.Properties)
                .ThenInclude(property => property.Address)
            .Include(item => item.ShipAddress)
            .FirstOrDefaultAsync(item => item.CustomerId == id && item.RenoCompanyID == _currentUserSession.RenoCompanyID, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        var model = ToDetailViewModel(customer);
        var customerName = GetCustomerFullName(model);
        var printDate = DateTime.Now;
        var logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "MikeHandymanLogo.png");
        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.DefaultTextStyle(text => text.FontSize(10));

                page.Header().Element(container => ComposeCustomerDataSheetHeader(container, logoPath));
                page.Content()
                    .PaddingVertical(20)
                    .Element(container => ComposeCustomerDataSheetBody(container, model));
                page.Footer().Element(container => ComposeCustomerDataSheetFooter(container, customerName, printDate));
            });
        });

        var pdfBytes = document.GeneratePdf();
        const string extension = ".pdf";
        var documentName = GetSafeDocumentName(reportName, "CustomerDataSheet");
        var fileName = $"{documentName}{extension}";
        var documentsDirectory = Path.Combine(_webHostEnvironment.ContentRootPath, "Documents", "Customers");
        Directory.CreateDirectory(documentsDirectory);

        var documentPath = GetAvailableDocumentPath(documentsDirectory, fileName);
        fileName = Path.GetFileName(documentPath);
        await System.IO.File.WriteAllBytesAsync(documentPath, pdfBytes, cancellationToken);

        _dbContext.Documents.Add(new RenovatorApp.Infrastructure.Models.Document
        {
            RenoCompanyID = _currentUserSession.RenoCompanyID,
            DocumentName = documentName,
            CustomerId = customer.CustomerId,
            InspectionId = null,
            CreateDate = DateTime.UtcNow,
            DocumentTypeId = DocumentType.CustomerDataSheetId,
            Filename = fileName,
            Extension = extension,
            Path = documentPath
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["CustomersStatus"] = "Customer DataSheet created.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("Customers/{id:guid}/Properties/Add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProperty(Guid id, CustomerAddPropertyViewModel update, CancellationToken cancellationToken)
    {
        update.Address ??= new CustomerAddressUpdateViewModel();
        var isEdit = update.PropertyId.HasValue;

        if (string.IsNullOrWhiteSpace(update.Address.Street1) || string.IsNullOrWhiteSpace(update.Address.City))
        {
            TempData["CustomersStatus"] = $"Property {(isEdit ? "update" : "add")} failed. Street1 and City are required.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var customer = await _dbContext.Customers
            .ForCompany(_currentUserSession.RenoCompanyID)
            .Include(item => item.Properties)
                .ThenInclude(property => property.Address)
            .FirstOrDefaultAsync(item => item.CustomerId == id, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        var property = update.PropertyId.HasValue
            ? customer.Properties.FirstOrDefault(item => item.PropertyId == update.PropertyId.Value)
            : null;

        if (update.PropertyId.HasValue && property is null)
        {
            return NotFound();
        }

        if (property is null)
        {
            property = new Property
            {
                RenoCompanyID = _currentUserSession.RenoCompanyID,
                Address = new Address
                {
                    RenoCompanyID = _currentUserSession.RenoCompanyID
                }
            };

            customer.Properties.Add(property);
            _dbContext.Properties.Add(property);
        }

        property.Name = Clean(update.Name);
        property.Address ??= new Address
        {
            RenoCompanyID = _currentUserSession.RenoCompanyID
        };
        property.Address.RenoCompanyID = _currentUserSession.RenoCompanyID;
        property.Address.PropertyId = property.PropertyId;
        property.Address.Street1 = Clean(update.Address.Street1);
        property.Address.Street2 = Clean(update.Address.Street2);
        property.Address.Street3 = Clean(update.Address.Street3);
        property.Address.City = Clean(update.Address.City);
        property.Address.State = Clean(update.Address.State);
        property.Address.PostalCode = Clean(update.Address.PostalCode);
        property.Address.Country = Clean(update.Address.Country);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["CustomersStatus"] = isEdit ? "Property updated." : "Property added.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("Customers/{id:guid}/Properties/AddBillingAddress")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBillingAddressProperty(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .ForCompany(_currentUserSession.RenoCompanyID)
            .Include(item => item.BillAddress)
            .Include(item => item.Properties)
                .ThenInclude(property => property.Address)
            .FirstOrDefaultAsync(item => item.CustomerId == id, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        if (customer.BillAddress is null || IsAddressEmpty(customer.BillAddress))
        {
            TempData["CustomersStatus"] = "Billing address cannot be added because it is blank.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (customer.Properties.Any(property => AddressesMatch(customer.BillAddress, property.Address)))
        {
            TempData["CustomersStatus"] = "Billing address is already in the property list.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var property = new Property
        {
            RenoCompanyID = _currentUserSession.RenoCompanyID,
            Name = "Billing Address",
            Address = CopyPropertyAddress(customer.BillAddress, _currentUserSession.RenoCompanyID)
        };
        property.Address.PropertyId = property.PropertyId;

        customer.Properties.Add(property);
        _dbContext.Properties.Add(property);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["CustomersStatus"] = "Billing address added to properties.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("Customers/{id:guid}/Properties/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProperty(Guid id, Guid propertyId, CancellationToken cancellationToken)
    {
        var renoCompanyId = _currentUserSession.RenoCompanyID;
        var customer = await _dbContext.Customers
            .ForCompany(renoCompanyId)
            .Include(item => item.Properties)
            .FirstOrDefaultAsync(item => item.CustomerId == id, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        var property = await _dbContext.Properties
            .ForCompany(renoCompanyId)
            .FirstOrDefaultAsync(item => item.PropertyId == propertyId, cancellationToken);

        if (property is null || customer.Properties.All(item => item.PropertyId != propertyId))
        {
            return NotFound();
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.Inspections
            .Where(inspection => inspection.PropertyId == propertyId && inspection.RenoCompanyID == renoCompanyId)
            .ExecuteDeleteAsync(cancellationToken);

        customer.Properties.RemoveAll(item => item.PropertyId == propertyId);
        _dbContext.Properties.Remove(property);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["CustomersStatus"] = "Property deleted.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(item => item.CustomerId == id && item.RenoCompanyID == _currentUserSession.RenoCompanyID, cancellationToken);

        if (customer is null)
        {
            return RedirectToAction(nameof(Index));
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.Documents
            .Where(document => document.CustomerId == id && document.RenoCompanyID == _currentUserSession.RenoCompanyID)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.Inspections
            .Where(inspection => inspection.CustomerId == id && inspection.RenoCompanyID == _currentUserSession.RenoCompanyID)
            .ExecuteDeleteAsync(cancellationToken);

        _dbContext.Customers.Remove(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["CustomersStatus"] = "Customer deleted.";
        return RedirectToAction(nameof(Index));
    }

    private static void ComposeCustomerDataSheetHeader(IContainer container, string logoPath)
    {
        container.Row(row =>
        {
            row.ConstantItem(95)
                .Element(container =>
                {
                    if (System.IO.File.Exists(logoPath))
                    {
                        container.Image(logoPath).FitArea();
                    }
                    else
                    {
                        container.Border(1).BorderColor(Colors.Grey.Lighten2).Height(55);
                    }
                });
            row.RelativeItem()
                .PaddingLeft(18)
                .AlignMiddle()
                .Text("Customer DataSheet")
                .FontSize(20)
                .SemiBold()
                .FontColor(Colors.Blue.Darken3);
        });
    }

    private static void ComposeCustomerDataSheetBody(IContainer container, CustomerDetailViewModel customer)
    {
        container.Column(column =>
        {
            column.Spacing(14);
            column.Item().Element(container => ComposeCustomerPdfSection(container, "Name",
            [
                ("Display Name", customer.DisplayName),
                ("Company Name", customer.CompanyName),
                ("Title", customer.Title),
                ("First Name", customer.GivenName),
                ("Middle Name", customer.MiddleName),
                ("Last Name", customer.FamilyName),
                ("Suffix", customer.Suffix),
                ("Print On Check Name", customer.PrintOnCheckName)
            ]));
            column.Item().Element(container => ComposeCustomerPdfSection(container, "Contact",
            [
                ("Email", customer.PrimaryEmailAddress),
                ("Primary Phone", customer.PrimaryPhone),
                ("Alternate Phone", customer.AlternatePhone),
                ("Mobile", customer.MobilePhone),
                ("Fax", customer.Fax),
                ("Website", customer.Website),
                ("Notes", customer.Notes)
            ]));
            column.Item().Element(container => ComposeCustomerPdfSection(container, "Bill Address", GetAddressRows(customer.BillAddress)));
            column.Item().Element(container => ComposeCustomerProperties(container, customer.Properties));
            column.Item().Element(container => ComposeCustomerDocuments(container, customer.Documents));
            column.Item().Element(container => ComposeCustomerPdfSection(container, "Accounting",
            [
                ("Balance", FormatCurrency(customer.Balance)),
                ("Balance With Jobs", FormatCurrency(customer.BalanceWithJobs)),
                ("Preferred Delivery", customer.PreferredDeliveryMethod),
                ("Parent", customer.ParentRef),
                ("Payment Method", customer.PaymentMethodRef),
                ("Sales Term", customer.SalesTermRef),
                ("Currency", customer.CurrencyRef)
            ]));
            column.Item().Element(container => ComposeCustomerPdfSection(container, "Ship Address", GetAddressRows(customer.ShipAddress)));
            column.Item().Element(container => ComposeCustomerPdfSection(container, "Status",
            [
                ("Active", customer.Active),
                ("Taxable", customer.Taxable),
                ("Job", customer.Job),
                ("Bill With Parent", customer.BillWithParent)
            ]));
        });
    }

    private static void ComposeCustomerPdfSection(IContainer container, string title, IReadOnlyCollection<(string Label, string? Value)> rows)
    {
        container.Column(column =>
        {
            column.Spacing(5);
            column.Item().Text(title).FontSize(13).SemiBold().FontColor(Colors.Blue.Darken3);

            foreach (var row in rows)
            {
                column.Item().Row(item =>
                {
                    item.ConstantItem(145).Text(row.Label).SemiBold();
                    item.RelativeItem().Text(Blank(row.Value));
                });
            }
        });
    }

    private static void ComposeCustomerProperties(IContainer container, IReadOnlyCollection<CustomerPropertyViewModel> properties)
    {
        container.Column(column =>
        {
            column.Spacing(5);
            column.Item().Text("Properties").FontSize(13).SemiBold().FontColor(Colors.Blue.Darken3);

            if (properties.Count == 0)
            {
                column.Item().Text("No properties.");
                return;
            }

            foreach (var property in properties)
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(145).Text(Blank(property.Name)).SemiBold();
                    row.RelativeItem().Text(Blank(property.StreetAddress));
                });
            }
        });
    }

    private static void ComposeCustomerDocuments(IContainer container, IReadOnlyCollection<CustomerDocumentViewModel> documents)
    {
        container.Column(column =>
        {
            column.Spacing(5);
            column.Item().Text("Documents").FontSize(13).SemiBold().FontColor(Colors.Blue.Darken3);

            if (documents.Count == 0)
            {
                column.Item().Text("No documents.");
                return;
            }

            foreach (var document in documents)
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem(2).Text(Blank(document.DocumentName)).SemiBold();
                    row.RelativeItem().Text(Blank(document.DocumentType));
                    row.ConstantItem(110).AlignRight().Text(document.CreateDate.ToLocalTime().ToString("g"));
                });
            }
        });
    }

    private static void ComposeCustomerDataSheetFooter(IContainer container, string customerName, DateTime printDate)
    {
        container
            .BorderTop(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingTop(8)
            .Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("Renovator App");
                    text.Span("   |   Customer DataSheet");
                    text.Span($"   |   {customerName}");
                    text.Span($"   |   Print date: {printDate:g}");
                });
                row.ConstantItem(80).AlignRight().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
    }

    private static IReadOnlyCollection<(string Label, string? Value)> GetAddressRows(CustomerAddressViewModel? address)
    {
        return
        [
            ("Street 1", address?.Street1),
            ("Street 2", address?.Street2),
            ("Street 3", address?.Street3),
            ("City", address?.City),
            ("State", address?.State),
            ("Country Subdivision", address?.CountrySubDivisionCode),
            ("Postal Code", address?.PostalCode),
            ("Country", address?.Country)
        ];
    }

    private static string GetCustomerFullName(CustomerDetailViewModel customer)
    {
        var name = string.Join(" ", new[] { customer.GivenName, customer.FamilyName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.IsNullOrWhiteSpace(name) ? Blank(customer.DisplayName) : name;
    }

    private static string Blank(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string FormatCurrency(decimal value)
    {
        return $"${value:N2}";
    }

    private static string GetSafeDocumentName(string value, string fallback)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeName = new string(Clean(value)
            .Where(character => !invalidCharacters.Contains(character))
            .Where(character => !char.IsPunctuation(character) || character is '_' or '-')
            .ToArray());

        return string.IsNullOrWhiteSpace(safeName) ? fallback : safeName;
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

    private static CustomerRowViewModel ToRowViewModel(Customer customer)
    {
        return new CustomerRowViewModel
        {
            CustomerId = customer.CustomerId,
            QuickBooksCustomerId = customer.QuickBooksCustomerId,
            DisplayName = customer.DisplayName,
            CompanyName = customer.CompanyName,
            ContactName = string.Join(" ", new[] { customer.GivenName, customer.FamilyName }.Where(value => !string.IsNullOrWhiteSpace(value))),
            Email = customer.PrimaryEmailAddress,
            Phone = customer.PrimaryPhone,
            Active = customer.Active ? "Yes" : "No"
        };
    }

    private static CustomerDetailViewModel ToDetailViewModel(Customer customer)
    {
        return new CustomerDetailViewModel
        {
            CustomerId = customer.CustomerId,
            QuickBooksCustomerId = customer.QuickBooksCustomerId,
            SyncToken = customer.SyncToken,
            DisplayName = customer.DisplayName,
            FullyQualifiedName = customer.FullyQualifiedName,
            CompanyName = customer.CompanyName,
            Title = customer.Title,
            GivenName = customer.GivenName,
            MiddleName = customer.MiddleName,
            FamilyName = customer.FamilyName,
            Suffix = customer.Suffix,
            PrintOnCheckName = customer.PrintOnCheckName,
            PrimaryEmailAddress = customer.PrimaryEmailAddress,
            PrimaryPhone = customer.PrimaryPhone,
            AlternatePhone = customer.AlternatePhone,
            MobilePhone = customer.MobilePhone,
            Fax = customer.Fax,
            Website = customer.Website,
            Notes = customer.Notes,
            Active = customer.Active ? "Yes" : "No",
            Taxable = customer.Taxable ? "Yes" : "No",
            Job = customer.Job ? "Yes" : "No",
            BillWithParent = customer.BillWithParent ? "Yes" : "No",
            Balance = customer.Balance,
            BalanceWithJobs = customer.BalanceWithJobs,
            PreferredDeliveryMethod = customer.PreferredDeliveryMethod,
            ParentRef = FormatRef(customer.ParentRefName, customer.ParentRefValue),
            PaymentMethodRef = FormatRef(customer.PaymentMethodRefName, customer.PaymentMethodRefValue),
            SalesTermRef = FormatRef(customer.SalesTermRefName, customer.SalesTermRefValue),
            CurrencyRef = FormatRef(customer.CurrencyRefName, customer.CurrencyRefValue),
            QuickBooksCreateTime = customer.QuickBooksCreateTime,
            QuickBooksLastUpdatedTime = customer.QuickBooksLastUpdatedTime,
            CreatedDate = customer.CreatedDate,
            LastSyncDate = customer.LastSyncDate,
            LastEditDate = customer.LastEditDate,
            BillAddress = ToAddressViewModel(customer.BillAddress),
            Properties = customer.Properties
                .OrderBy(property => property.Name)
                .ThenBy(property => property.Address == null ? string.Empty : property.Address.Street1)
                .Select(property => new CustomerPropertyViewModel
                {
                    PropertyId = property.PropertyId,
                    Name = property.Name,
                    StreetAddress = FormatStreetAddress(property.Address),
                    Street1 = property.Address?.Street1 ?? string.Empty,
                    Street2 = property.Address?.Street2 ?? string.Empty,
                    Street3 = property.Address?.Street3 ?? string.Empty,
                    City = property.Address?.City ?? string.Empty,
                    State = property.Address?.State ?? string.Empty,
                    PostalCode = property.Address?.PostalCode ?? string.Empty,
                    Country = property.Address?.Country ?? string.Empty
                })
                .ToList(),
            CanAddBillingAddressProperty = customer.BillAddress is not null
                && !IsAddressEmpty(customer.BillAddress)
                && !customer.Properties.Any(property => AddressesMatch(customer.BillAddress, property.Address)),
            Documents = customer.Documents
                .OrderByDescending(document => document.CreateDate)
                .Select(document => new CustomerDocumentViewModel
                {
                    DocumentId = document.DocumentId,
                    DocumentName = document.DocumentName,
                    DocumentType = document.DocumentType?.Name ?? string.Empty,
                    Filename = document.Filename,
                    CreateDate = document.CreateDate
                })
                .ToList(),
            ShipAddress = ToAddressViewModel(customer.ShipAddress)
        };
    }

    private static string FormatStreetAddress(Address? address)
    {
        if (address is null)
        {
            return string.Empty;
        }

        var street = string.Join(" ", new[] { address.Street1, address.Street2, address.Street3 }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var cityState = string.Join(", ", new[] { address.City, address.State }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.Join(" ", new[] { street, cityState, address.PostalCode }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static Address CopyPropertyAddress(Address source, Guid renoCompanyId)
    {
        return new Address
        {
            RenoCompanyID = renoCompanyId,
            Street1 = Clean(source.Street1),
            Street2 = Clean(source.Street2),
            Street3 = Clean(source.Street3),
            City = Clean(source.City),
            State = Clean(source.State),
            CountrySubDivisionCode = Clean(source.CountrySubDivisionCode),
            PostalCode = Clean(source.PostalCode),
            Country = Clean(source.Country)
        };
    }

    private static bool AddressesMatch(Address billingAddress, Address? propertyAddress)
    {
        return propertyAddress is not null
            && AddressPartMatches(billingAddress.Street1, propertyAddress.Street1)
            && AddressPartMatches(billingAddress.Street2, propertyAddress.Street2)
            && AddressPartMatches(billingAddress.Street3, propertyAddress.Street3)
            && AddressPartMatches(billingAddress.City, propertyAddress.City)
            && AddressPartMatches(billingAddress.State, propertyAddress.State)
            && AddressPartMatches(billingAddress.PostalCode, propertyAddress.PostalCode)
            && AddressPartMatches(billingAddress.Country, propertyAddress.Country);
    }

    private static bool AddressPartMatches(string? left, string? right)
    {
        return string.Equals(Clean(left), Clean(right), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAddressEmpty(Address address)
    {
        return string.IsNullOrWhiteSpace(address.Street1)
            && string.IsNullOrWhiteSpace(address.Street2)
            && string.IsNullOrWhiteSpace(address.Street3)
            && string.IsNullOrWhiteSpace(address.City)
            && string.IsNullOrWhiteSpace(address.State)
            && string.IsNullOrWhiteSpace(address.PostalCode)
            && string.IsNullOrWhiteSpace(address.Country);
    }

    private static CustomerDetailViewModel ToDetailViewModel(Customer customer, CustomerDetailUpdateViewModel update)
    {
        update.BillAddress ??= new CustomerAddressUpdateViewModel();
        update.ShipAddress ??= new CustomerAddressUpdateViewModel();

        var model = ToDetailViewModel(customer);
        model.DisplayName = update.DisplayName;
        model.CompanyName = update.CompanyName;
        model.Title = update.Title;
        model.GivenName = update.GivenName;
        model.MiddleName = update.MiddleName;
        model.FamilyName = update.FamilyName;
        model.Suffix = update.Suffix;
        model.PrintOnCheckName = update.PrintOnCheckName;
        model.PrimaryEmailAddress = update.PrimaryEmailAddress;
        model.PrimaryPhone = update.PrimaryPhone;
        model.AlternatePhone = update.AlternatePhone;
        model.MobilePhone = update.MobilePhone;
        model.Fax = update.Fax;
        model.Website = update.Website;
        model.Notes = update.Notes;
        model.BillAddress = ToAddressViewModel(update.BillAddress);
        model.ShipAddress = ToAddressViewModel(update.ShipAddress);
        return model;
    }

    private static CustomerDetailViewModel ToNewDetailViewModel(CustomerDetailUpdateViewModel update)
    {
        update.BillAddress ??= new CustomerAddressUpdateViewModel();
        update.ShipAddress ??= new CustomerAddressUpdateViewModel();

        return new CustomerDetailViewModel
        {
            CustomerId = update.CustomerId == Guid.Empty ? Guid.NewGuid() : update.CustomerId,
            DisplayName = update.DisplayName,
            CompanyName = update.CompanyName,
            Title = update.Title,
            GivenName = update.GivenName,
            MiddleName = update.MiddleName,
            FamilyName = update.FamilyName,
            Suffix = update.Suffix,
            PrintOnCheckName = update.PrintOnCheckName,
            PrimaryEmailAddress = update.PrimaryEmailAddress,
            PrimaryPhone = update.PrimaryPhone,
            AlternatePhone = update.AlternatePhone,
            MobilePhone = update.MobilePhone,
            Fax = update.Fax,
            Website = update.Website,
            Notes = update.Notes,
            Active = "Yes",
            Taxable = "No",
            Job = "No",
            BillWithParent = "No",
            CreatedDate = DateTime.UtcNow,
            BillAddress = ToAddressViewModel(update.BillAddress),
            IncludeBillingAddressAsCustomerProperty = update.IncludeBillingAddressAsCustomerProperty,
            ShipAddress = ToAddressViewModel(update.ShipAddress),
            StateOptions = StateOptionsProvider.GetStates()
        };
    }

    private async Task<string> GetDefaultStateAsync(CancellationToken cancellationToken)
    {
        var defaultState = await _dbContext.AppSettings
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .Where(setting => setting.Name == "defaultstate")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(defaultState)
            ? string.Empty
            : defaultState.Trim().ToUpperInvariant();
    }

    private static void ApplyUpdate(Customer customer, CustomerDetailUpdateViewModel update)
    {
        update.BillAddress ??= new CustomerAddressUpdateViewModel();
        update.ShipAddress ??= new CustomerAddressUpdateViewModel();

        customer.DisplayName = Clean(update.DisplayName);
        customer.FullyQualifiedName = Clean(update.DisplayName);
        customer.CompanyName = Clean(update.CompanyName);
        customer.Title = Clean(update.Title);
        customer.GivenName = Clean(update.GivenName);
        customer.MiddleName = Clean(update.MiddleName);
        customer.FamilyName = Clean(update.FamilyName);
        customer.Suffix = Clean(update.Suffix);
        customer.PrintOnCheckName = Clean(update.PrintOnCheckName);
        customer.PrimaryEmailAddress = Clean(update.PrimaryEmailAddress);
        customer.PrimaryPhone = Clean(update.PrimaryPhone);
        customer.AlternatePhone = Clean(update.AlternatePhone);
        customer.MobilePhone = Clean(update.MobilePhone);
        customer.Fax = Clean(update.Fax);
        customer.Website = Clean(update.Website);
        customer.Notes = Clean(update.Notes);
        customer.BillAddress = ApplyAddress(customer.BillAddress, update.BillAddress);
        customer.BillAddressId = customer.BillAddress?.AddressId;
        customer.ShipAddress = ApplyAddress(customer.ShipAddress, update.ShipAddress);
        customer.ShipAddressId = customer.ShipAddress?.AddressId;
    }

    private static void EnsureCustomerDisplayName(Customer customer)
    {
        var displayName = Clean(customer.DisplayName);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = string.Join(" ", new[] { customer.GivenName, customer.FamilyName }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = Clean(customer.CompanyName);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Unnamed Customer";
        }

        customer.DisplayName = displayName;
        customer.FullyQualifiedName = displayName;
    }

    private static Address? ApplyAddress(Address? address, CustomerAddressUpdateViewModel update)
    {
        if (IsAddressEmpty(update))
        {
            return null;
        }

        address ??= new Address();
        address.Street1 = Clean(update.Street1);
        address.Street2 = Clean(update.Street2);
        address.Street3 = Clean(update.Street3);
        address.City = Clean(update.City);
        address.State = Clean(update.State);
        address.CountrySubDivisionCode = Clean(update.CountrySubDivisionCode);
        address.PostalCode = Clean(update.PostalCode);
        address.Country = Clean(update.Country);
        return address;
    }

    private static bool IsAddressEmpty(CustomerAddressUpdateViewModel address)
    {
        return string.IsNullOrWhiteSpace(address.Street1)
            && string.IsNullOrWhiteSpace(address.Street2)
            && string.IsNullOrWhiteSpace(address.Street3)
            && string.IsNullOrWhiteSpace(address.City)
            && string.IsNullOrWhiteSpace(address.State)
            && string.IsNullOrWhiteSpace(address.CountrySubDivisionCode)
            && string.IsNullOrWhiteSpace(address.PostalCode)
            && string.IsNullOrWhiteSpace(address.Country);
    }

    private static CustomerAddressViewModel ToAddressViewModel(CustomerAddressUpdateViewModel address)
    {
        return new CustomerAddressViewModel
        {
            Street1 = address.Street1,
            Street2 = address.Street2,
            Street3 = address.Street3,
            City = address.City,
            State = address.State,
            CountrySubDivisionCode = address.CountrySubDivisionCode,
            PostalCode = address.PostalCode,
            Country = address.Country
        };
    }

    private void TrackCustomerAddresses(Customer customer)
    {
        TrackAddress(customer.BillAddress);
        if (customer.BillAddress is not null)
        {
            customer.BillAddressId = customer.BillAddress.AddressId;
        }

        TrackAddress(customer.ShipAddress);
        if (customer.ShipAddress is not null)
        {
            customer.ShipAddressId = customer.ShipAddress.AddressId;
        }
    }

    private void AddBillingAddressProperty(Customer customer)
    {
        if (customer.BillAddress is null || IsAddressEmpty(customer.BillAddress))
        {
            return;
        }

        var property = new Property
        {
            RenoCompanyID = _currentUserSession.RenoCompanyID,
            Name = "Billing Address",
            Address = CopyPropertyAddress(customer.BillAddress, _currentUserSession.RenoCompanyID)
        };
        property.Address.PropertyId = property.PropertyId;

        customer.Properties.Add(property);
        _dbContext.Properties.Add(property);
    }

    private void TrackAddress(Address? address)
    {
        if (address is not null && _dbContext.Entry(address).State == EntityState.Detached)
        {
            address.RenoCompanyID = _currentUserSession.RenoCompanyID;
            _dbContext.Addresses.Add(address);
        }
    }

    private static string Clean(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static CustomerAddressViewModel? ToAddressViewModel(Address? address)
    {
        if (address is null)
        {
            return null;
        }

        return new CustomerAddressViewModel
        {
            Street1 = address.Street1,
            Street2 = address.Street2,
            Street3 = address.Street3,
            City = address.City,
            State = address.State,
            CountrySubDivisionCode = address.CountrySubDivisionCode,
            PostalCode = address.PostalCode,
            Country = address.Country
        };
    }

    private static string FormatRef(string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
        {
            return $"{name} ({value})";
        }

        return string.IsNullOrWhiteSpace(name) ? value : name;
    }

    private async Task<DateTime?> GetLastQuickBooksSyncDateUtcAsync(CancellationToken cancellationToken)
    {
        var value = await _dbContext.AppSettings
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .Where(setting => setting.Name == "QuickBooks:CustomersLastSyncDateUtc")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }
}
