namespace RenovatorApp.Web.ViewModels;

public sealed class EmployeesIndexViewModel
{
    public IReadOnlyList<EmployeeRowViewModel> Employees { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalEmployees { get; init; }
    public int TotalPages { get; init; }
    public string Search { get; init; } = string.Empty;
    public DateTime? LastQuickBooksSyncDateUtc { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
}

public sealed class EmployeeRowViewModel
{
    public Guid EmployeeId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string ContactName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Active { get; init; } = string.Empty;
}

public sealed class EmployeeDetailViewModel
{
    public Guid EmployeeId { get; init; }
    public string QuickBooksEmployeeId { get; init; } = string.Empty;
    public string SyncToken { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string PrintOnCheckName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string MiddleName { get; init; } = string.Empty;
    public string FamilyName { get; init; } = string.Empty;
    public string Suffix { get; init; } = string.Empty;
    public string PrimaryEmailAddress { get; init; } = string.Empty;
    public string PrimaryPhone { get; init; } = string.Empty;
    public string MobilePhone { get; init; } = string.Empty;
    public string Active { get; init; } = string.Empty;
    public string BillableTime { get; init; } = string.Empty;
    public string EmployeeNumber { get; init; } = string.Empty;
    public string Organization { get; init; } = string.Empty;
    public string Gender { get; init; } = string.Empty;
    public DateTime? HiredDate { get; init; }
    public DateTime? ReleasedDate { get; init; }
    public DateTime? BirthDate { get; init; }
    public decimal BillRate { get; init; }
    public decimal HourlyCostRate { get; init; }
    public DateTime? QuickBooksCreateTime { get; init; }
    public DateTime? QuickBooksLastUpdatedTime { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? LastSyncDate { get; init; }
    public DateTime? LastEditDate { get; init; }
    public EmployeeAddressViewModel? PrimaryAddress { get; init; }
}

public sealed class EmployeeAddressViewModel
{
    public string Street1 { get; init; } = string.Empty;
    public string Street2 { get; init; } = string.Empty;
    public string Street3 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string CountrySubDivisionCode { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
}
