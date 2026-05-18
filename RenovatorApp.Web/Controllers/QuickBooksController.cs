using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.ViewModels;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RenovatorApp.Web.Controllers;

public sealed class QuickBooksController : Controller
{
    private const string AccountingScope = "com.intuit.quickbooks.accounting";
    private const string StateSessionKey = "QuickBooksOAuthState";
    private readonly IConfiguration _configuration;
    private readonly RenovatorAppDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;

    public QuickBooksController(
        IConfiguration configuration,
        RenovatorAppDbContext dbContext,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
    }

    public IActionResult Connect()
    {
        var clientId = GetRequiredConfig("ClientId");
        var clientSecret = GetRequiredConfig("ClientSecret");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            TempData["QuickBooksStatus"] = "QuickBooks ClientId and ClientSecret are required before connecting.";
            return RedirectToAction("Index", "Settings");
        }

        var state = CreateState();
        HttpContext.Session.SetString(StateSessionKey, state);

        var query = new QueryString()
            .Add("client_id", clientId)
            .Add("response_type", "code")
            .Add("scope", AccountingScope)
            .Add("redirect_uri", GetRedirectUri())
            .Add("state", state);

        return Redirect($"https://appcenter.intuit.com/connect/oauth2{query}");
    }

    public async Task<IActionResult> Customers(CancellationToken cancellationToken)
    {
        var connection = await LoadConnectionAsync(cancellationToken);
        if (!connection.IsConnected)
        {
            return View(new QuickBooksCustomersViewModel
            {
                IsConnected = false,
                StatusMessage = "QuickBooks is not connected yet."
            });
        }

        try
        {
            var accessToken = await GetValidAccessTokenAsync(connection, cancellationToken);
            var customers = await GetCustomersAsync(connection.RealmId, accessToken, cancellationToken);

            return View(new QuickBooksCustomersViewModel
            {
                IsConnected = true,
                StatusMessage = $"Retrieved {customers.Count} QuickBooks customer{(customers.Count == 1 ? string.Empty : "s")}.",
                Customers = customers
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or JsonException)
        {
            return View(new QuickBooksCustomersViewModel
            {
                IsConnected = true,
                StatusMessage = $"QuickBooks customer lookup failed: {exception.Message}"
            });
        }
    }

    public async Task<IActionResult> Callback(
        string? code,
        string? realmId,
        string? state,
        string? error,
        string? error_description,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            TempData["QuickBooksStatus"] = $"QuickBooks connection failed: {error_description ?? error}.";
            return RedirectToAction("Index", "Settings");
        }

        var expectedState = HttpContext.Session.GetString(StateSessionKey);
        HttpContext.Session.Remove(StateSessionKey);

        if (string.IsNullOrWhiteSpace(state) || !string.Equals(state, expectedState, StringComparison.Ordinal))
        {
            TempData["QuickBooksStatus"] = "QuickBooks connection failed because the OAuth state was invalid.";
            return RedirectToAction("Index", "Settings");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(realmId))
        {
            TempData["QuickBooksStatus"] = "QuickBooks did not return the authorization code or realm ID.";
            return RedirectToAction("Index", "Settings");
        }

        try
        {
            var tokenResponse = await ExchangeAuthorizationCodeAsync(code, cancellationToken);
            await SaveQuickBooksTokensAsync(realmId, tokenResponse, cancellationToken);
            TempData["QuickBooksStatus"] = "QuickBooks connected successfully.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or JsonException)
        {
            TempData["QuickBooksStatus"] = $"QuickBooks connection failed during token exchange: {exception.Message}";
        }

        return RedirectToAction("Index", "Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.AppSettings
            .Where(setting => setting.Name.StartsWith("QuickBooks:"))
            .ToListAsync(cancellationToken);

        _dbContext.AppSettings.RemoveRange(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["QuickBooksStatus"] = "QuickBooks disconnected.";
        return RedirectToAction("Index", "Settings");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncCustomers(CancellationToken cancellationToken)
    {
        var connection = await LoadConnectionAsync(cancellationToken);
        if (!connection.IsConnected)
        {
            TempData["CustomersStatus"] = "QuickBooks is not connected yet.";
            return RedirectToAction("Index", "Customers");
        }

        try
        {
            var syncDateUtc = DateTime.UtcNow;
            var accessToken = await GetValidAccessTokenAsync(connection, cancellationToken);
            var quickBooksCustomers = await FetchQuickBooksCustomersAsync(connection.RealmId, accessToken, cancellationToken);

            foreach (var quickBooksCustomer in quickBooksCustomers)
            {
                await UpsertCustomerAsync(quickBooksCustomer, syncDateUtc, cancellationToken);
            }

            await UpsertSettingAsync("QuickBooks:CustomersLastSyncDateUtc", syncDateUtc.ToString("O"), cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            TempData["CustomersStatus"] = $"Synced {quickBooksCustomers.Count} QuickBooks customer{(quickBooksCustomers.Count == 1 ? string.Empty : "s")}.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or JsonException or DbUpdateException)
        {
            TempData["CustomersStatus"] = $"QuickBooks customer sync failed: {exception.Message}";
        }

        return RedirectToAction("Index", "Customers");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncEmployees(CancellationToken cancellationToken)
    {
        var connection = await LoadConnectionAsync(cancellationToken);
        if (!connection.IsConnected)
        {
            TempData["EmployeesStatus"] = "QuickBooks is not connected yet.";
            return RedirectToAction("Index", "Employees");
        }

        try
        {
            var syncDateUtc = DateTime.UtcNow;
            var accessToken = await GetValidAccessTokenAsync(connection, cancellationToken);
            var quickBooksEmployees = await FetchQuickBooksEmployeesAsync(connection.RealmId, accessToken, cancellationToken);

            foreach (var quickBooksEmployee in quickBooksEmployees)
            {
                await UpsertEmployeeAsync(quickBooksEmployee, syncDateUtc, cancellationToken);
            }

            await UpsertSettingAsync("QuickBooks:EmployeesLastSyncDateUtc", syncDateUtc.ToString("O"), cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            TempData["EmployeesStatus"] = $"Synced {quickBooksEmployees.Count} QuickBooks employee{(quickBooksEmployees.Count == 1 ? string.Empty : "s")}.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or JsonException or DbUpdateException)
        {
            TempData["EmployeesStatus"] = $"QuickBooks employee sync failed: {exception.Message}";
        }

        return RedirectToAction("Index", "Employees");
    }

    private async Task<QuickBooksTokenResponse> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken)
    {
        var clientId = GetRequiredConfig("ClientId");
        var clientSecret = GetRequiredConfig("ClientSecret");
        var client = _httpClientFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = GetRedirectUri()
        });

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"QuickBooks token exchange failed with status {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        return new QuickBooksTokenResponse(
            root.GetProperty("access_token").GetString() ?? string.Empty,
            root.GetProperty("refresh_token").GetString() ?? string.Empty,
            root.TryGetProperty("expires_in", out var expiresIn) ? expiresIn.GetInt32() : 3600,
            root.TryGetProperty("x_refresh_token_expires_in", out var refreshExpiresIn) ? refreshExpiresIn.GetInt32() : 8726400);
    }

    private async Task<string> GetValidAccessTokenAsync(QuickBooksConnection connection, CancellationToken cancellationToken)
    {
        if (connection.AccessTokenExpiresAtUtc > DateTime.UtcNow.AddMinutes(5)
            && !string.IsNullOrWhiteSpace(connection.AccessToken))
        {
            return connection.AccessToken;
        }

        var tokenResponse = await RefreshAccessTokenAsync(connection.RefreshToken, cancellationToken);
        await SaveQuickBooksTokensAsync(connection.RealmId, tokenResponse, cancellationToken);
        return tokenResponse.AccessToken;
    }

    private async Task<QuickBooksTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException("No QuickBooks refresh token is stored.");
        }

        var clientId = GetRequiredConfig("ClientId");
        var clientSecret = GetRequiredConfig("ClientSecret");
        var client = _httpClientFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"QuickBooks token refresh failed with status {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        return new QuickBooksTokenResponse(
            root.GetProperty("access_token").GetString() ?? string.Empty,
            root.GetProperty("refresh_token").GetString() ?? refreshToken,
            root.TryGetProperty("expires_in", out var expiresIn) ? expiresIn.GetInt32() : 3600,
            root.TryGetProperty("x_refresh_token_expires_in", out var refreshExpiresIn) ? refreshExpiresIn.GetInt32() : 8726400);
    }

    private async Task<IReadOnlyList<QuickBooksCustomerViewModel>> GetCustomersAsync(string realmId, string accessToken, CancellationToken cancellationToken)
    {
        var customers = await FetchQuickBooksCustomersAsync(realmId, accessToken, cancellationToken);

        return customers
            .Select(customer => new QuickBooksCustomerViewModel
            {
                Id = GetJsonString(customer, "Id"),
                DisplayName = GetJsonString(customer, "DisplayName"),
                CompanyName = GetJsonString(customer, "CompanyName"),
                GivenName = GetJsonString(customer, "GivenName"),
                FamilyName = GetJsonString(customer, "FamilyName"),
                Email = GetNestedJsonString(customer, "PrimaryEmailAddr", "Address"),
                Phone = GetNestedJsonString(customer, "PrimaryPhone", "FreeFormNumber"),
                Active = customer.TryGetProperty("Active", out var active) ? active.ToString() : string.Empty
            })
            .ToList();
    }

    private async Task<IReadOnlyList<JsonElement>> FetchQuickBooksCustomersAsync(string realmId, string accessToken, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var baseUrl = string.Equals(GetEnvironment(), "production", StringComparison.OrdinalIgnoreCase)
            ? "https://quickbooks.api.intuit.com"
            : "https://sandbox-quickbooks.api.intuit.com";
        const int pageSize = 100;
        var startPosition = 1;
        var results = new List<JsonElement>();

        while (true)
        {
            var query = Uri.EscapeDataString($"select * from Customer order by DisplayName STARTPOSITION {startPosition} MAXRESULTS {pageSize}");

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/v3/company/{realmId}/query?query={query}&minorversion=75");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"QuickBooks Customer query failed with status {(int)response.StatusCode}: {body}");
            }

            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("QueryResponse", out var queryResponse)
                || !queryResponse.TryGetProperty("Customer", out var customers)
                || customers.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var page = customers.EnumerateArray().Select(customer => customer.Clone()).ToList();
            results.AddRange(page);

            if (page.Count < pageSize)
            {
                break;
            }

            startPosition += pageSize;
        }

        return results;
    }

    private async Task<IReadOnlyList<JsonElement>> FetchQuickBooksEmployeesAsync(string realmId, string accessToken, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var baseUrl = string.Equals(GetEnvironment(), "production", StringComparison.OrdinalIgnoreCase)
            ? "https://quickbooks.api.intuit.com"
            : "https://sandbox-quickbooks.api.intuit.com";
        const int pageSize = 100;
        var startPosition = 1;
        var results = new List<JsonElement>();

        while (true)
        {
            var query = Uri.EscapeDataString($"select * from Employee order by DisplayName STARTPOSITION {startPosition} MAXRESULTS {pageSize}");

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/v3/company/{realmId}/query?query={query}&minorversion=75");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"QuickBooks Employee query failed with status {(int)response.StatusCode}: {body}");
            }

            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("QueryResponse", out var queryResponse)
                || !queryResponse.TryGetProperty("Employee", out var employees)
                || employees.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var page = employees.EnumerateArray().Select(employee => employee.Clone()).ToList();
            results.AddRange(page);

            if (page.Count < pageSize)
            {
                break;
            }

            startPosition += pageSize;
        }

        return results;
    }

    private async Task<QuickBooksConnection> LoadConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.AppSettings
            .AsNoTracking()
            .Where(setting => setting.Name.StartsWith("QuickBooks:"))
            .ToDictionaryAsync(setting => setting.Name, setting => setting.Value, cancellationToken);

        settings.TryGetValue("QuickBooks:RealmId", out var realmId);
        settings.TryGetValue("QuickBooks:AccessToken", out var accessToken);
        settings.TryGetValue("QuickBooks:RefreshToken", out var refreshToken);
        settings.TryGetValue("QuickBooks:AccessTokenExpiresAtUtc", out var accessTokenExpiresAtValue);

        return new QuickBooksConnection(
            realmId ?? string.Empty,
            accessToken ?? string.Empty,
            refreshToken ?? string.Empty,
            ParseUtc(accessTokenExpiresAtValue));
    }

    private async Task UpsertCustomerAsync(JsonElement quickBooksCustomer, DateTime syncDateUtc, CancellationToken cancellationToken)
    {
        var quickBooksCustomerId = GetJsonString(quickBooksCustomer, "Id");
        if (string.IsNullOrWhiteSpace(quickBooksCustomerId))
        {
            return;
        }

        var customer = await _dbContext.Customers
            .Include(item => item.BillAddress)
            .Include(item => item.ShipAddress)
            .FirstOrDefaultAsync(item => item.QuickBooksCustomerId == quickBooksCustomerId, cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                QuickBooksCustomerId = quickBooksCustomerId,
                CreatedDate = syncDateUtc
            };
            _dbContext.Customers.Add(customer);
        }

        customer.SyncToken = GetJsonString(quickBooksCustomer, "SyncToken");
        customer.DisplayName = GetJsonString(quickBooksCustomer, "DisplayName");
        customer.FullyQualifiedName = GetJsonString(quickBooksCustomer, "FullyQualifiedName");
        customer.CompanyName = GetJsonString(quickBooksCustomer, "CompanyName");
        customer.Title = GetJsonString(quickBooksCustomer, "Title");
        customer.GivenName = GetJsonString(quickBooksCustomer, "GivenName");
        customer.MiddleName = GetJsonString(quickBooksCustomer, "MiddleName");
        customer.FamilyName = GetJsonString(quickBooksCustomer, "FamilyName");
        customer.Suffix = GetJsonString(quickBooksCustomer, "Suffix");
        customer.PrintOnCheckName = GetJsonString(quickBooksCustomer, "PrintOnCheckName");
        customer.PrimaryEmailAddress = GetNestedJsonString(quickBooksCustomer, "PrimaryEmailAddr", "Address");
        customer.PrimaryPhone = GetNestedJsonString(quickBooksCustomer, "PrimaryPhone", "FreeFormNumber");
        customer.AlternatePhone = GetNestedJsonString(quickBooksCustomer, "AlternatePhone", "FreeFormNumber");
        customer.MobilePhone = GetNestedJsonString(quickBooksCustomer, "Mobile", "FreeFormNumber");
        customer.Fax = GetNestedJsonString(quickBooksCustomer, "Fax", "FreeFormNumber");
        customer.Website = GetNestedJsonString(quickBooksCustomer, "WebAddr", "URI");
        customer.Notes = GetJsonString(quickBooksCustomer, "Notes");
        customer.Active = GetJsonBool(quickBooksCustomer, "Active");
        customer.Taxable = GetJsonBool(quickBooksCustomer, "Taxable");
        customer.Job = GetJsonBool(quickBooksCustomer, "Job");
        customer.BillWithParent = GetJsonBool(quickBooksCustomer, "BillWithParent");
        customer.Balance = GetJsonDecimal(quickBooksCustomer, "Balance");
        customer.BalanceWithJobs = GetJsonDecimal(quickBooksCustomer, "BalanceWithJobs");
        customer.PreferredDeliveryMethod = GetJsonString(quickBooksCustomer, "PreferredDeliveryMethod");
        customer.ParentRefValue = GetNestedJsonString(quickBooksCustomer, "ParentRef", "value");
        customer.ParentRefName = GetNestedJsonString(quickBooksCustomer, "ParentRef", "name");
        customer.PaymentMethodRefValue = GetNestedJsonString(quickBooksCustomer, "PaymentMethodRef", "value");
        customer.PaymentMethodRefName = GetNestedJsonString(quickBooksCustomer, "PaymentMethodRef", "name");
        customer.SalesTermRefValue = GetNestedJsonString(quickBooksCustomer, "SalesTermRef", "value");
        customer.SalesTermRefName = GetNestedJsonString(quickBooksCustomer, "SalesTermRef", "name");
        customer.CurrencyRefValue = GetNestedJsonString(quickBooksCustomer, "CurrencyRef", "value");
        customer.CurrencyRefName = GetNestedJsonString(quickBooksCustomer, "CurrencyRef", "name");
        customer.QuickBooksCreateTime = GetNestedJsonDateTime(quickBooksCustomer, "MetaData", "CreateTime");
        customer.QuickBooksLastUpdatedTime = GetNestedJsonDateTime(quickBooksCustomer, "MetaData", "LastUpdatedTime");
        customer.LastSyncDate = syncDateUtc;

        customer.BillAddress = UpsertAddress(customer.BillAddress, quickBooksCustomer, "BillAddr");
        if (customer.BillAddress is null)
        {
            customer.BillAddressId = null;
        }

        customer.ShipAddress = UpsertAddress(customer.ShipAddress, quickBooksCustomer, "ShipAddr");
        if (customer.ShipAddress is null)
        {
            customer.ShipAddressId = null;
        }
    }

    private async Task UpsertEmployeeAsync(JsonElement quickBooksEmployee, DateTime syncDateUtc, CancellationToken cancellationToken)
    {
        var quickBooksEmployeeId = GetJsonString(quickBooksEmployee, "Id");
        if (string.IsNullOrWhiteSpace(quickBooksEmployeeId))
        {
            return;
        }

        var employee = await _dbContext.Employees
            .Include(item => item.PrimaryAddress)
            .FirstOrDefaultAsync(item => item.QuickBooksEmployeeId == quickBooksEmployeeId, cancellationToken);

        if (employee is null)
        {
            employee = new Employee
            {
                QuickBooksEmployeeId = quickBooksEmployeeId,
                CreatedDate = syncDateUtc
            };
            _dbContext.Employees.Add(employee);
        }

        employee.SyncToken = GetJsonString(quickBooksEmployee, "SyncToken");
        employee.DisplayName = GetJsonString(quickBooksEmployee, "DisplayName");
        employee.PrintOnCheckName = GetJsonString(quickBooksEmployee, "PrintOnCheckName");
        employee.Title = GetJsonString(quickBooksEmployee, "Title");
        employee.GivenName = GetJsonString(quickBooksEmployee, "GivenName");
        employee.MiddleName = GetJsonString(quickBooksEmployee, "MiddleName");
        employee.FamilyName = GetJsonString(quickBooksEmployee, "FamilyName");
        employee.Suffix = GetJsonString(quickBooksEmployee, "Suffix");
        employee.PrimaryEmailAddress = GetNestedJsonString(quickBooksEmployee, "PrimaryEmailAddr", "Address");
        employee.PrimaryPhone = GetNestedJsonString(quickBooksEmployee, "PrimaryPhone", "FreeFormNumber");
        employee.MobilePhone = GetNestedJsonString(quickBooksEmployee, "Mobile", "FreeFormNumber");
        employee.Active = GetJsonBool(quickBooksEmployee, "Active");
        employee.BillableTime = GetJsonBool(quickBooksEmployee, "BillableTime");
        employee.EmployeeNumber = GetJsonString(quickBooksEmployee, "EmployeeNumber");
        employee.Organization = GetJsonString(quickBooksEmployee, "Organization");
        employee.Gender = GetJsonString(quickBooksEmployee, "Gender");
        employee.HiredDate = GetJsonDateTime(quickBooksEmployee, "HiredDate");
        employee.ReleasedDate = GetJsonDateTime(quickBooksEmployee, "ReleasedDate");
        employee.BirthDate = GetJsonDateTime(quickBooksEmployee, "BirthDate");
        employee.BillRate = GetJsonDecimal(quickBooksEmployee, "BillRate");
        employee.HourlyCostRate = GetJsonDecimal(quickBooksEmployee, "HourlyCostRate");
        employee.QuickBooksCreateTime = GetNestedJsonDateTime(quickBooksEmployee, "MetaData", "CreateTime");
        employee.QuickBooksLastUpdatedTime = GetNestedJsonDateTime(quickBooksEmployee, "MetaData", "LastUpdatedTime");
        employee.LastSyncDate = syncDateUtc;

        employee.PrimaryAddress = UpsertAddress(employee.PrimaryAddress, quickBooksEmployee, "PrimaryAddr");
        if (employee.PrimaryAddress is null)
        {
            employee.PrimaryAddressId = null;
        }
    }

    private Address? UpsertAddress(Address? address, JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out var addressElement) || addressElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (address is null)
        {
            address = new Address();
            _dbContext.Addresses.Add(address);
        }

        address.Street1 = GetJsonString(addressElement, "Line1");
        address.Street2 = GetJsonString(addressElement, "Line2");
        address.Street3 = GetJsonString(addressElement, "Line3");
        address.City = GetJsonString(addressElement, "City");
        address.State = GetJsonString(addressElement, "CountrySubDivisionCode");
        address.CountrySubDivisionCode = GetJsonString(addressElement, "CountrySubDivisionCode");
        address.PostalCode = GetJsonString(addressElement, "PostalCode");
        address.Country = GetJsonString(addressElement, "Country");
        return address;
    }

    private async Task SaveQuickBooksTokensAsync(string realmId, QuickBooksTokenResponse tokenResponse, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var values = new Dictionary<string, string>
        {
            ["QuickBooks:RealmId"] = realmId,
            ["QuickBooks:AccessToken"] = tokenResponse.AccessToken,
            ["QuickBooks:RefreshToken"] = tokenResponse.RefreshToken,
            ["QuickBooks:AccessTokenExpiresAtUtc"] = now.AddSeconds(tokenResponse.ExpiresInSeconds).ToString("O"),
            ["QuickBooks:RefreshTokenExpiresAtUtc"] = now.AddSeconds(tokenResponse.RefreshTokenExpiresInSeconds).ToString("O"),
            ["QuickBooks:ConnectedAtUtc"] = now.ToString("O"),
            ["QuickBooks:Environment"] = GetEnvironment()
        };

        foreach (var value in values)
        {
            await UpsertSettingAsync(value.Key, value.Value, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertSettingAsync(string name, string value, CancellationToken cancellationToken)
    {
        var setting = await _dbContext.AppSettings
            .FirstOrDefaultAsync(item => item.Name == name, cancellationToken);

        if (setting is null)
        {
            _dbContext.AppSettings.Add(new AppSetting
            {
                Name = name,
                Value = value
            });
            return;
        }

        setting.Value = value;
    }

    private static DateTime ParseUtc(string? value)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : DateTime.MinValue;
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ToString()
            : string.Empty;
    }

    private static string GetNestedJsonString(JsonElement element, string propertyName, string nestedPropertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? GetJsonString(property, nestedPropertyName)
            : string.Empty;
    }

    private static bool GetJsonBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.True;
    }

    private static decimal GetJsonDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetDecimal(out var value) ? value : 0,
            JsonValueKind.String => decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0,
            _ => 0
        };
    }

    private static DateTime? GetNestedJsonDateTime(JsonElement element, string propertyName, string nestedPropertyName)
    {
        var value = GetNestedJsonString(element, propertyName, nestedPropertyName);
        return ParseNullableDateTime(value);
    }

    private static DateTime? GetJsonDateTime(JsonElement element, string propertyName)
    {
        var value = GetJsonString(element, propertyName);
        return ParseNullableDateTime(value);
    }

    private static DateTime? ParseNullableDateTime(string value)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private string GetRequiredConfig(string name)
    {
        return _configuration[$"QuickBooks:{name}"] ?? string.Empty;
    }

    private string GetEnvironment()
    {
        var environment = _configuration["QuickBooks:Environment"];
        return string.IsNullOrWhiteSpace(environment) ? "sandbox" : environment;
    }

    private string GetRedirectUri()
    {
        var configuredRedirectUri = _configuration["QuickBooks:RedirectUri"];
        if (!string.IsNullOrWhiteSpace(configuredRedirectUri))
        {
            return configuredRedirectUri;
        }

        return Url.Action("Callback", "QuickBooks", null, Request.Scheme, Request.Host.ToString()) ?? string.Empty;
    }

    private static string CreateState()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    private sealed record QuickBooksTokenResponse(
        string AccessToken,
        string RefreshToken,
        int ExpiresInSeconds,
        int RefreshTokenExpiresInSeconds);

    private sealed record QuickBooksConnection(
        string RealmId,
        string AccessToken,
        string RefreshToken,
        DateTime AccessTokenExpiresAtUtc)
    {
        public bool IsConnected => !string.IsNullOrWhiteSpace(RealmId)
            && !string.IsNullOrWhiteSpace(RefreshToken);
    }
}
