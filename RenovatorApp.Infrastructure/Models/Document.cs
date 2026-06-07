namespace RenovatorApp.Infrastructure.Models;

public sealed class Document : IRenoCompanyEntity
{
    public Guid DocumentId { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Guid? InspectionId { get; set; }
    public Guid DocumentTypeId { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public string Filename { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public Customer? Customer { get; set; }
    public Inspection? Inspection { get; set; }
    public DocumentType? DocumentType { get; set; }
}
