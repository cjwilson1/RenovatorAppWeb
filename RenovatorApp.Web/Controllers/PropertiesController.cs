using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.Services;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

public sealed class PropertiesController : Controller
{
    private const int PageSize = 10;
    private readonly RenovatorAppDbContext _dbContext;
    private readonly CurrentUserSession _currentUserSession;

    public PropertiesController(RenovatorAppDbContext dbContext, CurrentUserSession currentUserSession)
    {
        _dbContext = dbContext;
        _currentUserSession = currentUserSession;
    }

    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = (search ?? string.Empty).Trim();
        var query = _dbContext.Properties
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .Include(property => property.Address)
            .Include(property => property.Customers)
            .Include(property => property.Inspections)
            .AsQueryable();

        if (normalizedSearch.Length >= 2)
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(property =>
                EF.Functions.ILike(property.Name, pattern)
                || EF.Functions.ILike(property.Address.Street1, pattern)
                || EF.Functions.ILike(property.Address.Street2, pattern)
                || EF.Functions.ILike(property.Address.Street3, pattern)
                || EF.Functions.ILike(property.Address.City, pattern)
                || EF.Functions.ILike(property.Address.State, pattern)
                || EF.Functions.ILike(property.Address.PostalCode, pattern)
                || property.Customers.Any(customer =>
                    EF.Functions.ILike(customer.DisplayName, pattern)
                    || EF.Functions.ILike(customer.GivenName, pattern)
                    || EF.Functions.ILike(customer.FamilyName, pattern)
                    || EF.Functions.ILike(customer.CompanyName, pattern))
                || property.Inspections.Any(inspection => EF.Functions.ILike(inspection.Title, pattern)));
        }

        var totalProperties = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalProperties / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var properties = await query
            .OrderBy(property => property.Name)
            .ThenBy(property => property.Address.Street1)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        return View(new PropertiesIndexViewModel
        {
            Properties = properties.Select(ToRowViewModel).ToList(),
            StateOptions = StateOptionsProvider.GetStates(),
            Page = page,
            PageSize = PageSize,
            TotalProperties = totalProperties,
            TotalPages = totalPages,
            Search = normalizedSearch
        });
    }

    [HttpPost("Properties/{id:guid}/Update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        Guid id,
        PropertyUpdateViewModel update,
        string? search,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var renoCompanyId = _currentUserSession.RenoCompanyID;
        var property = await _dbContext.Properties
            .ForCompany(renoCompanyId)
            .Include(item => item.Address)
            .FirstOrDefaultAsync(item => item.PropertyId == id, cancellationToken);

        if (property is null)
        {
            return NotFound();
        }

        property.Name = Clean(update.Name);
        property.Address ??= new Address
        {
            RenoCompanyID = renoCompanyId,
            PropertyId = property.PropertyId
        };
        property.Address.RenoCompanyID = renoCompanyId;
        property.Address.PropertyId = property.PropertyId;
        property.Address.Street1 = Clean(update.Street1);
        property.Address.Street2 = Clean(update.Street2);
        property.Address.Street3 = Clean(update.Street3);
        property.Address.City = Clean(update.City);
        property.Address.State = Clean(update.State);
        property.Address.CountrySubDivisionCode = Clean(update.CountrySubDivisionCode);
        property.Address.PostalCode = Clean(update.PostalCode);
        property.Address.Country = Clean(update.Country);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index), new { search, page });
    }

    [HttpPost("Properties/{id:guid}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        Guid id,
        string? search,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var renoCompanyId = _currentUserSession.RenoCompanyID;
        var property = await _dbContext.Properties
            .ForCompany(renoCompanyId)
            .Include(item => item.Customers)
            .FirstOrDefaultAsync(item => item.PropertyId == id, cancellationToken);

        if (property is null)
        {
            return NotFound();
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.Inspections
            .Where(inspection => inspection.PropertyId == id && inspection.RenoCompanyID == renoCompanyId)
            .ExecuteDeleteAsync(cancellationToken);

        property.Customers.Clear();
        _dbContext.Properties.Remove(property);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RedirectToAction(nameof(Index), new { search, page });
    }

    private static PropertyRowViewModel ToRowViewModel(Property property)
    {
        return new PropertyRowViewModel
        {
            PropertyId = property.PropertyId,
            Name = property.Name,
            Address = FormatAddress(property.Address),
            Street1 = property.Address?.Street1 ?? string.Empty,
            Street2 = property.Address?.Street2 ?? string.Empty,
            Street3 = property.Address?.Street3 ?? string.Empty,
            City = property.Address?.City ?? string.Empty,
            State = property.Address?.State ?? string.Empty,
            CountrySubDivisionCode = property.Address?.CountrySubDivisionCode ?? string.Empty,
            PostalCode = property.Address?.PostalCode ?? string.Empty,
            Country = property.Address?.Country ?? string.Empty,
            CustomerNames = string.Join(", ", property.Customers
                .OrderBy(customer => customer.DisplayName)
                .Select(GetCustomerName)
                .Where(value => !string.IsNullOrWhiteSpace(value))),
            InspectionNames = string.Join(", ", property.Inspections
                .OrderByDescending(inspection => inspection.InspectionDate)
                .ThenBy(inspection => inspection.Title)
                .Select(inspection => inspection.Title)
                .Where(value => !string.IsNullOrWhiteSpace(value)))
        };
    }

    private static string FormatAddress(Address? address)
    {
        if (address is null)
        {
            return string.Empty;
        }

        var street = string.Join(" ", new[] { address.Street1, address.Street2, address.Street3 }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var cityStateZip = string.Join(" ", new[]
        {
            string.Join(", ", new[] { address.City, address.State }.Where(value => !string.IsNullOrWhiteSpace(value))),
            address.PostalCode
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.Join(" ", new[] { street, cityStateZip }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string GetCustomerName(Customer customer)
    {
        if (!string.IsNullOrWhiteSpace(customer.DisplayName))
        {
            return customer.DisplayName;
        }

        var fullName = string.Join(" ", new[] { customer.GivenName, customer.FamilyName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.IsNullOrWhiteSpace(fullName) ? customer.CompanyName : fullName;
    }

    private static string Clean(string? value) => (value ?? string.Empty).Trim();
}
