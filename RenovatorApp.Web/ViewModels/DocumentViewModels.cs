namespace RenovatorApp.Web.ViewModels;

public sealed class DocumentsIndexViewModel
{
    public IReadOnlyList<DocumentRowViewModel> Documents { get; init; } = [];
    public IReadOnlyList<DocumentAssignmentOptionViewModel> Customers { get; init; } = [];
    public IReadOnlyList<DocumentAssignmentOptionViewModel> Inspections { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalDocuments { get; init; }
    public int TotalPages { get; init; }
    public string Search { get; init; } = string.Empty;
}

public sealed class DocumentRowViewModel
{
    public Guid DocumentId { get; init; }
    public string DocumentName { get; init; } = string.Empty;
    public Guid? CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public Guid? InspectionId { get; init; }
    public string InspectionTitle { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public string Filename { get; init; } = string.Empty;
    public DateTime CreateDate { get; init; }
}

public sealed class DocumentAssignmentOptionViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
