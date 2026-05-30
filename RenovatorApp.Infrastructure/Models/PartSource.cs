namespace RenovatorApp.Infrastructure.Models;

public sealed class PartSource : IRenoCompanyEntity
{
    public Guid PartSourceId { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Part> Parts { get; set; } = [];
}
