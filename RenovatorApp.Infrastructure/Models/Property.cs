namespace RenovatorApp.Infrastructure.Models;

public sealed class Property : IRenoCompanyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public Address Address { get; set; } = null!;
    public List<Building> Buildings { get; set; } = [];
    public List<InspectionArea> Areas { get; set; } = [];
    public List<Inspection> Inspections { get; set; } = [];
}
