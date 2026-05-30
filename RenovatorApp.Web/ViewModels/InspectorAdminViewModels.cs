using System.ComponentModel.DataAnnotations;

namespace RenovatorApp.Web.ViewModels;

public sealed class InspectorsIndexViewModel
{
    public IReadOnlyList<InspectorRowViewModel> Inspectors { get; init; } = [];
    public int Page { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int TotalInspectors { get; init; }
}

public sealed class InspectorRowViewModel
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public decimal HourlyRate { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
}

public sealed class InspectorEditViewModel
{
    public Guid? Id { get; set; }
    public bool IsEditMode => Id.HasValue;

    [Display(Name = "First Name")]
    public string? FirstName { get; set; }

    [Display(Name = "Last Name")]
    public string? LastName { get; set; }

    [DataType(DataType.Currency)]
    [Display(Name = "Hourly Rate")]
    public decimal HourlyRate { get; set; }

    public string? Phone { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    [Display(Name = "Default")]
    public bool IsDefault { get; set; }
}
