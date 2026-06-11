using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.Services;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

public sealed class EmployeesController : Controller
{
    private const int PageSize = 10;
    private readonly RenovatorAppDbContext _dbContext;
    private readonly CurrentUserSession _currentUserSession;

    public EmployeesController(RenovatorAppDbContext dbContext, CurrentUserSession currentUserSession)
    {
        _dbContext = dbContext;
        _currentUserSession = currentUserSession;
    }

    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = (search ?? string.Empty).Trim();
        var lastSyncDateUtc = await GetLastQuickBooksSyncDateUtcAsync(cancellationToken);
        var query = _dbContext.Employees.AsNoTracking().ForCompany(_currentUserSession.RenoCompanyID);

        if (normalizedSearch.Length >= 2)
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(employee =>
                EF.Functions.ILike(employee.DisplayName, pattern)
                || EF.Functions.ILike(employee.GivenName, pattern)
                || EF.Functions.ILike(employee.FamilyName, pattern)
                || EF.Functions.ILike(employee.PrimaryEmailAddress, pattern)
                || EF.Functions.ILike(employee.PrimaryPhone, pattern)
                || EF.Functions.ILike(employee.EmployeeNumber, pattern));
        }

        var totalEmployees = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalEmployees / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var employees = await query
            .OrderBy(employee => employee.DisplayName)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(employee => ToRowViewModel(employee))
            .ToListAsync(cancellationToken);

        return View(new EmployeesIndexViewModel
        {
            Employees = employees,
            Page = page,
            PageSize = PageSize,
            TotalEmployees = totalEmployees,
            TotalPages = totalPages,
            Search = normalizedSearch,
            LastQuickBooksSyncDateUtc = lastSyncDateUtc,
            StatusMessage = TempData["EmployeesStatus"] as string ?? string.Empty
        });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees
            .AsNoTracking()
            .Include(item => item.PrimaryAddress)
            .FirstOrDefaultAsync(item => item.EmployeeId == id && item.RenoCompanyID == _currentUserSession.RenoCompanyID, cancellationToken);

        if (employee is null)
        {
            return NotFound();
        }

        return View(ToDetailViewModel(employee));
    }

    [HttpGet]
    public IActionResult New()
    {
        return View("Details", new EmployeeDetailViewModel
        {
            IsNew = true,
            EmployeeId = Guid.NewGuid(),
            Active = "Yes",
            CreatedDate = DateTime.UtcNow,
            PrimaryAddress = new EmployeeAddressViewModel()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Details(Guid id, EmployeeDetailUpdateViewModel update, CancellationToken cancellationToken)
    {
        if (id != update.EmployeeId)
        {
            return BadRequest();
        }

        var employee = await _dbContext.Employees
            .Include(item => item.PrimaryAddress)
            .FirstOrDefaultAsync(item => item.EmployeeId == id && item.RenoCompanyID == _currentUserSession.RenoCompanyID, cancellationToken);

        if (employee is null)
        {
            return NotFound();
        }

        ValidateExistingEmployeeUpdate(update);
        if (!ModelState.IsValid)
        {
            return View(ToDetailViewModel(employee, update));
        }

        ApplyUpdate(employee, update);
        TrackPrimaryAddress(employee);
        employee.LastEditDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["EmployeesStatus"] = "Employee updated.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(EmployeeDetailUpdateViewModel update, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Details", ToNewDetailViewModel(update));
        }

        if (string.IsNullOrWhiteSpace(update.DisplayName))
        {
            update.DisplayName = BuildEmployeeDisplayName(update);
        }

        var employee = new Employee
        {
            EmployeeId = update.EmployeeId == Guid.Empty ? Guid.NewGuid() : update.EmployeeId,
            RenoCompanyID = _currentUserSession.RenoCompanyID,
            Active = true,
            CreatedDate = DateTime.UtcNow,
            LastEditDate = DateTime.UtcNow
        };

        ApplyUpdate(employee, update);
        TrackPrimaryAddress(employee);

        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["EmployeesStatus"] = "Employee created.";

        return RedirectToAction(nameof(Index));
    }

    private static EmployeeRowViewModel ToRowViewModel(Employee employee)
    {
        return new EmployeeRowViewModel
        {
            EmployeeId = employee.EmployeeId,
            DisplayName = employee.DisplayName,
            ContactName = string.Join(" ", new[] { employee.GivenName, employee.FamilyName }.Where(value => !string.IsNullOrWhiteSpace(value))),
            Email = employee.PrimaryEmailAddress,
            Phone = employee.PrimaryPhone,
            IsInspector = employee.IsInspector,
            Active = employee.Active ? "Yes" : "No"
        };
    }

    private static EmployeeDetailViewModel ToDetailViewModel(Employee employee)
    {
        return new EmployeeDetailViewModel
        {
            EmployeeId = employee.EmployeeId,
            QuickBooksEmployeeId = employee.QuickBooksEmployeeId,
            SyncToken = employee.SyncToken,
            DisplayName = employee.DisplayName,
            PrintOnCheckName = employee.PrintOnCheckName,
            Title = employee.Title,
            GivenName = employee.GivenName,
            MiddleName = employee.MiddleName,
            FamilyName = employee.FamilyName,
            Suffix = employee.Suffix,
            PrimaryEmailAddress = employee.PrimaryEmailAddress,
            PrimaryPhone = employee.PrimaryPhone,
            MobilePhone = employee.MobilePhone,
            Active = employee.Active ? "Yes" : "No",
            BillableTime = employee.BillableTime ? "Yes" : "No",
            EmployeeNumber = employee.EmployeeNumber,
            Organization = employee.Organization,
            Gender = employee.Gender,
            HiredDate = employee.HiredDate,
            ReleasedDate = employee.ReleasedDate,
            BirthDate = employee.BirthDate,
            BillRate = employee.BillRate,
            HourlyCostRate = employee.HourlyCostRate,
            InspectorHourlyRate = employee.InspectorHourlyRate,
            IsInspector = employee.IsInspector,
            IsDefaultInspector = employee.IsDefaultInspector,
            QuickBooksCreateTime = employee.QuickBooksCreateTime,
            QuickBooksLastUpdatedTime = employee.QuickBooksLastUpdatedTime,
            CreatedDate = employee.CreatedDate,
            LastSyncDate = employee.LastSyncDate,
            LastEditDate = employee.LastEditDate,
            PrimaryAddress = ToAddressViewModel(employee.PrimaryAddress)
        };
    }

    private static EmployeeDetailViewModel ToNewDetailViewModel(EmployeeDetailUpdateViewModel update)
    {
        update.PrimaryAddress ??= new EmployeeAddressUpdateViewModel();

        return new EmployeeDetailViewModel
        {
            IsNew = true,
            EmployeeId = update.EmployeeId == Guid.Empty ? Guid.NewGuid() : update.EmployeeId,
            DisplayName = update.DisplayName,
            PrintOnCheckName = update.PrintOnCheckName,
            Title = update.Title,
            GivenName = update.GivenName,
            MiddleName = update.MiddleName,
            FamilyName = update.FamilyName,
            Suffix = update.Suffix,
            PrimaryEmailAddress = update.PrimaryEmailAddress,
            PrimaryPhone = update.PrimaryPhone,
            MobilePhone = update.MobilePhone,
            Active = "Yes",
            BillRate = update.BillRate,
            HourlyCostRate = update.HourlyCostRate,
            InspectorHourlyRate = update.InspectorHourlyRate,
            IsInspector = update.IsInspector,
            IsDefaultInspector = update.IsDefaultInspector,
            CreatedDate = DateTime.UtcNow,
            PrimaryAddress = new EmployeeAddressViewModel
            {
                Street1 = update.PrimaryAddress.Street1,
                Street2 = update.PrimaryAddress.Street2,
                Street3 = update.PrimaryAddress.Street3,
                City = update.PrimaryAddress.City,
                State = update.PrimaryAddress.State,
                CountrySubDivisionCode = update.PrimaryAddress.CountrySubDivisionCode,
                PostalCode = update.PrimaryAddress.PostalCode,
                Country = update.PrimaryAddress.Country
            }
        };
    }

    private static EmployeeDetailViewModel ToDetailViewModel(Employee employee, EmployeeDetailUpdateViewModel update)
    {
        update.PrimaryAddress ??= new EmployeeAddressUpdateViewModel();

        var model = ToDetailViewModel(employee);
        model.DisplayName = update.DisplayName;
        model.PrintOnCheckName = update.PrintOnCheckName;
        model.Title = update.Title;
        model.GivenName = update.GivenName;
        model.MiddleName = update.MiddleName;
        model.FamilyName = update.FamilyName;
        model.Suffix = update.Suffix;
        model.PrimaryEmailAddress = update.PrimaryEmailAddress;
        model.PrimaryPhone = update.PrimaryPhone;
        model.MobilePhone = update.MobilePhone;
        model.BillRate = update.BillRate;
        model.HourlyCostRate = update.HourlyCostRate;
        model.InspectorHourlyRate = update.InspectorHourlyRate;
        model.IsInspector = update.IsInspector;
        model.IsDefaultInspector = update.IsDefaultInspector;
        model.PrimaryAddress = new EmployeeAddressViewModel
        {
            Street1 = update.PrimaryAddress.Street1,
            Street2 = update.PrimaryAddress.Street2,
            Street3 = update.PrimaryAddress.Street3,
            City = update.PrimaryAddress.City,
            State = update.PrimaryAddress.State,
            CountrySubDivisionCode = update.PrimaryAddress.CountrySubDivisionCode,
            PostalCode = update.PrimaryAddress.PostalCode,
            Country = update.PrimaryAddress.Country
        };
        return model;
    }

    private static void ApplyUpdate(Employee employee, EmployeeDetailUpdateViewModel update)
    {
        update.PrimaryAddress ??= new EmployeeAddressUpdateViewModel();

        employee.DisplayName = Clean(update.DisplayName);
        employee.PrintOnCheckName = Clean(update.PrintOnCheckName);
        employee.Title = Clean(update.Title);
        employee.GivenName = Clean(update.GivenName);
        employee.MiddleName = Clean(update.MiddleName);
        employee.FamilyName = Clean(update.FamilyName);
        employee.Suffix = Clean(update.Suffix);
        employee.PrimaryEmailAddress = Clean(update.PrimaryEmailAddress);
        employee.PrimaryPhone = Clean(update.PrimaryPhone);
        employee.MobilePhone = Clean(update.MobilePhone);
        employee.BillRate = update.BillRate;
        employee.HourlyCostRate = update.HourlyCostRate;
        employee.InspectorHourlyRate = update.InspectorHourlyRate;
        employee.IsInspector = update.IsInspector;
        employee.IsDefaultInspector = update.IsDefaultInspector;

        if (IsEmpty(update.PrimaryAddress))
        {
            employee.PrimaryAddress = null;
            employee.PrimaryAddressId = null;
            return;
        }

        employee.PrimaryAddress ??= new Address();
        employee.PrimaryAddress.Street1 = Clean(update.PrimaryAddress.Street1);
        employee.PrimaryAddress.Street2 = Clean(update.PrimaryAddress.Street2);
        employee.PrimaryAddress.Street3 = Clean(update.PrimaryAddress.Street3);
        employee.PrimaryAddress.City = Clean(update.PrimaryAddress.City);
        employee.PrimaryAddress.State = Clean(update.PrimaryAddress.State);
        employee.PrimaryAddress.CountrySubDivisionCode = Clean(update.PrimaryAddress.CountrySubDivisionCode);
        employee.PrimaryAddress.PostalCode = Clean(update.PrimaryAddress.PostalCode);
        employee.PrimaryAddress.Country = Clean(update.PrimaryAddress.Country);
    }

    private static bool IsEmpty(EmployeeAddressUpdateViewModel address)
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

    private static string Clean(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private void ValidateExistingEmployeeUpdate(EmployeeDetailUpdateViewModel update)
    {
        if (string.IsNullOrWhiteSpace(update.DisplayName))
        {
            ModelState.AddModelError(nameof(update.DisplayName), "The Display Name field is required.");
        }

        if (string.IsNullOrWhiteSpace(update.PrimaryPhone))
        {
            ModelState.AddModelError(nameof(update.PrimaryPhone), "The Primary Phone field is required.");
        }
    }

    private static string BuildEmployeeDisplayName(EmployeeDetailUpdateViewModel update)
    {
        return string.Join(" ", new[] { update.GivenName, update.FamilyName }
            .Select(Clean)
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private void TrackPrimaryAddress(Employee employee)
    {
        if (employee.PrimaryAddress is null)
        {
            return;
        }

        if (_dbContext.Entry(employee.PrimaryAddress).State == EntityState.Detached)
        {
            employee.PrimaryAddress.RenoCompanyID = _currentUserSession.RenoCompanyID;
            _dbContext.Addresses.Add(employee.PrimaryAddress);
        }

        employee.PrimaryAddressId = employee.PrimaryAddress.AddressId;
    }

    private static EmployeeAddressViewModel? ToAddressViewModel(Address? address)
    {
        if (address is null)
        {
            return null;
        }

        return new EmployeeAddressViewModel
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

    private async Task<DateTime?> GetLastQuickBooksSyncDateUtcAsync(CancellationToken cancellationToken)
    {
        var value = await _dbContext.AppSettings
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .Where(setting => setting.Name == "QuickBooks:EmployeesLastSyncDateUtc")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }
}
