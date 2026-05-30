namespace RenovatorApp.Infrastructure.Models;

public sealed class Inspector : IRenoCompanyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}
