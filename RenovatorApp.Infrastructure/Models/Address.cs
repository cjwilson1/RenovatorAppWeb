namespace RenovatorApp.Infrastructure.Models;

public sealed class Address : IRenoCompanyEntity
{
    public Guid AddressId { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public Guid? PropertyId { get; set; }
    public string Street1 { get; set; } = string.Empty;
    public string Street2 { get; set; } = string.Empty;
    public string Street3 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string CountrySubDivisionCode { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public Property? Property { get; set; }
    public List<Customer> BillingCustomers { get; set; } = [];
    public List<Customer> ShippingCustomers { get; set; } = [];
    public List<Employee> Employees { get; set; } = [];
}
