namespace RenovatorApp.Infrastructure.Models;

public sealed class Document : IRenoCompanyEntity
{
    public Guid DocumentId { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public string DocumentType { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public Customer? Customer { get; set; }
}
