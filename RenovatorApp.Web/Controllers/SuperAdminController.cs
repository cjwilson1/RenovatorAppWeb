using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.Services;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
public sealed class SuperAdminController : Controller
{
    private const int DefaultPageSize = 20;
    private const int CompaniesPageSize = 10;
    private const int UsersPageSize = 10;
    private static readonly Guid TemplateRenoCompanyID = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly DatabaseViewerService _databaseViewerService;
    private readonly RenovatorAppDbContext _dbContext;
    private readonly PasswordService _passwordService;
    private readonly CurrentUserSession _currentUserSession;

    public SuperAdminController(
        DatabaseViewerService databaseViewerService,
        RenovatorAppDbContext dbContext,
        PasswordService passwordService,
        CurrentUserSession currentUserSession)
    {
        _databaseViewerService = databaseViewerService;
        _dbContext = dbContext;
        _passwordService = passwordService;
        _currentUserSession = currentUserSession;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("SuperAdmin/Users")]
    public async Task<IActionResult> Users(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = (search ?? string.Empty).Trim();
        var query = _dbContext.RenoUsers
            .AsNoTracking()
            .Include(user => user.RenoCompany)
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .AsQueryable();

        if (normalizedSearch.Length >= 2)
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(user =>
                EF.Functions.ILike(user.Login, pattern)
                || EF.Functions.ILike(user.FirstName, pattern)
                || EF.Functions.ILike(user.LastName, pattern)
                || EF.Functions.ILike(user.Email, pattern)
                || EF.Functions.ILike(user.PhonePrimary, pattern)
                || EF.Functions.ILike(user.PhoneSecondary, pattern)
                || (user.RenoCompany != null && EF.Functions.ILike(user.RenoCompany.Name, pattern)));
        }

        var totalUsers = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalUsers / (double)UsersPageSize));
        page = Math.Clamp(page, 1, totalPages);

        var users = await query
            .OrderBy(user => user.Login)
            .Skip((page - 1) * UsersPageSize)
            .Take(UsersPageSize)
            .ToListAsync(cancellationToken);

        return View(new SuperAdminUsersViewModel
        {
            Users = users.Select(ToUserRowViewModel).ToList(),
            Search = normalizedSearch,
            Page = page,
            PageSize = UsersPageSize,
            TotalUsers = totalUsers,
            TotalPages = totalPages
        });
    }

    [HttpGet("SuperAdmin/Users/Add")]
    public async Task<IActionResult> AddUser(CancellationToken cancellationToken)
    {
        return View("EditUser", await BuildGlobalUserEditViewModelAsync(null, cancellationToken));
    }

    [HttpPost("SuperAdmin/Users/{id:guid}/Login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginAsUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.RenoUsers
            .Include(item => item.RenoCompany)
            .Include(item => item.UserRoles)
                .ThenInclude(item => item.Role)
            .FirstOrDefaultAsync(item => item.UserID == id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        if (!user.Active || user.RenoCompany is null || !user.RenoCompany.Active || user.RenoCompanyID is null)
        {
            TempData["SuperAdminUsersMessage"] = "That user cannot be logged in because the user or company is inactive or unassigned.";
            return RedirectToAction(nameof(Users));
        }

        var roleIDs = user.UserRoles.Select(item => item.RoleID).ToList();
        var roleNames = user.UserRoles
            .Select(item => item.Role?.Name)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new(ClaimTypes.Name, user.Login),
            new("RenoCompanyID", user.RenoCompanyID.Value.ToString()),
            new("RoleIDs", string.Join(',', roleIDs))
        };

        claims.AddRange(roleNames.Select(roleName => new Claim(ClaimTypes.Role, roleName)));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties
            {
                IsPersistent = false,
                IssuedUtc = DateTimeOffset.UtcNow
            });

        user.DateLastLogin = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        _currentUserSession.Set(user.UserID, user.RenoCompanyID.Value, roleIDs);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost("SuperAdmin/Users/Add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUser(SuperAdminUserEditViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Password is required for new users.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateUserEditOptionsAsync(model, cancellationToken);
            return View("EditUser", model);
        }

        if (await _dbContext.RenoUsers.AnyAsync(user => user.Login == model.Login.Trim(), cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Login), "A user with this login already exists.");
            await PopulateUserEditOptionsAsync(model, cancellationToken);
            return View("EditUser", model);
        }

        var user = new RenoUser
        {
            RenoCompanyID = null,
            Login = Clean(model.Login),
            Password = _passwordService.HashPassword(model.Password!),
            FirstName = Clean(model.FirstName),
            LastName = Clean(model.LastName),
            Email = Clean(model.Email),
            PhonePrimary = Clean(model.PhonePrimary),
            PhoneSecondary = Clean(model.PhoneSecondary),
            Active = model.Active,
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow
        };

        _dbContext.RenoUsers.Add(user);
        ApplyUserRoles(user, model.SelectedRoleIDs);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Users));
    }

    [HttpGet("SuperAdmin/Users/{id:guid}")]
    public async Task<IActionResult> EditUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.RenoUsers
            .AsNoTracking()
            .Include(item => item.RenoCompany)
            .Include(item => item.UserRoles)
            .FirstOrDefaultAsync(item => item.UserID == id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        return View("EditUser", await BuildGlobalUserEditViewModelAsync(user, cancellationToken));
    }

    [HttpPost("SuperAdmin/Users/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(Guid id, SuperAdminUserEditViewModel model, CancellationToken cancellationToken)
    {
        model.UserID = id;

        if (!ModelState.IsValid)
        {
            await PopulateUserEditOptionsAsync(model, cancellationToken);
            return View("EditUser", model);
        }

        var user = await _dbContext.RenoUsers
            .Include(item => item.UserRoles)
            .Include(item => item.RenoCompany)
            .FirstOrDefaultAsync(item => item.UserID == id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var normalizedLogin = Clean(model.Login);
        if (await _dbContext.RenoUsers.AnyAsync(item => item.UserID != id && item.Login == normalizedLogin, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Login), "A user with this login already exists.");
            model.RenoCompanyID = user.RenoCompanyID;
            await PopulateUserEditOptionsAsync(model, cancellationToken);
            return View("EditUser", model);
        }

        ApplyUserUpdate(user, model);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Users));
    }

    [HttpGet("SuperAdmin/Companies")]
    public async Task<IActionResult> Companies(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = (search ?? string.Empty).Trim();
        var query = _dbContext.RenoCompanies.AsNoTracking();

        if (normalizedSearch.Length >= 2)
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(company =>
                EF.Functions.ILike(company.Name, pattern)
                || EF.Functions.ILike(company.City, pattern)
                || EF.Functions.ILike(company.State, pattern)
                || EF.Functions.ILike(company.Phone, pattern)
                || EF.Functions.ILike(company.Email, pattern)
                || EF.Functions.ILike(company.URL, pattern));
        }

        var totalCompanies = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCompanies / (double)CompaniesPageSize));
        page = Math.Clamp(page, 1, totalPages);

        var companies = await query
            .OrderBy(company => company.Name)
            .Skip((page - 1) * CompaniesPageSize)
            .Take(CompaniesPageSize)
            .Select(company => new SuperAdminCompanyRowViewModel
            {
                RenoCompanyID = company.RenoCompanyID,
                Name = company.Name,
                City = company.City,
                State = company.State,
                Phone = company.Phone,
                Email = company.Email,
                URL = company.URL,
                Active = company.Active,
                DateCreated = company.DateCreated
            })
            .ToListAsync(cancellationToken);

        return View(new SuperAdminCompaniesViewModel
        {
            Companies = companies,
            Search = normalizedSearch,
            Page = page,
            PageSize = CompaniesPageSize,
            TotalCompanies = totalCompanies,
            TotalPages = totalPages
        });
    }

    [HttpGet("SuperAdmin/Companies/Add")]
    public IActionResult AddCompany()
    {
        return View(new SuperAdminCompanyEditViewModel());
    }

    [HttpPost("SuperAdmin/Companies/Add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCompany(SuperAdminCompanyEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var company = new RenoCompany
        {
            Name = Clean(model.Name),
            StreetAddress = Clean(model.StreetAddress),
            StreetAddress2 = Clean(model.StreetAddress2),
            City = Clean(model.City),
            State = Clean(model.State),
            Zip = Clean(model.Zip),
            Phone = Clean(model.Phone),
            Fax = Clean(model.Fax),
            Email = Clean(model.Email),
            URL = Clean(model.URL),
            Active = model.Active,
            DateCreated = DateTime.UtcNow
        };

        _dbContext.RenoCompanies.Add(company);
        await SeedCompanyLookupTablesAsync(company.RenoCompanyID, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Companies));
    }

    [HttpGet("SuperAdmin/Companies/{id:guid}")]
    public async Task<IActionResult> EditCompany(Guid id, CancellationToken cancellationToken)
    {
        var company = await _dbContext.RenoCompanies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.RenoCompanyID == id, cancellationToken);

        if (company is null)
        {
            return NotFound();
        }

        return View("AddCompany", new SuperAdminCompanyEditViewModel
        {
            RenoCompanyID = company.RenoCompanyID,
            Name = company.Name,
            StreetAddress = company.StreetAddress,
            StreetAddress2 = company.StreetAddress2,
            City = company.City,
            State = company.State,
            Zip = company.Zip,
            Phone = company.Phone,
            Fax = company.Fax,
            Email = company.Email,
            URL = company.URL,
            Active = company.Active,
            Users = await GetCompanyUsersAsync(company.RenoCompanyID, cancellationToken)
        });
    }

    [HttpPost("SuperAdmin/Companies/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCompany(Guid id, SuperAdminCompanyEditViewModel model, CancellationToken cancellationToken)
    {
        model.RenoCompanyID = id;

        if (!ModelState.IsValid)
        {
            return View("AddCompany", model);
        }

        var company = await _dbContext.RenoCompanies
            .FirstOrDefaultAsync(item => item.RenoCompanyID == id, cancellationToken);

        if (company is null)
        {
            return NotFound();
        }

        company.Name = Clean(model.Name);
        company.StreetAddress = Clean(model.StreetAddress);
        company.StreetAddress2 = Clean(model.StreetAddress2);
        company.City = Clean(model.City);
        company.State = Clean(model.State);
        company.Zip = Clean(model.Zip);
        company.Phone = Clean(model.Phone);
        company.Fax = Clean(model.Fax);
        company.Email = Clean(model.Email);
        company.URL = Clean(model.URL);
        company.Active = model.Active;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Companies));
    }

    [HttpPost("SuperAdmin/Companies/{id:guid}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCompany(Guid id, CancellationToken cancellationToken)
    {
        var companyExists = await _dbContext.RenoCompanies
            .AnyAsync(company => company.RenoCompanyID == id, cancellationToken);

        if (!companyExists)
        {
            return RedirectToAction(nameof(Companies));
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.UserRoles
            .Where(userRole => _dbContext.RenoUsers.Any(user => user.UserID == userRole.UserID && user.RenoCompanyID == id))
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.RenoUsers.Where(user => user.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);

        await _dbContext.InspectionAreaNotePhotos.Where(photo => photo.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.InspectionAreaNoteEstimateItems.Where(item => item.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.InspectionAreaNotes.Where(note => note.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.InspectionAreas.Where(area => area.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Buildings.Where(building => building.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Inspections.Where(inspection => inspection.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);

        await _dbContext.MileageTrackingWaypoints.Where(waypoint => waypoint.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.MileageTracking.Where(session => session.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);

        await _dbContext.Documents.Where(document => document.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Customers.Where(customer => customer.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Employees.Where(employee => employee.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Inspectors.Where(inspector => inspector.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);

        await _dbContext.Parts.Where(part => part.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PartSources.Where(source => source.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.InspectionAreaTypes.Where(areaType => areaType.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.InspectionAreaCategories.Where(category => category.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.BuildingTypes.Where(buildingType => buildingType.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.AppSettings.Where(setting => setting.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);

        await _dbContext.Addresses.Where(address => address.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Properties.Where(property => property.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.RenoCompanies.Where(company => company.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return RedirectToAction(nameof(Companies));
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/BuildingTypes")]
    public Task<IActionResult> CompanyBuildingTypes(Guid companyId, int page = 1, CancellationToken cancellationToken = default)
    {
        return CompanyTable(companyId, "Building Type", "BuildingType", nameof(CompanyBuildingTypes), page, cancellationToken);
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/Customers")]
    public Task<IActionResult> CompanyCustomers(Guid companyId, int page = 1, CancellationToken cancellationToken = default)
    {
        return CompanyTable(companyId, "Customer", "Customer", nameof(CompanyCustomers), page, cancellationToken);
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/Employees")]
    public Task<IActionResult> CompanyEmployees(Guid companyId, int page = 1, CancellationToken cancellationToken = default)
    {
        return CompanyTable(companyId, "Employee", "Employee", nameof(CompanyEmployees), page, cancellationToken);
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/Inspections")]
    public Task<IActionResult> CompanyInspections(Guid companyId, int page = 1, CancellationToken cancellationToken = default)
    {
        return CompanyTable(companyId, "Inspection", "Inspection", nameof(CompanyInspections), page, cancellationToken);
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/Inspections/{inspectionId:guid}")]
    public async Task<IActionResult> CompanyInspectionDetail(Guid companyId, Guid inspectionId, CancellationToken cancellationToken = default)
    {
        var companyName = await _dbContext.RenoCompanies
            .Where(company => company.RenoCompanyID == companyId)
            .Select(company => company.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (companyName is null)
        {
            return NotFound();
        }

        var inspection = await _dbContext.Inspections
            .AsNoTracking()
            .ForCompany(companyId)
            .Include(item => item.Customer)
                .ThenInclude(customer => customer!.BillAddress)
            .Include(item => item.Property)
                .ThenInclude(property => property.Address)
            .Include(item => item.Property)
                .ThenInclude(property => property.Buildings)
                    .ThenInclude(building => building.BuildingType)
            .Include(item => item.Property)
                .ThenInclude(property => property.Buildings)
                    .ThenInclude(building => building.Areas)
                        .ThenInclude(area => area.AreaType)
            .Include(item => item.Property)
                .ThenInclude(property => property.Areas)
                    .ThenInclude(area => area.AreaType)
            .FirstOrDefaultAsync(item => item.Id == inspectionId, cancellationToken);

        if (inspection is null)
        {
            return NotFound();
        }

        return View("InspectionDetail", new SuperAdminInspectionDetailViewModel
        {
            RenoCompanyID = companyId,
            CompanyName = companyName,
            Inspection = inspection,
            PropertyAddress = FormatAddress(inspection.Property.Address),
            CustomerName = FormatCustomerName(inspection.Customer),
            CustomerAddress = FormatAddress(inspection.Customer?.BillAddress)
        });
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/Inspectors")]
    public Task<IActionResult> CompanyInspectors(Guid companyId, int page = 1, CancellationToken cancellationToken = default)
    {
        return CompanyTable(companyId, "Inspectors", "Inspectors", nameof(CompanyInspectors), page, cancellationToken);
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/Mileage")]
    public Task<IActionResult> CompanyMileage(Guid companyId, int page = 1, CancellationToken cancellationToken = default)
    {
        return CompanyTable(companyId, "Mileage", "MileageTracking", nameof(CompanyMileage), page, cancellationToken);
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/Properties")]
    public Task<IActionResult> CompanyProperties(Guid companyId, int page = 1, CancellationToken cancellationToken = default)
    {
        return CompanyTable(companyId, "Property", "Property", nameof(CompanyProperties), page, cancellationToken);
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/Users")]
    public Task<IActionResult> CompanyUsersTable(Guid companyId, int page = 1, CancellationToken cancellationToken = default)
    {
        return CompanyTable(companyId, "Users", "RenoUser", nameof(CompanyUsersTable), page, cancellationToken);
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/Users/Add")]
    public async Task<IActionResult> AddCompanyUser(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.RenoCompanies.AnyAsync(company => company.RenoCompanyID == companyId, cancellationToken))
        {
            return NotFound();
        }

        return View("User", await BuildUserEditViewModelAsync(companyId, null, cancellationToken));
    }

    [HttpPost("SuperAdmin/Companies/{companyId:guid}/Users/Add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCompanyUser(Guid companyId, SuperAdminUserEditViewModel model, CancellationToken cancellationToken)
    {
        model.RenoCompanyID = companyId;

        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Password is required for new users.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateUserEditOptionsAsync(model, cancellationToken);
            return View("User", model);
        }

        if (await _dbContext.RenoUsers.AnyAsync(user => user.Login == model.Login.Trim(), cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Login), "A user with this login already exists.");
            await PopulateUserEditOptionsAsync(model, cancellationToken);
            return View("User", model);
        }

        var user = new RenoUser
        {
            RenoCompanyID = companyId,
            Login = Clean(model.Login),
            Password = _passwordService.HashPassword(model.Password!),
            FirstName = Clean(model.FirstName),
            LastName = Clean(model.LastName),
            Email = Clean(model.Email),
            PhonePrimary = Clean(model.PhonePrimary),
            PhoneSecondary = Clean(model.PhoneSecondary),
            Active = model.Active,
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow
        };

        _dbContext.RenoUsers.Add(user);
        ApplyUserRoles(user, model.SelectedRoleIDs);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(EditCompany), new { id = companyId });
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/Users/{userId:guid}")]
    public async Task<IActionResult> EditCompanyUser(Guid companyId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.RenoUsers
            .AsNoTracking()
            .Include(item => item.UserRoles)
            .FirstOrDefaultAsync(item => item.UserID == userId && item.RenoCompanyID == companyId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        return View("User", await BuildUserEditViewModelAsync(companyId, user, cancellationToken));
    }

    [HttpPost("SuperAdmin/Companies/{companyId:guid}/Users/{userId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCompanyUser(Guid companyId, Guid userId, SuperAdminUserEditViewModel model, CancellationToken cancellationToken)
    {
        model.UserID = userId;
        model.RenoCompanyID = companyId;

        if (!ModelState.IsValid)
        {
            await PopulateUserEditOptionsAsync(model, cancellationToken);
            return View("User", model);
        }

        var user = await _dbContext.RenoUsers
            .Include(item => item.UserRoles)
            .FirstOrDefaultAsync(item => item.UserID == userId && item.RenoCompanyID == companyId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var normalizedLogin = Clean(model.Login);
        if (await _dbContext.RenoUsers.AnyAsync(item => item.UserID != userId && item.Login == normalizedLogin, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Login), "A user with this login already exists.");
            await PopulateUserEditOptionsAsync(model, cancellationToken);
            return View("User", model);
        }

        ApplyUserUpdate(user, model);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(EditCompany), new { id = companyId });
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/Users/Attach")]
    public async Task<IActionResult> AttachCompanyUser(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.RenoCompanies.AnyAsync(company => company.RenoCompanyID == companyId, cancellationToken))
        {
            return NotFound();
        }

        return View("AttachUser", await BuildAttachUserViewModelAsync(companyId, cancellationToken));
    }

    [HttpPost("SuperAdmin/Companies/{companyId:guid}/Users/Attach")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AttachCompanyUser(Guid companyId, SuperAdminAttachUserViewModel model, CancellationToken cancellationToken)
    {
        model.RenoCompanyID = companyId;

        if (!ModelState.IsValid || model.UserID is null)
        {
            model.AvailableUsers = (await BuildAttachUserViewModelAsync(companyId, cancellationToken)).AvailableUsers;
            return View("AttachUser", model);
        }

        var user = await _dbContext.RenoUsers.FirstOrDefaultAsync(item => item.UserID == model.UserID.Value, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.RenoCompanyID = companyId;
        user.DateModified = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(EditCompany), new { id = companyId });
    }

    [HttpPost("SuperAdmin/Companies/{companyId:guid}/Users/{userId:guid}/Remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCompanyUser(Guid companyId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.RenoUsers
            .FirstOrDefaultAsync(item => item.UserID == userId && item.RenoCompanyID == companyId, cancellationToken);

        if (user is not null)
        {
            user.RenoCompanyID = null;
            user.DateModified = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return RedirectToAction(nameof(EditCompany), new { id = companyId });
    }

    [HttpGet("SuperAdmin/Database")]
    public async Task<IActionResult> Database(string? tableName, int page = 1, int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var tableNames = _databaseViewerService.GetTableNames();

        if (string.IsNullOrWhiteSpace(tableName))
        {
            return View("~/Views/Database/Index.cshtml", new DatabaseTablePageViewModel
            {
                TableNames = tableNames,
                Page = 1,
                PageSize = DefaultPageSize,
                TotalPages = 1
            });
        }

        var model = await _databaseViewerService.GetTablePageAsync(tableName, page, pageSize, cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        return View("~/Views/Database/Index.cshtml", model);
    }

    [HttpPost("SuperAdmin/Database/Clear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearDatabase(CancellationToken cancellationToken)
    {
        await _databaseViewerService.ClearDatabaseAsync(cancellationToken);
        TempData["DatabaseMessage"] = "Database data was cleared.";

        return RedirectToAction(nameof(Database));
    }

    [HttpPost("SuperAdmin/Database/ClearMileageTracking")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearMileageTracking(CancellationToken cancellationToken)
    {
        var result = await _databaseViewerService.ClearMileageTrackingAsync(cancellationToken);
        TempData["DatabaseMessage"] = $"Mileage tracking was cleared. Deleted {result.SessionsDeleted:N0} session row(s) and {result.WaypointsDeleted:N0} waypoint row(s).";

        return RedirectToAction(nameof(Database));
    }

    private static string Clean(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string FormatAddress(Address? address)
    {
        if (address is null)
        {
            return string.Empty;
        }

        var street = string.Join(" ", new[] { address.Street1, address.Street2, address.Street3 }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var cityState = string.Join(", ", new[] { address.City, address.State }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var cityStateZip = string.Join(" ", new[] { cityState, address.PostalCode }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.Join(" - ", new[] { street, cityStateZip }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string FormatCustomerName(Customer? customer)
    {
        if (customer is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(customer.DisplayName))
        {
            return customer.DisplayName;
        }

        var name = string.Join(" ", new[] { customer.GivenName, customer.FamilyName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.IsNullOrWhiteSpace(name) ? customer.CompanyName : name;
    }

    private async Task<IActionResult> CompanyTable(
        Guid companyId,
        string title,
        string tableName,
        string routeAction,
        int page,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.RenoCompanies.AnyAsync(company => company.RenoCompanyID == companyId, cancellationToken))
        {
            return NotFound();
        }

        var model = await _databaseViewerService.GetCompanyTablePageAsync(
            companyId,
            tableName,
            title,
            routeAction,
            page,
            DefaultPageSize,
            cancellationToken);

        return model is null
            ? NotFound()
            : View("CompanyTable", model);
    }

    private async Task SeedCompanyLookupTablesAsync(Guid newRenoCompanyID, CancellationToken cancellationToken)
    {
        var buildingTypes = await _dbContext.BuildingTypes
            .AsNoTracking()
            .Where(item => item.RenoCompanyID == TemplateRenoCompanyID)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        foreach (var buildingType in buildingTypes)
        {
            _dbContext.BuildingTypes.Add(new BuildingType
            {
                Id = Guid.NewGuid(),
                RenoCompanyID = newRenoCompanyID,
                Name = buildingType.Name
            });
        }

        var templateCategories = await _dbContext.InspectionAreaCategories
            .AsNoTracking()
            .Where(item => item.RenoCompanyID == TemplateRenoCompanyID)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var categoryIdMap = new Dictionary<Guid, Guid>();
        foreach (var category in templateCategories)
        {
            var newCategoryId = Guid.NewGuid();
            categoryIdMap[category.Id] = newCategoryId;

            _dbContext.InspectionAreaCategories.Add(new InspectionAreaCategory
            {
                Id = newCategoryId,
                RenoCompanyID = newRenoCompanyID,
                Name = category.Name,
                SortOrder = category.SortOrder
            });
        }

        var templateAreaTypes = await _dbContext.InspectionAreaTypes
            .AsNoTracking()
            .Where(item => item.RenoCompanyID == TemplateRenoCompanyID)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        foreach (var areaType in templateAreaTypes)
        {
            if (!categoryIdMap.TryGetValue(areaType.CategoryId, out var newCategoryId))
            {
                continue;
            }

            _dbContext.InspectionAreaTypes.Add(new InspectionAreaType
            {
                AreaTypeId = Guid.NewGuid(),
                RenoCompanyID = newRenoCompanyID,
                CategoryId = newCategoryId,
                Name = areaType.Name,
                SortOrder = areaType.SortOrder
            });
        }

        var templatePartSources = await _dbContext.PartSources
            .AsNoTracking()
            .Where(item => item.RenoCompanyID == TemplateRenoCompanyID)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var partSourceIdMap = new Dictionary<Guid, Guid>();
        foreach (var partSource in templatePartSources)
        {
            var newPartSourceId = Guid.NewGuid();
            partSourceIdMap[partSource.PartSourceId] = newPartSourceId;

            _dbContext.PartSources.Add(new PartSource
            {
                PartSourceId = newPartSourceId,
                RenoCompanyID = newRenoCompanyID,
                Name = partSource.Name
            });
        }

        var templateParts = await _dbContext.Parts
            .AsNoTracking()
            .Where(item => item.RenoCompanyID == TemplateRenoCompanyID)
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Sku)
            .ToListAsync(cancellationToken);

        foreach (var part in templateParts)
        {
            if (!partSourceIdMap.TryGetValue(part.PartSourceId, out var newPartSourceId))
            {
                continue;
            }

            _dbContext.Parts.Add(new Part
            {
                PartId = Guid.NewGuid(),
                RenoCompanyID = newRenoCompanyID,
                PartSourceId = newPartSourceId,
                Name = part.Name,
                Description = part.Description,
                ModelNumber = part.ModelNumber,
                Manufacturer = part.Manufacturer,
                Sku = part.Sku,
                Url = part.Url,
                ImageUrl = part.ImageUrl,
                Cost = part.Cost,
                IsPackage = part.IsPackage,
                PackageUnits = part.PackageUnits
            });
        }
    }

    private async Task<IReadOnlyList<SuperAdminCompanyUserRowViewModel>> GetCompanyUsersAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var users = await _dbContext.RenoUsers
            .AsNoTracking()
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .Where(user => user.RenoCompanyID == companyId)
            .OrderBy(user => user.Login)
            .ToListAsync(cancellationToken);

        return users
            .Select(user => new SuperAdminCompanyUserRowViewModel
            {
                UserID = user.UserID,
                Login = user.Login,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhonePrimary = user.PhonePrimary,
                Active = user.Active,
                Roles = string.Join(", ", user.UserRoles
                    .Where(userRole => userRole.Role != null)
                    .Select(userRole => userRole.Role!.Name)
                    .OrderBy(roleName => roleName))
            })
            .ToList();
    }

    private async Task<IReadOnlyList<SuperAdminRoleOptionViewModel>> GetRoleOptionsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new SuperAdminRoleOptionViewModel
            {
                RoleID = role.RoleID,
                Name = role.Name
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<SuperAdminUserEditViewModel> BuildUserEditViewModelAsync(Guid companyId, RenoUser? user, CancellationToken cancellationToken)
    {
        return new SuperAdminUserEditViewModel
        {
            UserID = user?.UserID,
            RenoCompanyID = companyId,
            RenoCompanyName = await _dbContext.RenoCompanies
                .Where(company => company.RenoCompanyID == companyId)
                .Select(company => company.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty,
            Login = user?.Login ?? string.Empty,
            FirstName = user?.FirstName,
            LastName = user?.LastName,
            Email = user?.Email,
            PhonePrimary = user?.PhonePrimary,
            PhoneSecondary = user?.PhoneSecondary,
            Active = user?.Active ?? true,
            SelectedRoleIDs = user?.UserRoles.Select(userRole => userRole.RoleID).ToList() ?? [],
            AvailableRoles = await GetRoleOptionsAsync(cancellationToken)
        };
    }

    private async Task<SuperAdminUserEditViewModel> BuildGlobalUserEditViewModelAsync(RenoUser? user, CancellationToken cancellationToken)
    {
        return new SuperAdminUserEditViewModel
        {
            UserID = user?.UserID,
            RenoCompanyID = user?.RenoCompanyID,
            RenoCompanyName = user?.RenoCompany?.Name ?? "Unassigned",
            Login = user?.Login ?? string.Empty,
            FirstName = user?.FirstName,
            LastName = user?.LastName,
            Email = user?.Email,
            PhonePrimary = user?.PhonePrimary,
            PhoneSecondary = user?.PhoneSecondary,
            Active = user?.Active ?? true,
            SelectedRoleIDs = user?.UserRoles.Select(userRole => userRole.RoleID).ToList() ?? [],
            AvailableRoles = await GetRoleOptionsAsync(cancellationToken)
        };
    }

    private async Task<SuperAdminAttachUserViewModel> BuildAttachUserViewModelAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var users = await _dbContext.RenoUsers
            .AsNoTracking()
            .Include(user => user.RenoCompany)
            .Where(user => user.RenoCompanyID != companyId)
            .OrderBy(user => user.Login)
            .Select(user => new SuperAdminAttachUserOptionViewModel
            {
                UserID = user.UserID,
                Login = user.Login,
                Name = (user.FirstName + " " + user.LastName).Trim(),
                CompanyName = user.RenoCompany == null ? "Unassigned" : user.RenoCompany.Name
            })
            .ToListAsync(cancellationToken);

        return new SuperAdminAttachUserViewModel
        {
            RenoCompanyID = companyId,
            AvailableUsers = users
        };
    }

    private async Task PopulateUserEditOptionsAsync(SuperAdminUserEditViewModel model, CancellationToken cancellationToken)
    {
        model.AvailableRoles = await GetRoleOptionsAsync(cancellationToken);
        model.RenoCompanyName = model.RenoCompanyID is Guid companyId
            ? await _dbContext.RenoCompanies
                .Where(company => company.RenoCompanyID == companyId)
                .Select(company => company.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "Unassigned"
            : "Unassigned";
    }

    private void ApplyUserRoles(RenoUser user, IEnumerable<Guid> roleIDs)
    {
        foreach (var roleID in roleIDs.Distinct())
        {
            user.UserRoles.Add(new UserRole
            {
                UserID = user.UserID,
                RoleID = roleID
            });
        }
    }

    private void ApplyUserUpdate(RenoUser user, SuperAdminUserEditViewModel model)
    {
        user.Login = Clean(model.Login);
        user.FirstName = Clean(model.FirstName);
        user.LastName = Clean(model.LastName);
        user.Email = Clean(model.Email);
        user.PhonePrimary = Clean(model.PhonePrimary);
        user.PhoneSecondary = Clean(model.PhoneSecondary);
        user.Active = model.Active;
        user.DateModified = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            user.Password = _passwordService.HashPassword(model.Password);
        }

        user.UserRoles.Clear();
        ApplyUserRoles(user, model.SelectedRoleIDs);
    }

    private static SuperAdminUserRowViewModel ToUserRowViewModel(RenoUser user)
    {
        return new SuperAdminUserRowViewModel
        {
            UserID = user.UserID,
            Login = user.Login,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhonePrimary = user.PhonePrimary,
            CompanyName = user.RenoCompany?.Name ?? "Unassigned",
            Active = user.Active,
            Roles = string.Join(", ", user.UserRoles
                .Where(userRole => userRole.Role != null)
                .Select(userRole => userRole.Role!.Name)
                .OrderBy(roleName => roleName))
        };
    }
}
