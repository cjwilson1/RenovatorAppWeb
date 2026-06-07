namespace RenovatorApp.Web.ViewModels;

public sealed class PropertiesIndexViewModel
{
    public IReadOnlyList<PropertyRowViewModel> Properties { get; init; } = [];
    public IReadOnlyList<StateOptionViewModel> StateOptions { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalProperties { get; init; }
    public int TotalPages { get; init; }
    public string Search { get; init; } = string.Empty;
}

public sealed class PropertyRowViewModel
{
    public Guid PropertyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Street1 { get; init; } = string.Empty;
    public string Street2 { get; init; } = string.Empty;
    public string Street3 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string CountrySubDivisionCode { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string CustomerNames { get; init; } = string.Empty;
    public string InspectionNames { get; init; } = string.Empty;
}

public sealed class PropertyUpdateViewModel
{
    public string Name { get; init; } = string.Empty;
    public string Street1 { get; init; } = string.Empty;
    public string Street2 { get; init; } = string.Empty;
    public string Street3 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string CountrySubDivisionCode { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
}
