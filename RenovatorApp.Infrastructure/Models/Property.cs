namespace RenovatorApp.Infrastructure.Models;

public sealed class Property
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Address Address { get; set; } = null!;
    public List<Building> Buildings { get; set; } = [];
    public List<InspectionArea> Areas { get; set; } = [];
    public List<Inspection> Inspections { get; set; } = [];
}
