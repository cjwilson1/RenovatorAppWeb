namespace RenovatorApp.Infrastructure.Models;

public sealed class RenoUser
{
    public Guid UserID { get; set; } = Guid.NewGuid();
    public Guid? RenoCompanyID { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhonePrimary { get; set; } = string.Empty;
    public string PhoneSecondary { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateModified { get; set; } = DateTime.UtcNow;
    public DateTime? DateLastLogin { get; set; }
    public bool Active { get; set; } = true;
    public RenoCompany? RenoCompany { get; set; }
    public List<UserRole> UserRoles { get; set; } = [];
    public List<CalendarEvent> CalendarEvents { get; set; } = [];
    public List<UserInvitation> UserInvitations { get; set; } = [];
}
