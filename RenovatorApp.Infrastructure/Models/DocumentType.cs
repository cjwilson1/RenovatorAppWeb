namespace RenovatorApp.Infrastructure.Models;

public sealed class DocumentType
{
    public static readonly Guid InspectionId = Guid.Parse("2f5c7a2d-89a5-4f97-9b6f-70c9f47a1f01");
    public static readonly Guid CustomerDataSheetId = Guid.Parse("9a9f93a7-9b6e-45c9-bc19-cc6cb17635b5");

    public Guid DocumentTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Document> Documents { get; set; } = [];
}
