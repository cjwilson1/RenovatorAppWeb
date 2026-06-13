using System.Security.Claims;
using System.Security.Cryptography;
using System.Diagnostics;
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
    private readonly RequestDiagnosticsService _requestDiagnosticsService;
    private readonly NewEmployeeEmailService _newEmployeeEmailService;
    private readonly IConfiguration _configuration;

    public SuperAdminController(
        DatabaseViewerService databaseViewerService,
        RenovatorAppDbContext dbContext,
        PasswordService passwordService,
        CurrentUserSession currentUserSession,
        RequestDiagnosticsService requestDiagnosticsService,
        NewEmployeeEmailService newEmployeeEmailService,
        IConfiguration configuration)
    {
        _databaseViewerService = databaseViewerService;
        _dbContext = dbContext;
        _passwordService = passwordService;
        _currentUserSession = currentUserSession;
        _requestDiagnosticsService = requestDiagnosticsService;
        _newEmployeeEmailService = newEmployeeEmailService;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var diagnostics = _requestDiagnosticsService.GetSnapshot();
        var databaseStopwatch = Stopwatch.StartNew();
        var databaseAvailable = true;
        var databaseStatusMessage = "OK";
        long? databaseLatencyMilliseconds = null;

        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            databaseStopwatch.Stop();
            databaseLatencyMilliseconds = databaseStopwatch.ElapsedMilliseconds;
        }
        catch (Exception exception)
        {
            databaseStopwatch.Stop();
            databaseAvailable = false;
            databaseStatusMessage = $"{exception.GetType().Name}: {exception.Message}";
        }

        ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out _);
        ThreadPool.GetMaxThreads(out var maxWorkerThreads, out _);

        var process = Process.GetCurrentProcess();

        return View(new SuperAdminIndexViewModel
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            StartedAtUtc = diagnostics.StartedAtUtc,
            Uptime = DateTimeOffset.UtcNow - diagnostics.StartedAtUtc,
            DatabaseAvailable = databaseAvailable,
            DatabaseLatencyMilliseconds = databaseLatencyMilliseconds,
            DatabaseStatusMessage = databaseStatusMessage,
            ActiveRequests = diagnostics.ActiveRequests,
            AllRequests = ToTimingViewModel(diagnostics.AllRequests),
            ApiRequests = ToTimingViewModel(diagnostics.ApiRequests),
            SlowRequests = diagnostics.SlowRequests.Select(ToRequestRowViewModel).ToList(),
            RecentRequests = diagnostics.RecentRequests.Select(ToRequestRowViewModel).ToList(),
            ProcessMemoryMegabytes = process.WorkingSet64 / 1024d / 1024d,
            ThreadPoolAvailableWorkerThreads = availableWorkerThreads,
            ThreadPoolMaxWorkerThreads = maxWorkerThreads
        });
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

    [HttpPost("SuperAdmin/Users/{id:guid}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await _dbContext.RenoUsers
            .FirstOrDefaultAsync(item => item.UserID == id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        if (id == _currentUserSession.UserID)
        {
            TempData["SuperAdminUsersMessage"] = "You cannot delete the currently signed-in user.";
            return RedirectToAction(nameof(EditUser), new { id });
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.CalendarEvents
            .Where(calendarEvent => calendarEvent.RenoUserID == id)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.UserRoles
            .Where(userRole => userRole.UserID == id)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.UserInvitations
            .Where(invitation => invitation.UserID == id)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.UserInvitations
            .Where(invitation => invitation.CreatedByUserID == id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(invitation => invitation.CreatedByUserID, (Guid?)null),
                cancellationToken);

        var userEmail = Clean(user.Email);
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            userEmail = Clean(user.Login);
        }

        if (user.RenoCompanyID is Guid companyId && !string.IsNullOrWhiteSpace(userEmail))
        {
            var employees = await _dbContext.Employees
                .Include(employee => employee.PrimaryAddress)
                .Where(employee => employee.RenoCompanyID == companyId
                    && employee.PrimaryEmailAddress.ToLower() == userEmail.ToLower())
                .ToListAsync(cancellationToken);

            foreach (var employee in employees)
            {
                var address = employee.PrimaryAddress;
                var addressId = employee.PrimaryAddressId;

                _dbContext.Employees.Remove(employee);

                if (address is not null
                    && addressId is Guid primaryAddressId
                    && await CanDeleteEmployeeAddressAsync(companyId, employee.EmployeeId, primaryAddressId, cancellationToken))
                {
                    _dbContext.Addresses.Remove(address);
                }
            }
        }

        _dbContext.RenoUsers.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["SuperAdminUsersMessage"] = "User deleted.";
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

        await ClearCompanyDataAsync(id, deleteLookupTables: true, deleteUsers: true, cancellationToken);
        await _dbContext.Parts.Where(part => part.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PartSources.Where(source => source.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.AppSettings.Where(setting => setting.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.RenoCompanies.Where(company => company.RenoCompanyID == id).ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return RedirectToAction(nameof(Companies));
    }

    [HttpPost("SuperAdmin/Companies/{id:guid}/Reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetCompany(Guid id, CancellationToken cancellationToken)
    {
        var companyExists = await _dbContext.RenoCompanies
            .AnyAsync(company => company.RenoCompanyID == id, cancellationToken);

        if (!companyExists)
        {
            return RedirectToAction(nameof(Companies));
        }

        if (id == TemplateRenoCompanyID)
        {
            TempData["SuperAdminCompaniesMessage"] = "The RenovatorApp template company cannot be reset.";
            return RedirectToAction(nameof(EditCompany), new { id });
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await ClearCompanyDataAsync(id, deleteLookupTables: true, deleteUsers: true, cancellationToken);
        await SeedResetLookupTablesAsync(id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        TempData["SuperAdminCompaniesMessage"] = "Company data was reset.";
        return RedirectToAction(nameof(EditCompany), new { id });
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/BuildingTypes")]
    public Task<IActionResult> CompanyBuildingTypes(Guid companyId, int page = 1, CancellationToken cancellationToken = default)
    {
        return CompanyTable(companyId, "Building Type", "BuildingType", nameof(CompanyBuildingTypes), page, cancellationToken);
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/Areas")]
    public Task<IActionResult> CompanyAreas(Guid companyId, int page = 1, CancellationToken cancellationToken = default)
    {
        return CompanyTable(companyId, "Inspection Areas", "InspectionAreaType", nameof(CompanyAreas), page, cancellationToken);
    }

    [HttpPost("SuperAdmin/Companies/{companyId:guid}/BuildingTypes/AddDefaults")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDefaultBuildingTypes(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.RenoCompanies.AnyAsync(company => company.RenoCompanyID == companyId, cancellationToken))
        {
            return NotFound();
        }

        var defaultBuildingTypes = await _dbContext.BuildingTypes
            .AsNoTracking()
            .Where(item => item.RenoCompanyID == TemplateRenoCompanyID)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var existingNames = await _dbContext.BuildingTypes
            .Where(item => item.RenoCompanyID == companyId)
            .Select(item => item.Name)
            .ToListAsync(cancellationToken);
        var existingNameSet = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var buildingType in defaultBuildingTypes)
        {
            var name = Clean(buildingType.Name);
            if (string.IsNullOrWhiteSpace(name) || existingNameSet.Contains(name))
            {
                continue;
            }

            _dbContext.BuildingTypes.Add(new BuildingType
            {
                BuildingTypeId = Guid.NewGuid(),
                RenoCompanyID = companyId,
                Name = name
            });
            existingNameSet.Add(name);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(CompanyBuildingTypes), new { companyId });
    }

    [HttpPost("SuperAdmin/Companies/{companyId:guid}/Areas/AddDefaults")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDefaultAreas(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.RenoCompanies.AnyAsync(company => company.RenoCompanyID == companyId, cancellationToken))
        {
            return NotFound();
        }

        await UpsertDefaultInspectionAreasAsync(companyId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(CompanyAreas), new { companyId });
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

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/Employees/New")]
    public async Task<IActionResult> NewCompanyEmployee(Guid companyId, CancellationToken cancellationToken)
    {
        var model = await BuildNewEmployeeViewModelAsync(companyId, null, cancellationToken);
        return model is null ? NotFound() : View("NewEmployee", model);
    }

    [HttpGet("SuperAdmin/Companies/{companyId:guid}/Employees/{employeeId:guid}")]
    public async Task<IActionResult> EditCompanyEmployee(Guid companyId, Guid employeeId, CancellationToken cancellationToken)
    {
        var model = await BuildEditEmployeeViewModelAsync(companyId, employeeId, null, cancellationToken);
        return model is null ? NotFound() : View("NewEmployee", model);
    }

    [HttpPost("SuperAdmin/Companies/{companyId:guid}/Employees/New")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewCompanyEmployee(
        Guid companyId,
        SuperAdminNewEmployeeViewModel model,
        CancellationToken cancellationToken)
    {
        model.RenoCompanyID = companyId;

        var role = model.SelectedRoleID is Guid selectedRoleId
            ? await _dbContext.Roles.FirstOrDefaultAsync(
                item => item.RoleID == selectedRoleId
                    && (item.Name == "User" || item.Name == "Admin"),
                cancellationToken)
            : null;

        if (role is null)
        {
            ModelState.AddModelError(nameof(model.SelectedRoleID), "Select User or Admin.");
        }

        var normalizedEmail = Clean(model.PrimaryEmailAddress);
        var normalizedEmailLookup = normalizedEmail.ToLowerInvariant();
        var existingUser = string.IsNullOrWhiteSpace(normalizedEmail)
            ? null
            : await _dbContext.RenoUsers
                .Include(user => user.UserRoles)
                .FirstOrDefaultAsync(
                    user => user.Login.ToLower() == normalizedEmailLookup
                        || user.Email.ToLower() == normalizedEmailLookup,
                    cancellationToken);

        if (existingUser?.RenoCompanyID is Guid existingCompanyId && existingCompanyId != companyId)
        {
            model.DialogTitle = "Cannot Add Employee";
            model.DialogMessage = "The email address you entered is already a user in the RenovatorApp but they are connected to a different company.  User's cannot belong to more that one company";
            var conflictModel = await BuildNewEmployeeViewModelAsync(companyId, model, cancellationToken);
            return conflictModel is null ? NotFound() : View("NewEmployee", conflictModel);
        }

        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildNewEmployeeViewModelAsync(companyId, model, cancellationToken);
            return invalidModel is null ? NotFound() : View("NewEmployee", invalidModel);
        }

        var companyName = await _dbContext.RenoCompanies
            .Where(company => company.RenoCompanyID == companyId)
            .Select(company => company.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (companyName is null)
        {
            return NotFound();
        }

        var expirationHours = await GetNewUserTokenExpirationHoursAsync(cancellationToken);
        var now = DateTime.UtcNow;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var address = new Address
        {
            RenoCompanyID = companyId,
            Street1 = Clean(model.Street1),
            Street2 = Clean(model.Street2),
            Street3 = Clean(model.Street3),
            City = Clean(model.City),
            State = Clean(model.State),
            PostalCode = Clean(model.PostalCode)
        };

        _dbContext.Addresses.Add(address);

        var firstName = Clean(model.GivenName);
        var lastName = Clean(model.FamilyName);
        var displayName = FormatPersonName(firstName, lastName);

        var employee = new Employee
        {
            RenoCompanyID = companyId,
            Title = Clean(model.Title),
            GivenName = firstName,
            MiddleName = Clean(model.MiddleName),
            FamilyName = lastName,
            DisplayName = displayName,
            PrintOnCheckName = displayName,
            PrimaryEmailAddress = normalizedEmail,
            PrimaryPhone = Clean(model.PrimaryPhone),
            MobilePhone = Clean(model.MobilePhone),
            EmployeeNumber = Clean(model.EmployeeNumber),
            Active = model.Active,
            PrimaryAddress = address,
            CreatedDate = now,
            LastEditDate = now
        };

        var user = existingUser;
        if (user is null)
        {
            user = new RenoUser
            {
                RenoCompanyID = companyId,
                Login = normalizedEmail,
                Password = _passwordService.HashPassword(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")),
                FirstName = firstName,
                LastName = lastName,
                Email = normalizedEmail,
                PhonePrimary = Clean(model.PrimaryPhone),
                PhoneSecondary = Clean(model.MobilePhone),
                Active = false,
                DateCreated = now,
                DateModified = now
            };

            _dbContext.RenoUsers.Add(user);
        }
        else
        {
            user.RenoCompanyID = companyId;
            user.FirstName = string.IsNullOrWhiteSpace(user.FirstName) ? firstName : user.FirstName;
            user.LastName = string.IsNullOrWhiteSpace(user.LastName) ? lastName : user.LastName;
            user.Email = string.IsNullOrWhiteSpace(user.Email) ? normalizedEmail : user.Email;
            user.PhonePrimary = string.IsNullOrWhiteSpace(user.PhonePrimary) ? Clean(model.PrimaryPhone) : user.PhonePrimary;
            user.PhoneSecondary = string.IsNullOrWhiteSpace(user.PhoneSecondary) ? Clean(model.MobilePhone) : user.PhoneSecondary;
            user.DateModified = now;
        }

        _dbContext.Employees.Add(employee);
        if (!user.UserRoles.Any(userRole => userRole.RoleID == role!.RoleID))
        {
            user.UserRoles.Add(new UserRole
            {
                UserID = user.UserID,
                RoleID = role!.RoleID
            });
        }

        UserInvitation? invitation = null;
        string? invitationToken = null;
        if (model.SendInviteEmail)
        {
            await RevokePendingInvitationsAsync(user.UserID, now, cancellationToken);
            invitationToken = GenerateInvitationToken();
            invitation = new UserInvitation
            {
                UserID = user.UserID,
                TokenHash = HashInvitationToken(invitationToken),
                SentToEmail = normalizedEmail,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddHours(expirationHours),
                CreatedByUserID = _currentUserSession.UserID
            };

            _dbContext.UserInvitations.Add(invitation);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (invitation is null || string.IsNullOrWhiteSpace(invitationToken))
        {
            TempData["SuperAdminEmployeesMessage"] = "Employee saved.";
            return RedirectToAction(nameof(CompanyEmployees), new { companyId });
        }

        var inviteLink = BuildInviteLink(invitation.UserInvitationId, invitationToken);

        try
        {
            await _newEmployeeEmailService.SendWelcomeEmailAsync(
                normalizedEmail,
                firstName,
                companyName,
                inviteLink,
                expirationHours,
                cancellationToken);

            TempData["SuperAdminEmployeesMessage"] = "Employee saved and welcome email sent.";
        }
        catch (Exception exception)
        {
            TempData["SuperAdminEmployeesMessage"] = $"Employee saved, but the welcome email failed to send: {exception.Message}";
        }

        return RedirectToAction(nameof(CompanyEmployees), new { companyId });
    }

    [HttpPost("SuperAdmin/Companies/{companyId:guid}/Employees/{employeeId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCompanyEmployee(
        Guid companyId,
        Guid employeeId,
        SuperAdminNewEmployeeViewModel model,
        CancellationToken cancellationToken)
    {
        model.EmployeeId = employeeId;
        model.RenoCompanyID = companyId;

        var role = model.SelectedRoleID is Guid selectedRoleId
            ? await _dbContext.Roles.FirstOrDefaultAsync(
                item => item.RoleID == selectedRoleId
                    && (item.Name == "User" || item.Name == "Admin"),
                cancellationToken)
            : null;

        if (role is null)
        {
            ModelState.AddModelError(nameof(model.SelectedRoleID), "Select User or Admin.");
        }

        var normalizedEmail = Clean(model.PrimaryEmailAddress);
        var existingUser = await FindUserByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser?.RenoCompanyID is Guid existingCompanyId && existingCompanyId != companyId)
        {
            model.DialogTitle = "Cannot Add Employee";
            model.DialogMessage = "The email address you entered is already a user in the RenovatorApp but they are connected to a different company.  User's cannot belong to more that one company";
            var conflictModel = await BuildEditEmployeeViewModelAsync(companyId, employeeId, model, cancellationToken);
            return conflictModel is null ? NotFound() : View("NewEmployee", conflictModel);
        }

        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildEditEmployeeViewModelAsync(companyId, employeeId, model, cancellationToken);
            return invalidModel is null ? NotFound() : View("NewEmployee", invalidModel);
        }

        var employee = await _dbContext.Employees
            .Include(item => item.PrimaryAddress)
            .FirstOrDefaultAsync(item => item.EmployeeId == employeeId && item.RenoCompanyID == companyId, cancellationToken);

        if (employee is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        ApplyEmployeeUpdate(employee, model, now);
        TrackEmployeeAddress(employee, model);

        var firstName = Clean(model.GivenName);
        var lastName = Clean(model.FamilyName);
        var user = existingUser;
        if (user is null)
        {
            user = new RenoUser
            {
                RenoCompanyID = companyId,
                Login = normalizedEmail,
                Password = _passwordService.HashPassword(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")),
                FirstName = firstName,
                LastName = lastName,
                Email = normalizedEmail,
                PhonePrimary = Clean(model.PrimaryPhone),
                PhoneSecondary = Clean(model.MobilePhone),
                Active = false,
                DateCreated = now,
                DateModified = now
            };

            _dbContext.RenoUsers.Add(user);
        }
        else
        {
            user.RenoCompanyID = companyId;
            user.Login = string.IsNullOrWhiteSpace(user.Login) ? normalizedEmail : user.Login;
            user.Email = normalizedEmail;
            user.FirstName = firstName;
            user.LastName = lastName;
            user.PhonePrimary = Clean(model.PrimaryPhone);
            user.PhoneSecondary = Clean(model.MobilePhone);
            user.DateModified = now;
        }

        ApplyEmployeeUserRole(user, role!);

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["SuperAdminEmployeesMessage"] = "Employee updated.";
        return RedirectToAction(nameof(CompanyEmployees), new { companyId });
    }

    [HttpPost("SuperAdmin/Companies/{companyId:guid}/Employees/{employeeId:guid}/SendInvite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendCompanyEmployeeInvite(
        Guid companyId,
        Guid employeeId,
        SuperAdminNewEmployeeViewModel model,
        CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.EmployeeId == employeeId && item.RenoCompanyID == companyId, cancellationToken);

        if (employee is null)
        {
            return NotFound();
        }

        var role = model.SelectedRoleID is Guid selectedRoleId
            ? await _dbContext.Roles.FirstOrDefaultAsync(
                item => item.RoleID == selectedRoleId
                    && (item.Name == "User" || item.Name == "Admin"),
                cancellationToken)
            : null;

        if (role is null)
        {
            model.EmployeeId = employeeId;
            model.RenoCompanyID = companyId;
            ModelState.AddModelError(nameof(model.SelectedRoleID), "Select User or Admin before sending an invite.");
            var invalidModel = await BuildEditEmployeeViewModelAsync(companyId, employeeId, model, cancellationToken);
            return invalidModel is null ? NotFound() : View("NewEmployee", invalidModel);
        }

        var user = await FindUserByEmailAsync(employee.PrimaryEmailAddress, cancellationToken);
        if (user?.RenoCompanyID is Guid existingCompanyId && existingCompanyId != companyId)
        {
            model.EmployeeId = employeeId;
            model.RenoCompanyID = companyId;
            model.DialogTitle = "Cannot Add Employee";
            model.DialogMessage = "The email address you entered is already a user in the RenovatorApp but they are connected to a different company.  User's cannot belong to more that one company";
            var conflictModel = await BuildEditEmployeeViewModelAsync(companyId, employeeId, model, cancellationToken);
            return conflictModel is null ? NotFound() : View("NewEmployee", conflictModel);
        }

        if (user is null)
        {
            var now = DateTime.UtcNow;
            user = new RenoUser
            {
                RenoCompanyID = companyId,
                Login = Clean(employee.PrimaryEmailAddress),
                Password = _passwordService.HashPassword(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")),
                FirstName = Clean(employee.GivenName),
                LastName = Clean(employee.FamilyName),
                Email = Clean(employee.PrimaryEmailAddress),
                PhonePrimary = Clean(employee.PrimaryPhone),
                PhoneSecondary = Clean(employee.MobilePhone),
                Active = false,
                DateCreated = now,
                DateModified = now
            };

            _dbContext.RenoUsers.Add(user);
        }
        else
        {
            user.RenoCompanyID = companyId;
        }

        ApplyEmployeeUserRole(user, role);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await CreateAndSendEmployeeInvitationAsync(companyId, user, employee.GivenName, employee.PrimaryEmailAddress, cancellationToken);

        return RedirectToAction(nameof(CompanyEmployees), new { companyId });
    }

    [HttpPost("SuperAdmin/Companies/{companyId:guid}/Employees/{employeeId:guid}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCompanyEmployee(
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees
            .Include(item => item.PrimaryAddress)
            .FirstOrDefaultAsync(
                item => item.EmployeeId == employeeId && item.RenoCompanyID == companyId,
                cancellationToken);

        if (employee is null)
        {
            return NotFound();
        }

        var employeeEmail = Clean(employee.PrimaryEmailAddress);
        var address = employee.PrimaryAddress;
        var addressId = employee.PrimaryAddressId;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(employeeEmail))
        {
            var inactiveUser = await _dbContext.RenoUsers
                .Include(user => user.UserRoles)
                .FirstOrDefaultAsync(
                    user => user.RenoCompanyID == companyId
                        && !user.Active
                        && user.DateLastLogin == null
                        && (user.Login == employeeEmail || user.Email == employeeEmail),
                    cancellationToken);

            if (inactiveUser is not null)
            {
                _dbContext.UserRoles.RemoveRange(inactiveUser.UserRoles);
                _dbContext.RenoUsers.Remove(inactiveUser);
            }
        }

        _dbContext.Employees.Remove(employee);

        if (address is not null
            && addressId is Guid primaryAddressId
            && await CanDeleteEmployeeAddressAsync(companyId, employeeId, primaryAddressId, cancellationToken))
        {
            _dbContext.Addresses.Remove(address);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["SuperAdminEmployeesMessage"] = "Employee deleted.";
        return RedirectToAction(nameof(CompanyEmployees), new { companyId });
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
            .FirstOrDefaultAsync(item => item.InspectionId == inspectionId, cancellationToken);

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

    [HttpPost("SuperAdmin/Companies/{companyId:guid}/Users/AddFromTable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCompanyUserFromTable(Guid companyId, SuperAdminUserEditViewModel model, CancellationToken cancellationToken)
    {
        if (!await _dbContext.RenoCompanies.AnyAsync(company => company.RenoCompanyID == companyId, cancellationToken))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(model.Login)
            || string.IsNullOrWhiteSpace(model.Password)
            || string.IsNullOrWhiteSpace(model.FirstName)
            || string.IsNullOrWhiteSpace(model.LastName))
        {
            TempData["SuperAdminUsersMessage"] = "New user failed. Login, password, first name, and last name are required.";
            return RedirectToAction(nameof(CompanyUsersTable), new { companyId });
        }

        if (await _dbContext.RenoUsers.AnyAsync(user => user.Login == model.Login.Trim(), cancellationToken))
        {
            TempData["SuperAdminUsersMessage"] = "New user failed. A user with this login already exists.";
            return RedirectToAction(nameof(CompanyUsersTable), new { companyId });
        }

        var now = DateTime.UtcNow;
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
            DateCreated = now,
            DateModified = now
        };

        _dbContext.RenoUsers.Add(user);
        ApplyUserRoles(user, model.SelectedRoleIDs);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(CompanyUsersTable), new { companyId });
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

    [HttpPost("SuperAdmin/Companies/{companyId:guid}/Users/{userId:guid}/UpdateFromTable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCompanyUserFromTable(
        Guid companyId,
        Guid userId,
        SuperAdminUserEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Login)
            || string.IsNullOrWhiteSpace(model.FirstName)
            || string.IsNullOrWhiteSpace(model.LastName))
        {
            TempData["SuperAdminUsersMessage"] = "Edit user failed. Login, first name, and last name are required.";
            return RedirectToAction(nameof(CompanyUsersTable), new { companyId });
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
            TempData["SuperAdminUsersMessage"] = "Edit user failed. A user with this login already exists.";
            return RedirectToAction(nameof(CompanyUsersTable), new { companyId });
        }

        user.Login = normalizedLogin;
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
        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(CompanyUsersTable), new { companyId });
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

    [HttpGet("SuperAdmin/Settings")]
    public async Task<IActionResult> Settings(CancellationToken cancellationToken = default)
    {
        return View(new SuperAdminSettingsViewModel
        {
            Settings = await GetDefaultSettingsRowsAsync(cancellationToken),
            StatusMessage = TempData["SuperAdminSettingsMessage"] as string ?? string.Empty
        });
    }

    [HttpPost("SuperAdmin/Settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(SuperAdminSettingsViewModel model, CancellationToken cancellationToken)
    {
        var normalizedSettings = model.Settings
            .Select(setting => new SuperAdminSettingEditViewModel
            {
                Name = Clean(setting.Name),
                Value = setting.Value ?? string.Empty
            })
            .Where(setting => !string.IsNullOrWhiteSpace(setting.Name))
            .GroupBy(setting => setting.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(setting => setting.Name)
            .ToList();

        var existingSettings = await _dbContext.AppSettings
            .Where(setting => setting.RenoCompanyID == TemplateRenoCompanyID)
            .ToListAsync(cancellationToken);
        var existingByName = existingSettings.ToDictionary(setting => setting.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var postedSetting in normalizedSettings)
        {
            if (!existingByName.TryGetValue(postedSetting.Name, out var setting))
            {
                setting = new AppSetting
                {
                    RenoCompanyID = TemplateRenoCompanyID,
                    Name = postedSetting.Name
                };
                _dbContext.AppSettings.Add(setting);
                existingByName[postedSetting.Name] = setting;
            }

            setting.Value = postedSetting.Value;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["SuperAdminSettingsMessage"] = "Settings saved.";

        return RedirectToAction(nameof(Settings));
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

    private async Task<int> GetNewUserTokenExpirationHoursAsync(CancellationToken cancellationToken)
    {
        var value = await _dbContext.AppSettings
            .AsNoTracking()
            .Where(setting => setting.RenoCompanyID == TemplateRenoCompanyID
                && setting.Name == "NewUserTokenExpiratinHours")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return int.TryParse(value, out var expirationHours) && expirationHours > 0
            ? expirationHours
            : 72;
    }

    private async Task RevokePendingInvitationsAsync(Guid userId, DateTime revokedAtUtc, CancellationToken cancellationToken)
    {
        var pendingInvitations = await _dbContext.UserInvitations
            .Where(invitation => invitation.UserID == userId
                && invitation.AcceptedAtUtc == null
                && invitation.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var invitation in pendingInvitations)
        {
            invitation.RevokedAtUtc = revokedAtUtc;
        }
    }

    private static string GenerateInvitationToken()
    {
        return Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    private static string HashInvitationToken(string token)
    {
        var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToBase64String(hashBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal);
    }

    private string BuildInviteLink(Guid invitationId, string invitationToken)
    {
        var path = Url.Action(
            "AcceptInvite",
            "Account",
            new { invitationId, token = invitationToken }) ?? "/Account/AcceptInvite";
        var baseUrl = _configuration["App:BaseUrl"]?.TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            return $"{baseUrl}{path}";
        }

        return Url.Action(
            "AcceptInvite",
            "Account",
            new { invitationId, token = invitationToken },
            Request.Scheme,
            Request.Host.Value) ?? path;
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

    private static string FormatPersonName(string firstName, string lastName)
    {
        return string.Join(" ", new[] { firstName, lastName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
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

    private async Task ClearCompanyDataAsync(
        Guid renoCompanyID,
        bool deleteLookupTables,
        bool deleteUsers,
        CancellationToken cancellationToken)
    {
        if (deleteUsers)
        {
            await _dbContext.CalendarEvents.Where(calendarEvent => calendarEvent.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);
            await _dbContext.UserRoles
                .Where(userRole => _dbContext.RenoUsers.Any(user => user.UserID == userRole.UserID && user.RenoCompanyID == renoCompanyID))
                .ExecuteDeleteAsync(cancellationToken);
            await _dbContext.RenoUsers.Where(user => user.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);
        }

        await _dbContext.InspectionAreaNotePhotos.Where(photo => photo.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.InspectionAreaNoteEstimateItems.Where(item => item.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.InspectionAreaNotes.Where(note => note.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.InspectionAreas.Where(area => area.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Buildings.Where(building => building.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);

        await _dbContext.MileageTrackingWaypoints.Where(waypoint => waypoint.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.MileageTracking.Where(session => session.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);

        await _dbContext.Documents.Where(document => document.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Inspections.Where(inspection => inspection.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             DELETE FROM "CustomerProperty"
             WHERE "CustomerId" IN (SELECT "CustomerId" FROM "Customer" WHERE "RenoCompanyID" = {renoCompanyID})
                OR "PropertyId" IN (SELECT "PropertyId" FROM "Property" WHERE "RenoCompanyID" = {renoCompanyID})
             """,
            cancellationToken);

        await _dbContext.Customers.Where(customer => customer.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Employees.Where(employee => employee.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Addresses.Where(address => address.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Properties.Where(property => property.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);

        if (deleteLookupTables)
        {
            await _dbContext.InspectionAreaTypes.Where(areaType => areaType.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);
            await _dbContext.InspectionAreaCategories.Where(category => category.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);
            await _dbContext.BuildingTypes.Where(buildingType => buildingType.RenoCompanyID == renoCompanyID).ExecuteDeleteAsync(cancellationToken);
        }
    }

    private async Task SeedResetLookupTablesAsync(Guid targetRenoCompanyID, CancellationToken cancellationToken)
    {
        var templateBuildingTypes = await _dbContext.BuildingTypes
            .AsNoTracking()
            .Where(item => item.RenoCompanyID == TemplateRenoCompanyID)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        foreach (var buildingType in templateBuildingTypes)
        {
            _dbContext.BuildingTypes.Add(new BuildingType
            {
                BuildingTypeId = Guid.NewGuid(),
                RenoCompanyID = targetRenoCompanyID,
                Name = buildingType.Name
            });
        }

        await UpsertDefaultInspectionAreasAsync(targetRenoCompanyID, cancellationToken);
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
                BuildingTypeId = Guid.NewGuid(),
                RenoCompanyID = newRenoCompanyID,
                Name = buildingType.Name
            });
        }

        await UpsertDefaultInspectionAreasAsync(newRenoCompanyID, cancellationToken);

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

    private async Task UpsertDefaultInspectionAreasAsync(Guid targetRenoCompanyID, CancellationToken cancellationToken)
    {
        var templateCategories = await _dbContext.InspectionAreaCategories
            .AsNoTracking()
            .Where(item => item.RenoCompanyID == TemplateRenoCompanyID)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var targetCategories = await _dbContext.InspectionAreaCategories
            .Where(item => item.RenoCompanyID == targetRenoCompanyID)
            .ToListAsync(cancellationToken);
        var targetCategoriesByName = targetCategories
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToDictionary(item => Clean(item.Name), StringComparer.OrdinalIgnoreCase);
        var categoryIdMap = new Dictionary<Guid, Guid>();

        foreach (var templateCategory in templateCategories)
        {
            var categoryName = Clean(templateCategory.Name);
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                continue;
            }

            if (!targetCategoriesByName.TryGetValue(categoryName, out var targetCategory))
            {
                targetCategory = new InspectionAreaCategory
                {
                    InspectionAreaCategoryId = Guid.NewGuid(),
                    RenoCompanyID = targetRenoCompanyID,
                    Name = categoryName
                };
                _dbContext.InspectionAreaCategories.Add(targetCategory);
                targetCategoriesByName[categoryName] = targetCategory;
            }

            targetCategory.SortOrder = templateCategory.SortOrder;
            categoryIdMap[templateCategory.InspectionAreaCategoryId] = targetCategory.InspectionAreaCategoryId;
        }

        var templateAreaTypes = await _dbContext.InspectionAreaTypes
            .AsNoTracking()
            .Where(item => item.RenoCompanyID == TemplateRenoCompanyID)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var targetAreaTypes = await _dbContext.InspectionAreaTypes
            .Where(item => item.RenoCompanyID == targetRenoCompanyID)
            .ToListAsync(cancellationToken);
        var targetAreaTypesByName = targetAreaTypes
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToDictionary(item => Clean(item.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var templateAreaType in templateAreaTypes)
        {
            var areaTypeName = Clean(templateAreaType.Name);
            if (string.IsNullOrWhiteSpace(areaTypeName)
                || !categoryIdMap.TryGetValue(templateAreaType.CategoryId, out var targetCategoryId))
            {
                continue;
            }

            if (!targetAreaTypesByName.TryGetValue(areaTypeName, out var targetAreaType))
            {
                targetAreaType = new InspectionAreaType
                {
                    InspectionAreaTypeId = Guid.NewGuid(),
                    RenoCompanyID = targetRenoCompanyID,
                    Name = areaTypeName
                };
                _dbContext.InspectionAreaTypes.Add(targetAreaType);
                targetAreaTypesByName[areaTypeName] = targetAreaType;
            }

            targetAreaType.CategoryId = targetCategoryId;
            targetAreaType.SortOrder = templateAreaType.SortOrder;
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

    private async Task<List<SuperAdminSettingEditViewModel>> GetDefaultSettingsRowsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.AppSettings
            .AsNoTracking()
            .Where(setting => setting.RenoCompanyID == TemplateRenoCompanyID)
            .OrderBy(setting => setting.Name)
            .Select(setting => new SuperAdminSettingEditViewModel
            {
                Name = setting.Name,
                Value = setting.Value
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SuperAdminRoleOptionViewModel>> GetNewEmployeeRoleOptionsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Roles
            .AsNoTracking()
            .Where(role => role.Name == "User" || role.Name == "Admin")
            .OrderBy(role => role.Name == "User" ? 0 : 1)
            .ThenBy(role => role.Name)
            .Select(role => new SuperAdminRoleOptionViewModel
            {
                RoleID = role.RoleID,
                Name = role.Name
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<RenoUser?> FindUserByEmailAsync(string? email, CancellationToken cancellationToken)
    {
        var normalizedEmail = Clean(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        var normalizedEmailLookup = normalizedEmail.ToLowerInvariant();
        return await _dbContext.RenoUsers
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(
                user => user.Login.ToLower() == normalizedEmailLookup
                    || user.Email.ToLower() == normalizedEmailLookup,
                cancellationToken);
    }

    private async Task CreateAndSendEmployeeInvitationAsync(
        Guid companyId,
        RenoUser user,
        string firstName,
        string email,
        CancellationToken cancellationToken)
    {
        var companyName = await _dbContext.RenoCompanies
            .Where(company => company.RenoCompanyID == companyId)
            .Select(company => company.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (companyName is null)
        {
            throw new InvalidOperationException("Company was not found.");
        }

        var expirationHours = await GetNewUserTokenExpirationHoursAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var invitationToken = GenerateInvitationToken();
        var invitation = new UserInvitation
        {
            UserID = user.UserID,
            TokenHash = HashInvitationToken(invitationToken),
            SentToEmail = Clean(email),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(expirationHours),
            CreatedByUserID = _currentUserSession.UserID
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await RevokePendingInvitationsAsync(user.UserID, now, cancellationToken);
        _dbContext.UserInvitations.Add(invitation);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var inviteLink = BuildInviteLink(invitation.UserInvitationId, invitationToken);

        try
        {
            await _newEmployeeEmailService.SendWelcomeEmailAsync(
                Clean(email),
                Clean(firstName),
                companyName,
                inviteLink,
                expirationHours,
                cancellationToken);

            TempData["SuperAdminEmployeesMessage"] = "Invite email sent.";
        }
        catch (Exception exception)
        {
            TempData["SuperAdminEmployeesMessage"] = $"Invite was created, but the welcome email failed to send: {exception.Message}";
        }
    }

    private void ApplyEmployeeUpdate(Employee employee, SuperAdminNewEmployeeViewModel model, DateTime editedAtUtc)
    {
        var firstName = Clean(model.GivenName);
        var lastName = Clean(model.FamilyName);
        var displayName = FormatPersonName(firstName, lastName);

        employee.Title = Clean(model.Title);
        employee.GivenName = firstName;
        employee.MiddleName = Clean(model.MiddleName);
        employee.FamilyName = lastName;
        employee.DisplayName = displayName;
        employee.PrintOnCheckName = displayName;
        employee.PrimaryEmailAddress = Clean(model.PrimaryEmailAddress);
        employee.PrimaryPhone = Clean(model.PrimaryPhone);
        employee.MobilePhone = Clean(model.MobilePhone);
        employee.EmployeeNumber = Clean(model.EmployeeNumber);
        employee.Active = model.Active;
        employee.LastEditDate = editedAtUtc;
    }

    private void TrackEmployeeAddress(Employee employee, SuperAdminNewEmployeeViewModel model)
    {
        if (IsEmployeeAddressEmpty(model))
        {
            employee.PrimaryAddress = null;
            employee.PrimaryAddressId = null;
            return;
        }

        employee.PrimaryAddress ??= new Address
        {
            RenoCompanyID = employee.RenoCompanyID
        };

        employee.PrimaryAddress.RenoCompanyID = employee.RenoCompanyID;
        employee.PrimaryAddress.Street1 = Clean(model.Street1);
        employee.PrimaryAddress.Street2 = Clean(model.Street2);
        employee.PrimaryAddress.Street3 = Clean(model.Street3);
        employee.PrimaryAddress.City = Clean(model.City);
        employee.PrimaryAddress.State = Clean(model.State);
        employee.PrimaryAddress.PostalCode = Clean(model.PostalCode);
        employee.PrimaryAddressId = employee.PrimaryAddress.AddressId;
    }

    private static bool IsEmployeeAddressEmpty(SuperAdminNewEmployeeViewModel model)
    {
        return string.IsNullOrWhiteSpace(model.Street1)
            && string.IsNullOrWhiteSpace(model.Street2)
            && string.IsNullOrWhiteSpace(model.Street3)
            && string.IsNullOrWhiteSpace(model.City)
            && string.IsNullOrWhiteSpace(model.State)
            && string.IsNullOrWhiteSpace(model.PostalCode);
    }

    private static void ApplyEmployeeUserRole(RenoUser user, Role role)
    {
        var employeeRoles = user.UserRoles
            .Where(userRole => string.Equals(userRole.Role?.Name, "User", StringComparison.OrdinalIgnoreCase)
                || string.Equals(userRole.Role?.Name, "Admin", StringComparison.OrdinalIgnoreCase)
                || userRole.RoleID == role.RoleID)
            .ToList();

        foreach (var userRole in employeeRoles)
        {
            user.UserRoles.Remove(userRole);
        }

        user.UserRoles.Add(new UserRole
        {
            UserID = user.UserID,
            RoleID = role.RoleID
        });
    }

    private async Task<bool> CanDeleteEmployeeAddressAsync(
        Guid companyId,
        Guid employeeId,
        Guid addressId,
        CancellationToken cancellationToken)
    {
        var usedByAnotherEmployee = await _dbContext.Employees.AnyAsync(
            item => item.RenoCompanyID == companyId
                && item.EmployeeId != employeeId
                && item.PrimaryAddressId == addressId,
            cancellationToken);

        if (usedByAnotherEmployee)
        {
            return false;
        }

        var usedByCustomer = await _dbContext.Customers.AnyAsync(
            item => item.RenoCompanyID == companyId
                && (item.BillAddressId == addressId || item.ShipAddressId == addressId),
            cancellationToken);

        if (usedByCustomer)
        {
            return false;
        }

        return !await _dbContext.Addresses.AnyAsync(
            item => item.RenoCompanyID == companyId
                && item.AddressId == addressId
                && item.PropertyId != null,
            cancellationToken);
    }

    private async Task<SuperAdminNewEmployeeViewModel?> BuildNewEmployeeViewModelAsync(
        Guid companyId,
        SuperAdminNewEmployeeViewModel? model,
        CancellationToken cancellationToken)
    {
        var companyName = await _dbContext.RenoCompanies
            .Where(company => company.RenoCompanyID == companyId)
            .Select(company => company.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (companyName is null)
        {
            return null;
        }

        model ??= new SuperAdminNewEmployeeViewModel();
        model.RenoCompanyID = companyId;
        model.RenoCompanyName = companyName;
        model.AvailableRoles = await GetNewEmployeeRoleOptionsAsync(cancellationToken);
        return model;
    }

    private async Task<SuperAdminNewEmployeeViewModel?> BuildEditEmployeeViewModelAsync(
        Guid companyId,
        Guid employeeId,
        SuperAdminNewEmployeeViewModel? model,
        CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees
            .AsNoTracking()
            .Include(item => item.PrimaryAddress)
            .FirstOrDefaultAsync(item => item.EmployeeId == employeeId && item.RenoCompanyID == companyId, cancellationToken);

        if (employee is null)
        {
            return null;
        }

        var companyName = await _dbContext.RenoCompanies
            .Where(company => company.RenoCompanyID == companyId)
            .Select(company => company.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (companyName is null)
        {
            return null;
        }

        if (model is null)
        {
            var user = await FindUserByEmailAsync(employee.PrimaryEmailAddress, cancellationToken);
            var selectedRoleId = user?.UserRoles
                .Where(userRole => string.Equals(userRole.Role?.Name, "User", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(userRole.Role?.Name, "Admin", StringComparison.OrdinalIgnoreCase))
                .OrderBy(userRole => userRole.Role?.Name == "User" ? 0 : 1)
                .Select(userRole => (Guid?)userRole.RoleID)
                .FirstOrDefault();

            model = new SuperAdminNewEmployeeViewModel
            {
                EmployeeId = employee.EmployeeId,
                RenoCompanyID = companyId,
                RenoCompanyName = companyName,
                Title = employee.Title,
                GivenName = employee.GivenName,
                MiddleName = employee.MiddleName,
                FamilyName = employee.FamilyName,
                PrimaryEmailAddress = employee.PrimaryEmailAddress,
                PrimaryPhone = employee.PrimaryPhone,
                MobilePhone = employee.MobilePhone,
                EmployeeNumber = employee.EmployeeNumber,
                Active = employee.Active,
                SelectedRoleID = selectedRoleId,
                Street1 = employee.PrimaryAddress?.Street1,
                Street2 = employee.PrimaryAddress?.Street2,
                Street3 = employee.PrimaryAddress?.Street3,
                City = employee.PrimaryAddress?.City,
                State = employee.PrimaryAddress?.State,
                PostalCode = employee.PrimaryAddress?.PostalCode
            };
        }

        model.EmployeeId = employeeId;
        model.RenoCompanyID = companyId;
        model.RenoCompanyName = companyName;
        model.AvailableRoles = await GetNewEmployeeRoleOptionsAsync(cancellationToken);
        model.Invitations = await GetEmployeeInvitationRowsAsync(employee.PrimaryEmailAddress, cancellationToken);
        return model;
    }

    private async Task<IReadOnlyList<SuperAdminEmployeeInvitationViewModel>> GetEmployeeInvitationRowsAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var user = await FindUserByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            return [];
        }

        var invitations = await _dbContext.UserInvitations
            .AsNoTracking()
            .Where(invitation => invitation.UserID == user.UserID)
            .OrderByDescending(invitation => invitation.CreatedAtUtc)
            .Select(invitation => new
            {
                invitation.SentToEmail,
                invitation.CreatedAtUtc,
                invitation.ExpiresAtUtc,
                invitation.AcceptedAtUtc,
                invitation.RevokedAtUtc,
                invitation.CreatedByUserID
            })
            .ToListAsync(cancellationToken);

        var createdByUserIds = invitations
            .Select(invitation => invitation.CreatedByUserID)
            .Where(userId => userId.HasValue)
            .Select(userId => userId!.Value)
            .Distinct()
            .ToList();

        var createdByLogins = createdByUserIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.RenoUsers
                .AsNoTracking()
                .Where(createdByUser => createdByUserIds.Contains(createdByUser.UserID))
                .ToDictionaryAsync(createdByUser => createdByUser.UserID, createdByUser => createdByUser.Login, cancellationToken);

        return invitations
            .Select(invitation => new SuperAdminEmployeeInvitationViewModel
            {
                SentToEmail = invitation.SentToEmail,
                CreatedAtUtc = invitation.CreatedAtUtc,
                ExpiresAtUtc = invitation.ExpiresAtUtc,
                AcceptedAtUtc = invitation.AcceptedAtUtc,
                RevokedAtUtc = invitation.RevokedAtUtc,
                CreatedByLogin = invitation.CreatedByUserID is Guid createdByUserId
                    && createdByLogins.TryGetValue(createdByUserId, out var login)
                        ? login
                        : string.Empty
            })
            .ToList();
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

    private static RequestTimingViewModel ToTimingViewModel(RequestTimingSummary summary)
    {
        return new RequestTimingViewModel
        {
            Count = summary.Count,
            AverageMilliseconds = summary.AverageMilliseconds,
            P95Milliseconds = summary.P95Milliseconds,
            MaxMilliseconds = summary.MaxMilliseconds
        };
    }

    private static RequestDiagnosticRowViewModel ToRequestRowViewModel(RequestDiagnosticEntry entry)
    {
        return new RequestDiagnosticRowViewModel
        {
            CompletedAtUtc = entry.CompletedAtUtc,
            Method = entry.Method,
            Path = entry.Path,
            StatusCode = entry.StatusCode,
            ElapsedMilliseconds = entry.ElapsedMilliseconds,
            IsApi = entry.IsApi
        };
    }
}
