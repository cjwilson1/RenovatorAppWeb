namespace RenovatorApp.Infrastructure.Models;

public sealed class RenoCompany
{
    public Guid RenoCompanyID { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string StreetAddress2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Zip { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string URL { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public List<RenoUser> Users { get; set; } = [];
}
