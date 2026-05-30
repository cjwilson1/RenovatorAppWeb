namespace RenovatorApp.Infrastructure.Models;

public sealed class InspectionAreaCategory : IRenoCompanyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<InspectionAreaType> AreaTypes { get; set; } = [];
}
