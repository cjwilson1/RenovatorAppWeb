namespace RenovatorApp.Web.ViewModels;

public sealed class CustomersIndexViewModel
{
    public IReadOnlyList<CustomerRowViewModel> Customers { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCustomers { get; init; }
    public int TotalPages { get; init; }
    public string Search { get; init; } = string.Empty;
    public DateTime? LastQuickBooksSyncDateUtc { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
}

public sealed class CustomerRowViewModel
{
    public Guid CustomerId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string ContactName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Active { get; init; } = string.Empty;
}

public sealed class CustomerDetailViewModel
{
    public Guid CustomerId { get; init; }
    public string QuickBooksCustomerId { get; init; } = string.Empty;
    public string SyncToken { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string FullyQualifiedName { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string MiddleName { get; init; } = string.Empty;
    public string FamilyName { get; init; } = string.Empty;
    public string Suffix { get; init; } = string.Empty;
    public string PrintOnCheckName { get; init; } = string.Empty;
    public string PrimaryEmailAddress { get; init; } = string.Empty;
    public string PrimaryPhone { get; init; } = string.Empty;
    public string AlternatePhone { get; init; } = string.Empty;
    public string MobilePhone { get; init; } = string.Empty;
    public string Fax { get; init; } = string.Empty;
    public string Website { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string Active { get; init; } = string.Empty;
    public string Taxable { get; init; } = string.Empty;
    public string Job { get; init; } = string.Empty;
    public string BillWithParent { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public decimal BalanceWithJobs { get; init; }
    public string PreferredDeliveryMethod { get; init; } = string.Empty;
    public string ParentRef { get; init; } = string.Empty;
    public string PaymentMethodRef { get; init; } = string.Empty;
    public string SalesTermRef { get; init; } = string.Empty;
    public string CurrencyRef { get; init; } = string.Empty;
    public DateTime? QuickBooksCreateTime { get; init; }
    public DateTime? QuickBooksLastUpdatedTime { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? LastSyncDate { get; init; }
    public DateTime? LastEditDate { get; init; }
    public CustomerAddressViewModel? BillAddress { get; init; }
    public IReadOnlyList<CustomerDocumentViewModel> Documents { get; init; } = [];
    public CustomerAddressViewModel? ShipAddress { get; init; }
}

public sealed class CustomerDocumentViewModel
{
    public Guid DocumentId { get; init; }
    public string DocumentName { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public string Filename { get; init; } = string.Empty;
    public DateTime CreateDate { get; init; }
}

public sealed class CustomerAddressViewModel
{
    public string Street1 { get; init; } = string.Empty;
    public string Street2 { get; init; } = string.Empty;
    public string Street3 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string CountrySubDivisionCode { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
}
