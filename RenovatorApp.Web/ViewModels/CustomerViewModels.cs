namespace RenovatorApp.Web.ViewModels;

public sealed class CustomersIndexViewModel
{
    public IReadOnlyList<CustomerRowViewModel> Customers { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCustomers { get; init; }
    public int TotalPages { get; init; }
    public string Search { get; init; } = string.Empty;
    public bool ShowInactive { get; init; }
    public DateTime? LastQuickBooksSyncDateUtc { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
}

public sealed class CustomerRowViewModel
{
    public Guid CustomerId { get; init; }
    public string QuickBooksCustomerId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string ContactName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Active { get; init; } = string.Empty;
}

public sealed class CustomerDetailViewModel
{
    public Guid CustomerId { get; set; }
    public string QuickBooksCustomerId { get; set; } = string.Empty;
    public string SyncToken { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string FullyQualifiedName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Title { get; set; }
    public string? GivenName { get; set; }
    public string? MiddleName { get; set; }
    public string? FamilyName { get; set; }
    public string? Suffix { get; set; }
    public string? PrintOnCheckName { get; set; }
    public string? PrimaryEmailAddress { get; set; }
    public string? PrimaryPhone { get; set; }
    public string? AlternatePhone { get; set; }
    public string? MobilePhone { get; set; }
    public string? Fax { get; set; }
    public string? Website { get; set; }
    public string? Notes { get; set; }
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
    public CustomerAddressViewModel? BillAddress { get; set; }
    public IReadOnlyList<CustomerPropertyViewModel> Properties { get; init; } = [];
    public bool CanAddBillingAddressProperty { get; init; }
    public IReadOnlyList<CustomerDocumentViewModel> Documents { get; init; } = [];
    public CustomerAddressViewModel? ShipAddress { get; set; }
    public IReadOnlyList<StateOptionViewModel> StateOptions { get; set; } = [];
}

public sealed class CustomerDetailUpdateViewModel
{
    public Guid CustomerId { get; set; }
    public string? DisplayName { get; set; }
    public string? CompanyName { get; set; }
    public string? Title { get; set; }
    public string? GivenName { get; set; }
    public string? MiddleName { get; set; }
    public string? FamilyName { get; set; }
    public string? Suffix { get; set; }
    public string? PrintOnCheckName { get; set; }
    public string? PrimaryEmailAddress { get; set; }
    public string? PrimaryPhone { get; set; }
    public string? AlternatePhone { get; set; }
    public string? MobilePhone { get; set; }
    public string? Fax { get; set; }
    public string? Website { get; set; }
    public string? Notes { get; set; }
    public CustomerAddressUpdateViewModel BillAddress { get; set; } = new();
    public CustomerAddressUpdateViewModel ShipAddress { get; set; } = new();
}

public sealed class CustomerDocumentViewModel
{
    public Guid DocumentId { get; init; }
    public string DocumentName { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public string Filename { get; init; } = string.Empty;
    public DateTime CreateDate { get; init; }
}

public sealed class CustomerPropertyViewModel
{
    public Guid PropertyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string StreetAddress { get; init; } = string.Empty;
    public string Street1 { get; init; } = string.Empty;
    public string Street2 { get; init; } = string.Empty;
    public string Street3 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
}

public sealed class CustomerAddPropertyViewModel
{
    public Guid? PropertyId { get; set; }
    public string? Name { get; set; }
    public CustomerAddressUpdateViewModel Address { get; set; } = new();
}

public sealed class CustomerAddressViewModel
{
    public string? Street1 { get; init; }
    public string? Street2 { get; init; }
    public string? Street3 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? CountrySubDivisionCode { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}

public sealed class CustomerAddressUpdateViewModel
{
    public string? Street1 { get; set; }
    public string? Street2 { get; set; }
    public string? Street3 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? CountrySubDivisionCode { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}
