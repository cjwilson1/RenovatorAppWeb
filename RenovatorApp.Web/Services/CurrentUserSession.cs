using System.Security.Claims;

namespace RenovatorApp.Web.Services;

public sealed class CurrentUserSession
{
    public const string UserIDKey = "UserID";
    public const string RoleIDsKey = "RoleIDs";
    public const string RenoCompanyIDKey = "RenoCompanyID";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserSession(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserID => GetRequiredGuid(UserIDKey, ClaimTypes.NameIdentifier);
    public Guid RenoCompanyID => GetRequiredGuid(RenoCompanyIDKey, "RenoCompanyID");
    public IReadOnlyList<Guid> RoleIDs => GetString(RoleIDsKey)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(Guid.Parse)
        .ToList();

    public bool IsLoggedIn => TryGetGuid(RenoCompanyIDKey, "RenoCompanyID", out _);

    public void Set(Guid userID, Guid renoCompanyID, IReadOnlyList<Guid> roleIDs)
    {
        var session = HttpContext.Session;
        session.SetString(UserIDKey, userID.ToString());
        session.SetString(RenoCompanyIDKey, renoCompanyID.ToString());
        session.SetString(RoleIDsKey, string.Join(',', roleIDs));
    }

    public void Clear()
    {
        HttpContext.Session.Remove(UserIDKey);
        HttpContext.Session.Remove(RenoCompanyIDKey);
        HttpContext.Session.Remove(RoleIDsKey);
    }

    private HttpContext HttpContext => _httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("No current HTTP context is available.");

    private Guid GetRequiredGuid(string sessionKey, string claimType)
    {
        if (TryGetGuid(sessionKey, claimType, out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Current user session is missing {sessionKey}.");
    }

    private bool TryGetGuid(string sessionKey, string claimType, out Guid value)
    {
        var raw = GetString(sessionKey);
        if (Guid.TryParse(raw, out value))
        {
            return true;
        }

        raw = HttpContext.User.FindFirstValue(claimType);
        return Guid.TryParse(raw, out value);
    }

    private string GetString(string sessionKey) => HttpContext.Session.GetString(sessionKey) ?? string.Empty;
}
