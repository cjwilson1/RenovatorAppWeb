namespace RenovatorApp.Web.ViewModels;

public sealed class DatabaseTablePageViewModel
{
    public IReadOnlyList<string> TableNames { get; init; } = [];
    public string? SelectedTableName { get; init; }
    public IReadOnlyList<string> Columns { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int TotalRows { get; init; }
    public int TotalPages { get; init; } = 1;
    public bool HasSelectedTable => !string.IsNullOrWhiteSpace(SelectedTableName);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public sealed class CompanyTablePageViewModel
{
    public Guid RenoCompanyID { get; init; }
    public string Title { get; init; } = string.Empty;
    public string RouteAction { get; init; } = string.Empty;
    public string? SelectedTableName { get; init; }
    public IReadOnlyList<string> Columns { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int TotalRows { get; init; }
    public int TotalPages { get; init; } = 1;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
