namespace RenovatorApp.Infrastructure.Models;

public sealed class Role
{
    public Guid RoleID { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public List<UserRole> UserRoles { get; set; } = [];
}
