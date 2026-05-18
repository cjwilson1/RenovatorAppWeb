namespace RenovatorApp.Infrastructure.Models;

public sealed class Employee
{
    public Guid EmployeeId { get; set; } = Guid.NewGuid();
    public string QuickBooksEmployeeId { get; set; } = string.Empty;
    public string SyncToken { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PrintOnCheckName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string GivenName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;
    public string PrimaryEmailAddress { get; set; } = string.Empty;
    public string PrimaryPhone { get; set; } = string.Empty;
    public string MobilePhone { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool BillableTime { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? HiredDate { get; set; }
    public DateTime? ReleasedDate { get; set; }
    public DateTime? BirthDate { get; set; }
    public decimal BillRate { get; set; }
    public decimal HourlyCostRate { get; set; }
    public DateTime? QuickBooksCreateTime { get; set; }
    public DateTime? QuickBooksLastUpdatedTime { get; set; }
    public Guid? PrimaryAddressId { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncDate { get; set; }
    public DateTime? LastEditDate { get; set; }
    public Address? PrimaryAddress { get; set; }
}
