using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

public sealed class EmployeesController : Controller
{
    private const int PageSize = 10;
    private readonly RenovatorAppDbContext _dbContext;

    public EmployeesController(RenovatorAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = (search ?? string.Empty).Trim();
        var lastSyncDateUtc = await GetLastQuickBooksSyncDateUtcAsync(cancellationToken);
        var query = _dbContext.Employees.AsNoTracking();

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
            .FirstOrDefaultAsync(item => item.EmployeeId == id, cancellationToken);

        if (employee is null)
        {
            return NotFound();
        }

        return View(ToDetailViewModel(employee));
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
            QuickBooksCreateTime = employee.QuickBooksCreateTime,
            QuickBooksLastUpdatedTime = employee.QuickBooksLastUpdatedTime,
            CreatedDate = employee.CreatedDate,
            LastSyncDate = employee.LastSyncDate,
            LastEditDate = employee.LastEditDate,
            PrimaryAddress = ToAddressViewModel(employee.PrimaryAddress)
        };
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
            .Where(setting => setting.Name == "QuickBooks:EmployeesLastSyncDateUtc")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }
}
