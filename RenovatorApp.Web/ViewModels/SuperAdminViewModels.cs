using System.ComponentModel.DataAnnotations;

namespace RenovatorApp.Web.ViewModels;

public sealed class SuperAdminCompaniesViewModel
{
    public IReadOnlyList<SuperAdminCompanyRowViewModel> Companies { get; init; } = [];
    public string Search { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalCompanies { get; init; }
    public int TotalPages { get; init; } = 1;
}

public sealed class SuperAdminUsersViewModel
{
    public IReadOnlyList<SuperAdminUserRowViewModel> Users { get; init; } = [];
    public string Search { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalUsers { get; init; }
    public int TotalPages { get; init; } = 1;
}

public sealed class SuperAdminUserRowViewModel
{
    public Guid UserID { get; init; }
    public string Login { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhonePrimary { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string Roles { get; init; } = string.Empty;
    public bool Active { get; init; }
}

public sealed class SuperAdminCompanyRowViewModel
{
    public Guid RenoCompanyID { get; init; }
    public string Name { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string URL { get; init; } = string.Empty;
    public bool Active { get; init; }
    public DateTime DateCreated { get; init; }
}

public sealed class SuperAdminCompanyEditViewModel
{
    public Guid? RenoCompanyID { get; set; }
    public bool IsEditMode => RenoCompanyID.HasValue;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? StreetAddress { get; set; }
    public string? StreetAddress2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? URL { get; set; }
    public bool Active { get; set; } = true;
    public IReadOnlyList<SuperAdminCompanyUserRowViewModel> Users { get; set; } = [];
}

public sealed class SuperAdminCompanyUserRowViewModel
{
    public Guid UserID { get; init; }
    public string Login { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhonePrimary { get; init; } = string.Empty;
    public bool Active { get; init; }
    public string Roles { get; init; } = string.Empty;
}

public sealed class SuperAdminUserEditViewModel
{
    public Guid? UserID { get; set; }
    public Guid? RenoCompanyID { get; set; }
    public string RenoCompanyName { get; set; } = string.Empty;
    public bool IsEditMode => UserID.HasValue;

    [Required]
    public string Login { get; set; } = string.Empty;

    [Required]
    public string? FirstName { get; set; }

    [Required]
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhonePrimary { get; set; }
    public string? PhoneSecondary { get; set; }
    public bool Active { get; set; } = true;

    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    public string? ConfirmPassword { get; set; }

    public IReadOnlyList<SuperAdminRoleOptionViewModel> AvailableRoles { get; set; } = [];
    public List<Guid> SelectedRoleIDs { get; set; } = [];
}

public sealed class SuperAdminRoleOptionViewModel
{
    public Guid RoleID { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class SuperAdminAttachUserViewModel
{
    public Guid RenoCompanyID { get; set; }
    public IReadOnlyList<SuperAdminAttachUserOptionViewModel> AvailableUsers { get; set; } = [];

    [Required]
    public Guid? UserID { get; set; }
}

public sealed class SuperAdminAttachUserOptionViewModel
{
    public Guid UserID { get; init; }
    public string Login { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
}
