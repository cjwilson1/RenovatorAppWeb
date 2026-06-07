namespace RenovatorApp.Web.ViewModels;

public sealed class SettingsIndexViewModel
{
    public QuickBooksConnectionViewModel QuickBooks { get; init; } = new();
}

public sealed class QuickBooksConnectionViewModel
{
    public bool IsConfigured { get; init; }
    public bool IsConnected { get; init; }
    public string RealmId { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public DateTime? AccessTokenExpiresAtUtc { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
}

public sealed class DefaultSettingsViewModel
{
    public string DefaultState { get; set; } = string.Empty;
    public IReadOnlyList<StateOptionViewModel> States { get; init; } = [];
    public string StatusMessage { get; init; } = string.Empty;
}

public sealed record StateOptionViewModel(string Abbreviation, string Name);

public sealed class QuickBooksCustomersViewModel
{
    public bool IsConnected { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public IReadOnlyList<QuickBooksCustomerViewModel> Customers { get; init; } = [];
}

public sealed class QuickBooksCustomerViewModel
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string FamilyName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Active { get; init; } = string.Empty;
}

public sealed class PartsManagerViewModel
{
    public IReadOnlyList<PartsManagerPartViewModel> Parts { get; init; } = [];
    public string StatusMessage { get; init; } = string.Empty;
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
    public string ImageUrl { get; init; } = string.Empty;
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
    public string ImageUrl { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> LookupDebugMessages { get; set; } = [];
}

public sealed class EditPartViewModel
{
    public Guid PartId { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ModelNumber { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
