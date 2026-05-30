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
    private readonly DatabaseViewerService _databaseViewerService;
    private readonly RenovatorAppDbContext _dbContext;
    private readonly PasswordService _passwordService;

    public SuperAdminController(
        DatabaseViewerService databaseViewerService,
        RenovatorAppDbContext dbContext,
        PasswordService passwordService)
    {
        _databaseViewerService = databaseViewerService;
        _dbContext = dbContext;
        _passwordService = passwordService;
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

        _dbContext.RenoCompanies.Add(new RenoCompany
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
        });

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
