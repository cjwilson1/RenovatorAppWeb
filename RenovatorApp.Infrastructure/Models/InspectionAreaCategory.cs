namespace RenovatorApp.Infrastructure.Models;

public sealed class InspectionAreaCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<InspectionAreaType> AreaTypes { get; set; } = [];
}
