using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace RenovatorApp.Web.Services;

public sealed class NewEmployeeEmailService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public NewEmployeeEmailService(HttpClient httpClient, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task SendWelcomeEmailAsync(
        string toEmail,
        string firstName,
        string companyName,
        string inviteLink,
        int expirationHours,
        CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Resend:ApiKey"];
        var fromEmail = _configuration["Resend:FromEmail"];
        var fromName = _configuration["Resend:FromName"] ?? "RenovatorApp Website";

        if (string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException("Resend email settings are not configured.");
        }

        var body = await BuildBodyAsync(firstName, companyName, inviteLink, expirationHours, cancellationToken);
        var subject = $"Welcome to Renovator App for {companyName}";

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new ResendEmailRequest(
            From: $"{fromName} <{fromEmail}>",
            To: [toEmail],
            Subject: subject,
            Text: body));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Resend email failed with HTTP {(int)response.StatusCode}: {responseBody}");
        }
    }

    private async Task<string> BuildBodyAsync(
        string firstName,
        string companyName,
        string inviteLink,
        int expirationHours,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(_environment.WebRootPath, "emails", "new-employee-invite.txt");
        var body = await File.ReadAllTextAsync(path, cancellationToken);

        return body
            .Replace("{{FirstName}}", firstName, StringComparison.Ordinal)
            .Replace("{{CompanyName}}", companyName, StringComparison.Ordinal)
            .Replace("{{InviteLink}}", inviteLink, StringComparison.Ordinal)
            .Replace("{{ExpirationHours}}", expirationHours.ToString(), StringComparison.Ordinal);
    }

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("text")] string Text);
}
