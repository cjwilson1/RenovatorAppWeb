using System.Security.Claims;
using System.Security.Cryptography;
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

[AllowAnonymous]
public sealed class AccountController : Controller
{
    private readonly RenovatorAppDbContext _dbContext;
    private readonly PasswordService _passwordService;
    private readonly CurrentUserSession _currentUserSession;

    public AccountController(
        RenovatorAppDbContext dbContext,
        PasswordService passwordService,
        CurrentUserSession currentUserSession)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _currentUserSession = currentUserSession;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        if (!await _dbContext.RenoUsers.AnyAsync(cancellationToken))
        {
            return RedirectToAction(nameof(InitialAdmin));
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedLogin = model.Login.Trim();
        var user = await _dbContext.RenoUsers
            .Include(item => item.RenoCompany)
            .Include(item => item.UserRoles)
                .ThenInclude(item => item.Role)
            .FirstOrDefaultAsync(item => item.Login == normalizedLogin, cancellationToken);

        if (user is null
            || !user.Active
            || user.RenoCompany is null
            || !user.RenoCompany.Active
            || user.RenoCompanyID is null
            || !_passwordService.VerifyPassword(model.Password, user.Password))
        {
            model.ErrorMessage = "Invalid login or password.";
            return View(model);
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

        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpGet]
    public async Task<IActionResult> InitialAdmin(CancellationToken cancellationToken)
    {
        return await _dbContext.RenoUsers.AnyAsync(cancellationToken)
            ? RedirectToAction(nameof(Login))
            : View(new InitialAdminViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InitialAdmin(InitialAdminViewModel model, CancellationToken cancellationToken)
    {
        if (await _dbContext.RenoUsers.AnyAsync(cancellationToken))
        {
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var company = await _dbContext.RenoCompanies.FirstOrDefaultAsync(cancellationToken)
            ?? new RenoCompany();

        company.Name = model.CompanyName.Trim();
        company.Active = true;

        if (_dbContext.Entry(company).State == EntityState.Detached)
        {
            _dbContext.RenoCompanies.Add(company);
        }

        var superAdminRole = await _dbContext.Roles.FirstOrDefaultAsync(role => role.Name == "SuperAdmin", cancellationToken);
        if (superAdminRole is null)
        {
            superAdminRole = new Role { Name = "SuperAdmin" };
            _dbContext.Roles.Add(superAdminRole);
        }

        var user = new RenoUser
        {
            RenoCompanyID = company.RenoCompanyID,
            Login = model.Login.Trim(),
            Password = _passwordService.HashPassword(model.Password),
            FirstName = model.FirstName?.Trim() ?? string.Empty,
            LastName = model.LastName?.Trim() ?? string.Empty,
            Email = model.Email?.Trim() ?? string.Empty,
            Active = true
        };

        _dbContext.RenoUsers.Add(user);
        _dbContext.UserRoles.Add(new UserRole { User = user, Role = superAdminRole });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> AcceptInvite(Guid invitationId, string? token, CancellationToken cancellationToken)
    {
        var invitation = await GetValidInvitationAsync(invitationId, token, cancellationToken);
        if (invitation is null)
        {
            return View(new AcceptInviteViewModel
            {
                InvitationId = invitationId,
                Token = token ?? string.Empty,
                ErrorMessage = "This invitation link is invalid or has expired."
            });
        }

        return View(new AcceptInviteViewModel
        {
            InvitationId = invitation.UserInvitationId,
            Token = token ?? string.Empty,
            Login = invitation.User.Login
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptInvite(AcceptInviteViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var invitation = await GetValidInvitationAsync(model.InvitationId, model.Token, cancellationToken);
        if (invitation is null)
        {
            model.ErrorMessage = "This invitation link is invalid or has expired.";
            return View(model);
        }

        invitation.User.Password = _passwordService.HashPassword(model.Password);
        invitation.User.Active = true;
        invitation.User.DateModified = DateTime.UtcNow;
        invitation.AcceptedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["AccountMessage"] = "Password created. You can now log in.";

        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        _currentUserSession.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return User.IsInRole("SuperAdmin")
            ? RedirectToAction("Index", "SuperAdmin")
            : RedirectToAction("Index", "Home");
    }

    private async Task<UserInvitation?> GetValidInvitationAsync(
        Guid invitationId,
        string? token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = HashInvitationToken(token);
        var now = DateTime.UtcNow;

        return await _dbContext.UserInvitations
            .Include(invitation => invitation.User)
                .ThenInclude(user => user.RenoCompany)
            .FirstOrDefaultAsync(invitation =>
                invitation.UserInvitationId == invitationId
                && invitation.TokenHash == tokenHash
                && invitation.AcceptedAtUtc == null
                && invitation.RevokedAtUtc == null
                && invitation.ExpiresAtUtc > now
                && invitation.User.RenoCompanyID != null
                && invitation.User.RenoCompany != null
                && invitation.User.RenoCompany.Active,
                cancellationToken);
    }

    private static string HashInvitationToken(string token)
    {
        var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToBase64String(hashBytes);
    }
}
