namespace RenovatorApp.Infrastructure.Models;

public sealed class UserRole
{
    public Guid UserID { get; set; }
    public Guid RoleID { get; set; }
    public RenoUser? User { get; set; }
    public Role? Role { get; set; }
}
