namespace RenovatorApp.Web.ViewModels;

public sealed class PartsManagerViewModel
{
    public IReadOnlyList<PartsManagerPartViewModel> Parts { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalParts { get; init; }
    public int TotalPages { get; init; }
    public IReadOnlyList<int> PageSizeOptions { get; init; } = [10, 15, 25, 50, 100];
}

public sealed class PartsManagerPartViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public decimal Cost { get; init; }
    public string Url { get; init; } = string.Empty;
    public bool IsPackage { get; init; }
    public int PackageUnits { get; init; }
}

public sealed class AddPartViewModel
{
    public string Url { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ModelNumber { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
