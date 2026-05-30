using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public CustomersController(RenovatorAppDbContext dbContext, CurrentUserSession currentUserSession)
    {
        _dbContext = dbContext;
        _currentUserSession = currentUserSession;
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
            .Include(item => item.ShipAddress)
            .FirstOrDefaultAsync(item => item.CustomerId == id && item.RenoCompanyID == _currentUserSession.RenoCompanyID, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        return View(ToDetailViewModel(customer));
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
        customer.BillAddressId = customer.BillAddress?.Id;
        customer.ShipAddress = ApplyAddress(customer.ShipAddress, update.ShipAddress);
        customer.ShipAddressId = customer.ShipAddress?.Id;
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
            customer.BillAddressId = customer.BillAddress.Id;
        }

        TrackAddress(customer.ShipAddress);
        if (customer.ShipAddress is not null)
        {
            customer.ShipAddressId = customer.ShipAddress.Id;
        }
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
