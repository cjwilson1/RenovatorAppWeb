using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
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
        var apiKey = GetSetting("Resend:ApiKey", "Resend__ApiKey", "RESEND_API_KEY");
        var fromEmail = GetSetting("Resend:FromEmail", "Resend__FromEmail", "RESEND_FROM_EMAIL");
        var fromName = GetSetting("Resend:FromName", "Resend__FromName", "RESEND_FROM_NAME")
            ?? "RenovatorApp Website";

        if (string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException(BuildMissingSettingsMessage(apiKey, fromEmail));
        }

        var body = await BuildBodyAsync(firstName, companyName, inviteLink, expirationHours, cancellationToken);
        var htmlBody = BuildHtmlBody(firstName, companyName, inviteLink, expirationHours);
        var subject = $"Welcome to Renovator App for {companyName}";

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new ResendEmailRequest(
            From: $"{fromName} <{fromEmail}>",
            To: [toEmail],
            Subject: subject,
            Text: body,
            Html: htmlBody));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Resend email failed with HTTP {(int)response.StatusCode}: {responseBody}");
        }
    }

    private string? GetSetting(params string[] names)
    {
        foreach (var name in names)
        {
            var value = _configuration[name] ?? Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim().Trim('"');
            }
        }

        return null;
    }

    private string BuildMissingSettingsMessage(string? apiKey, string? fromEmail)
    {
        var missingSettings = new List<string>();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            missingSettings.Add("API key");
        }

        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            missingSettings.Add("from email");
        }

        var visibleResendSettings = _configuration.AsEnumerable()
            .Select(setting => setting.Key)
            .Where(key => key.StartsWith("Resend", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("RESEND", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var visibleSettingsText = visibleResendSettings.Length == 0
            ? "none"
            : string.Join(", ", visibleResendSettings);

        return $"Resend email settings are not configured. Missing: {string.Join(", ", missingSettings)}. Visible Resend setting names: {visibleSettingsText}.";
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

    private static string BuildHtmlBody(string firstName, string companyName, string inviteLink, int expirationHours)
    {
        var encodedFirstName = WebUtility.HtmlEncode(firstName);
        var encodedCompanyName = WebUtility.HtmlEncode(companyName);
        var encodedInviteLink = WebUtility.HtmlEncode(inviteLink);
        var encodedExpirationHours = WebUtility.HtmlEncode(expirationHours.ToString());

        return $"""
            <!doctype html>
            <html lang="en">
            <body style="font-family: Arial, sans-serif; color: #1f2933; line-height: 1.5;">
                <p>Hi {encodedFirstName},</p>
                <p>Welcome to Renovator App for {encodedCompanyName}.</p>
                <p>Your account has been created. Use this secure link to set your password and sign in for the first time:</p>
                <p>
                    <a href="{encodedInviteLink}" style="color: #0d6efd;">Set your password</a>
                </p>
                <p>If the button does not work, copy and paste this link into your browser:</p>
                <p><a href="{encodedInviteLink}" style="color: #0d6efd;">{encodedInviteLink}</a></p>
                <p>This link expires in {encodedExpirationHours} hours. If you were not expecting this email, you can ignore it.</p>
            </body>
            </html>
            """;
    }

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("html")] string Html);
}
