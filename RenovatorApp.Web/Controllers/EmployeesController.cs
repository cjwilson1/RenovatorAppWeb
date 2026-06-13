using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.Services;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
public sealed class EmployeesController : Controller
{
    private const int PageSize = 10;
    private static readonly Guid TemplateRenoCompanyID = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly RenovatorAppDbContext _dbContext;
    private readonly CurrentUserSession _currentUserSession;
    private readonly PasswordService _passwordService;
    private readonly NewEmployeeEmailService _newEmployeeEmailService;
    private readonly IConfiguration _configuration;

    public EmployeesController(
        RenovatorAppDbContext dbContext,
        CurrentUserSession currentUserSession,
        PasswordService passwordService,
        NewEmployeeEmailService newEmployeeEmailService,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _currentUserSession = currentUserSession;
        _passwordService = passwordService;
        _newEmployeeEmailService = newEmployeeEmailService;
        _configuration = configuration;
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

        var model = ToDetailViewModel(employee);
        model.UserRole = await GetEmployeeUserRoleDisplayAsync(employee.PrimaryEmailAddress, cancellationToken);
        return View(model);
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
            var model = ToDetailViewModel(employee, update);
            model.UserRole = await GetEmployeeUserRoleDisplayAsync(employee.PrimaryEmailAddress, cancellationToken);
            return View(model);
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
        if (update.SendInviteEmail && string.IsNullOrWhiteSpace(update.PrimaryEmailAddress))
        {
            ModelState.AddModelError(nameof(update.PrimaryEmailAddress), "Email is required to send an invitation.");
        }

        var inviteUser = update.SendInviteEmail
            ? await BuildEmployeeInviteUserAsync(update, cancellationToken)
            : null;

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

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (inviteUser is not null)
        {
            await CreateAndSendEmployeeInvitationAsync(inviteUser, Clean(update.GivenName), Clean(update.PrimaryEmailAddress), cancellationToken);
        }
        else
        {
            TempData["EmployeesStatus"] = "Employee created.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Employees/{id:guid}/SendInvite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendInvite(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.EmployeeId == id && item.RenoCompanyID == _currentUserSession.RenoCompanyID, cancellationToken);

        if (employee is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(employee.PrimaryEmailAddress))
        {
            TempData["EmployeesStatus"] = "Invite requires an employee email address.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var update = new EmployeeDetailUpdateViewModel
        {
            EmployeeId = employee.EmployeeId,
            GivenName = employee.GivenName,
            FamilyName = employee.FamilyName,
            PrimaryEmailAddress = employee.PrimaryEmailAddress,
            PrimaryPhone = employee.PrimaryPhone,
            MobilePhone = employee.MobilePhone
        };

        var inviteUser = await BuildEmployeeInviteUserAsync(update, cancellationToken);
        if (!ModelState.IsValid || inviteUser is null)
        {
            TempData["EmployeesStatus"] = ModelState
                .SelectMany(item => item.Value?.Errors ?? [])
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                ?? "Invite could not be sent.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await CreateAndSendEmployeeInvitationAsync(inviteUser, Clean(employee.GivenName), Clean(employee.PrimaryEmailAddress), cancellationToken);

        return RedirectToAction(nameof(Details), new { id });
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
            PrimaryAddress = ToAddressViewModel(employee.PrimaryAddress),
            SendInviteEmail = true
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
            SendInviteEmail = update.SendInviteEmail,
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
        model.SendInviteEmail = update.SendInviteEmail;
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

    private async Task<string> GetEmployeeUserRoleDisplayAsync(string? employeeEmail, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(employeeEmail))
        {
            return "No web user";
        }

        var normalizedEmail = employeeEmail.Trim().ToLowerInvariant();
        var user = await _dbContext.RenoUsers
            .AsNoTracking()
            .Include(item => item.UserRoles)
                .ThenInclude(item => item.Role)
            .FirstOrDefaultAsync(
                item => item.RenoCompanyID == _currentUserSession.RenoCompanyID
                    && (item.Login.ToLower() == normalizedEmail || item.Email.ToLower() == normalizedEmail),
                cancellationToken);

        if (user is null)
        {
            return "No web user";
        }

        var roles = user.UserRoles
            .Select(userRole => userRole.Role?.Name)
            .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
            .Cast<string>()
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return roles.Length == 0 ? "No role" : string.Join(", ", roles);
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

    private async Task<RenoUser?> BuildEmployeeInviteUserAsync(EmployeeDetailUpdateViewModel update, CancellationToken cancellationToken)
    {
        var normalizedEmail = Clean(update.PrimaryEmailAddress);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(item => item.Name == "User", cancellationToken);

        if (role is null)
        {
            ModelState.AddModelError(string.Empty, "Invite failed because the User role was not found.");
            return null;
        }

        var normalizedEmailLookup = normalizedEmail.ToLowerInvariant();
        var user = await _dbContext.RenoUsers
            .Include(item => item.UserRoles)
            .FirstOrDefaultAsync(
                item => item.Login.ToLower() == normalizedEmailLookup
                    || item.Email.ToLower() == normalizedEmailLookup,
                cancellationToken);

        if (user?.RenoCompanyID is Guid existingCompanyId && existingCompanyId != _currentUserSession.RenoCompanyID)
        {
            ModelState.AddModelError(
                nameof(update.PrimaryEmailAddress),
                "The email address you entered is already a user in the RenovatorApp but they are connected to a different company. User's cannot belong to more that one company.");
            return null;
        }

        var now = DateTime.UtcNow;
        if (user is null)
        {
            user = new RenoUser
            {
                RenoCompanyID = _currentUserSession.RenoCompanyID,
                Login = normalizedEmail,
                Password = _passwordService.HashPassword(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")),
                FirstName = Clean(update.GivenName),
                LastName = Clean(update.FamilyName),
                Email = normalizedEmail,
                PhonePrimary = Clean(update.PrimaryPhone),
                PhoneSecondary = Clean(update.MobilePhone),
                Active = false,
                DateCreated = now,
                DateModified = now
            };

            _dbContext.RenoUsers.Add(user);
        }
        else
        {
            user.RenoCompanyID = _currentUserSession.RenoCompanyID;
            user.Login = string.IsNullOrWhiteSpace(user.Login) ? normalizedEmail : user.Login;
            user.Email = normalizedEmail;
            user.FirstName = string.IsNullOrWhiteSpace(user.FirstName) ? Clean(update.GivenName) : user.FirstName;
            user.LastName = string.IsNullOrWhiteSpace(user.LastName) ? Clean(update.FamilyName) : user.LastName;
            user.PhonePrimary = string.IsNullOrWhiteSpace(user.PhonePrimary) ? Clean(update.PrimaryPhone) : user.PhonePrimary;
            user.PhoneSecondary = string.IsNullOrWhiteSpace(user.PhoneSecondary) ? Clean(update.MobilePhone) : user.PhoneSecondary;
            user.DateModified = now;
        }

        if (!user.UserRoles.Any(userRole => userRole.RoleID == role.RoleID))
        {
            user.UserRoles.Add(new UserRole
            {
                UserID = user.UserID,
                RoleID = role.RoleID
            });
        }

        return user;
    }

    private async Task CreateAndSendEmployeeInvitationAsync(
        RenoUser user,
        string firstName,
        string email,
        CancellationToken cancellationToken)
    {
        var companyName = await _dbContext.RenoCompanies
            .Where(company => company.RenoCompanyID == _currentUserSession.RenoCompanyID)
            .Select(company => company.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (companyName is null)
        {
            TempData["EmployeesStatus"] = "Invite failed because the company was not found.";
            return;
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

            TempData["EmployeesStatus"] = "Invite email sent.";
        }
        catch (Exception exception)
        {
            TempData["EmployeesStatus"] = $"Invite was created, but the welcome email failed to send: {exception.Message}";
        }
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
