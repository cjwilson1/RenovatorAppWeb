namespace RenovatorApp.Infrastructure.Models;

public sealed class Property : IRenoCompanyEntity
{
    public Guid PropertyId { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public string Name { get; set; } = string.Empty;
    public Address Address { get; set; } = null!;
    public List<Customer> Customers { get; set; } = [];
    public List<Building> Buildings { get; set; } = [];
    public List<InspectionArea> Areas { get; set; } = [];
    public List<Inspection> Inspections { get; set; } = [];
}
