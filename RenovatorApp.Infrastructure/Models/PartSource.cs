namespace RenovatorApp.Infrastructure.Models;

public sealed class PartSource
{
    public Guid PartSourceId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public List<Part> Parts { get; set; } = [];
}
