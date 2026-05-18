using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

public sealed class CustomersController : Controller
{
    private const int PageSize = 10;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private readonly RenovatorAppDbContext _dbContext;

    public CustomersController(RenovatorAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = (search ?? string.Empty).Trim();
        var lastSyncDateUtc = await GetLastQuickBooksSyncDateUtcAsync(cancellationToken);
        var query = _dbContext.Customers.AsNoTracking();

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
            .Include(item => item.ShipAddress)
            .FirstOrDefaultAsync(item => item.CustomerId == id, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        return View(ToDetailViewModel(customer));
    }

    public async Task<IActionResult> Document(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.DocumentId == documentId && item.CustomerId == id, cancellationToken);

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

    private static CustomerRowViewModel ToRowViewModel(Customer customer)
    {
        return new CustomerRowViewModel
        {
            CustomerId = customer.CustomerId,
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
            Documents = customer.Documents
                .OrderByDescending(document => document.CreateDate)
                .Select(document => new CustomerDocumentViewModel
                {
                    DocumentId = document.DocumentId,
                    DocumentName = document.DocumentName,
                    DocumentType = document.DocumentType,
                    Filename = document.Filename,
                    CreateDate = document.CreateDate
                })
                .ToList(),
            ShipAddress = ToAddressViewModel(customer.ShipAddress)
        };
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
            .Where(setting => setting.Name == "QuickBooks:CustomersLastSyncDateUtc")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }
}
