namespace RenovatorApp.Infrastructure.Models;

public sealed class Customer : IRenoCompanyEntity
{
    public Guid CustomerId { get; set; } = Guid.NewGuid();
    public Guid RenoCompanyID { get; set; }
    public string QuickBooksCustomerId { get; set; } = string.Empty;
    public string SyncToken { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FullyQualifiedName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string GivenName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;
    public string PrintOnCheckName { get; set; } = string.Empty;
    public string PrimaryEmailAddress { get; set; } = string.Empty;
    public string PrimaryPhone { get; set; } = string.Empty;
    public string AlternatePhone { get; set; } = string.Empty;
    public string MobilePhone { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool Taxable { get; set; }
    public bool Job { get; set; }
    public bool BillWithParent { get; set; }
    public decimal Balance { get; set; }
    public decimal BalanceWithJobs { get; set; }
    public string PreferredDeliveryMethod { get; set; } = string.Empty;
    public string ParentRefValue { get; set; } = string.Empty;
    public string ParentRefName { get; set; } = string.Empty;
    public string PaymentMethodRefValue { get; set; } = string.Empty;
    public string PaymentMethodRefName { get; set; } = string.Empty;
    public string SalesTermRefValue { get; set; } = string.Empty;
    public string SalesTermRefName { get; set; } = string.Empty;
    public string CurrencyRefValue { get; set; } = string.Empty;
    public string CurrencyRefName { get; set; } = string.Empty;
    public DateTime? QuickBooksCreateTime { get; set; }
    public DateTime? QuickBooksLastUpdatedTime { get; set; }
    public Guid? BillAddressId { get; set; }
    public Guid? ShipAddressId { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncDate { get; set; }
    public DateTime? LastEditDate { get; set; }
    public Address? BillAddress { get; set; }
    public Address? ShipAddress { get; set; }
    public List<Property> Properties { get; set; } = [];
    public List<Inspection> Inspections { get; set; } = [];
    public List<Document> Documents { get; set; } = [];
}
