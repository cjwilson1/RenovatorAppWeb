using System.ComponentModel.DataAnnotations;

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
    public bool IsInspector { get; init; }
    public string Active { get; init; } = string.Empty;
}

public sealed class EmployeeDetailViewModel
{
    public bool IsNew { get; set; }
    public Guid EmployeeId { get; set; }
    public string QuickBooksEmployeeId { get; set; } = string.Empty;
    public string SyncToken { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? PrintOnCheckName { get; set; }
    public string? Title { get; set; }

    [Required]
    public string GivenName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    [Required]
    public string FamilyName { get; set; } = string.Empty;

    public string? Suffix { get; set; }
    public string? PrimaryEmailAddress { get; set; }

    public string? PrimaryPhone { get; set; }

    public string? MobilePhone { get; set; }
    public string Active { get; set; } = string.Empty;
    public string BillableTime { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? HiredDate { get; set; }
    public DateTime? ReleasedDate { get; set; }
    public DateTime? BirthDate { get; set; }
    public decimal BillRate { get; set; }
    public decimal HourlyCostRate { get; set; }
    public decimal InspectorHourlyRate { get; set; }
    public bool IsInspector { get; set; }
    public bool IsDefaultInspector { get; set; }
    public DateTime? QuickBooksCreateTime { get; set; }
    public DateTime? QuickBooksLastUpdatedTime { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastSyncDate { get; set; }
    public DateTime? LastEditDate { get; set; }
    public EmployeeAddressViewModel? PrimaryAddress { get; set; }
    public bool SendInviteEmail { get; set; } = true;
}

public sealed class EmployeeDetailUpdateViewModel
{
    public Guid EmployeeId { get; set; }

    public string? DisplayName { get; set; }

    public string? PrintOnCheckName { get; set; }
    public string? Title { get; set; }

    [Required]
    public string GivenName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    [Required]
    public string FamilyName { get; set; } = string.Empty;

    public string? Suffix { get; set; }
    public string? PrimaryEmailAddress { get; set; }

    public string? PrimaryPhone { get; set; }

    public string? MobilePhone { get; set; }
    public decimal BillRate { get; set; }
    public decimal HourlyCostRate { get; set; }
    public decimal InspectorHourlyRate { get; set; }
    public bool IsInspector { get; set; }
    public bool IsDefaultInspector { get; set; }
    public EmployeeAddressUpdateViewModel PrimaryAddress { get; set; } = new();
    public bool SendInviteEmail { get; set; } = true;
}

public sealed class EmployeeAddressViewModel
{
    public string? Street1 { get; init; }
    public string? Street2 { get; init; }
    public string? Street3 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? CountrySubDivisionCode { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}

public sealed class EmployeeAddressUpdateViewModel
{
    public string? Street1 { get; set; }
    public string? Street2 { get; set; }
    public string? Street3 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? CountrySubDivisionCode { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}
