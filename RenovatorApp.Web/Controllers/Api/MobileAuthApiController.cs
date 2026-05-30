using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Web.Services;

namespace RenovatorApp.Web.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/mobile-auth")]
public sealed class MobileAuthApiController : ControllerBase
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly IConfiguration _configuration;
    private readonly RenovatorAppDbContext _dbContext;
    private readonly PasswordService _passwordService;

    public MobileAuthApiController(
        IConfiguration configuration,
        RenovatorAppDbContext dbContext,
        PasswordService passwordService)
    {
        _configuration = configuration;
        _dbContext = dbContext;
        _passwordService = passwordService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<MobileAuthLoginResponse>> Login(MobileAuthLoginRequest request, CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        var normalizedLogin = request.Login.Trim();
        if (string.IsNullOrWhiteSpace(normalizedLogin) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized();
        }

        var user = await _dbContext.RenoUsers
            .Include(item => item.RenoCompany)
            .FirstOrDefaultAsync(item => item.Login == normalizedLogin, cancellationToken);

        if (user is null
            || !user.Active
            || user.RenoCompany is null
            || !user.RenoCompany.Active
            || user.RenoCompanyID is null
            || !_passwordService.VerifyPassword(request.Password, user.Password))
        {
            return Unauthorized();
        }

        user.DateLastLogin = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MobileAuthLoginResponse(
            user.UserID,
            user.RenoCompanyID.Value,
            user.Login,
            user.Password,
            user.FirstName,
            user.LastName,
            user.Email,
            user.PhonePrimary,
            user.PhoneSecondary,
            user.DateCreated,
            user.DateModified,
            user.DateLastLogin,
            user.Active);
    }

    private bool IsAuthorized()
    {
        var configuredApiKey = _configuration["MobileSync:ApiKey"] ?? _configuration["MOBILE_SYNC_API_KEY"];
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            return true;
        }

        return Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKey)
            && string.Equals(apiKey.ToString(), configuredApiKey, StringComparison.Ordinal);
    }
}

public sealed record MobileAuthLoginRequest(string Login, string Password);

public sealed record MobileAuthLoginResponse(
    Guid UserID,
    Guid RenoCompanyID,
    string Login,
    string PasswordHash,
    string FirstName,
    string LastName,
    string Email,
    string PhonePrimary,
    string PhoneSecondary,
    DateTime DateCreated,
    DateTime DateModified,
    DateTime? DateLastLogin,
    bool Active);
