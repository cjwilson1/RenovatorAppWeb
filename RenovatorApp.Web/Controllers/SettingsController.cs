using Microsoft.AspNetCore.Mvc;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Infrastructure.Services;
using RenovatorApp.Web.ViewModels;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RenovatorApp.Web.Controllers;

public sealed class SettingsController : Controller
{
    private static readonly int[] PageSizeOptions = [10, 15, 25, 50, 100];
    private readonly InspectionDataService _inspectionDataService;
    private readonly IHttpClientFactory _httpClientFactory;

    public SettingsController(InspectionDataService inspectionDataService, IHttpClientFactory httpClientFactory)
    {
        _inspectionDataService = inspectionDataService;
        _httpClientFactory = httpClientFactory;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> PartsManager(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (!PageSizeOptions.Contains(pageSize))
        {
            pageSize = 10;
        }

        var parts = await _inspectionDataService.GetPartsAsync(cancellationToken);
        var totalParts = parts.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalParts / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        return View(new PartsManagerViewModel
        {
            Parts = parts
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToPartViewModel)
                .ToList(),
            Page = page,
            PageSize = pageSize,
            TotalParts = totalParts,
            TotalPages = totalPages,
            PageSizeOptions = PageSizeOptions
        });
    }

    public async Task<IActionResult> FindPart(CancellationToken cancellationToken)
    {
        var parts = await _inspectionDataService.GetPartsAsync(cancellationToken);

        return View(parts.Select(ToPartViewModel).ToList());
    }

    public IActionResult AddPart()
    {
        return View(new AddPartViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPart(AddPartViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Url))
        {
            model.ErrorMessage = "Enter a URL before clicking Find.";
            return View(model);
        }

        if (!Uri.TryCreate(model.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            model.ErrorMessage = "Enter a valid http or https URL.";
            return View(model);
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Browser");
            client.Timeout = TimeSpan.FromSeconds(20);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36");
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            request.Headers.Referrer = new Uri("https://www.homedepot.com/");
            request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "none");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                model.ErrorMessage = response.StatusCode == HttpStatusCode.Forbidden
                    ? "The page refused the lookup request with a 403 Forbidden response. This usually means the retailer is blocking server-side scraping for that page."
                    : $"The page could not be opened. Status code: {(int)response.StatusCode}.";
                return View(model);
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var scraped = ScrapePartPage(html);

            model.Manufacturer = scraped.Manufacturer;
            model.Title = scraped.Title;
            model.Price = scraped.Price;
            model.Description = scraped.Description;
            model.Sku = scraped.Sku;
            model.ModelNumber = scraped.ModelNumber;

            if (!scraped.HasAnyValue)
            {
                model.ErrorMessage = "The page opened, but product details could not be found in the page HTML.";
            }
        }
        catch (TaskCanceledException)
        {
            model.ErrorMessage = "The page request timed out.";
        }
        catch (HttpRequestException exception)
        {
            model.ErrorMessage = $"The page could not be opened. {exception.Message}";
        }

        return View(model);
    }

    private static PartsManagerPartViewModel ToPartViewModel(Part part)
    {
        return new PartsManagerPartViewModel
        {
            Id = part.PartId,
            Name = part.Name,
            Description = part.Description,
            SourceName = part.PartSource?.Name ?? string.Empty,
            Sku = part.Sku,
            Manufacturer = part.Manufacturer,
            Cost = part.Cost,
            Url = part.Url,
            IsPackage = part.IsPackage,
            PackageUnits = part.PackageUnits
        };
    }

    private static ScrapedPartData ScrapePartPage(string html)
    {
        var data = new ScrapedPartData();

        foreach (var json in ExtractJsonLdBlocks(html))
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                foreach (var product in EnumerateProductObjects(document.RootElement))
                {
                    ApplyJsonLdProduct(data, product);
                }
            }
            catch (JsonException)
            {
                // Some pages include multiple JSON fragments in one script tag. Fall back to HTML parsing.
            }
        }

        data.Title = FirstValue(data.Title, GetMetaContent(html, "og:title"), GetMetaContent(html, "twitter:title"), GetTitle(html));
        data.Description = FirstValue(data.Description, FirstParagraph(GetMetaContent(html, "description")), FirstParagraph(GetMetaContent(html, "og:description")));
        data.Price = FirstValue(data.Price, GetMetaContent(html, "product:price:amount"), MatchJsonString(html, "price"), MatchJsonString(html, "currentPrice"));
        data.Manufacturer = FirstValue(data.Manufacturer, MatchJsonString(html, "brandName"), MatchJsonString(html, "manufacturer"));
        data.Sku = FirstValue(data.Sku, MatchJsonString(html, "skuNumber"), MatchJsonString(html, "sku"));
        data.ModelNumber = FirstValue(data.ModelNumber, MatchJsonString(html, "modelNumber"), MatchJsonString(html, "model"));

        return data.Trimmed();
    }

    private static IEnumerable<string> ExtractJsonLdBlocks(string html)
    {
        foreach (Match match in Regex.Matches(
            html,
            "<script[^>]+type=[\"']application/ld\\+json[\"'][^>]*>(.*?)</script>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            yield return WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
        }
    }

    private static IEnumerable<JsonElement> EnumerateProductObjects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (IsProductObject(element))
            {
                yield return element;
            }

            foreach (var property in element.EnumerateObject())
            {
                foreach (var product in EnumerateProductObjects(property.Value))
                {
                    yield return product;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var product in EnumerateProductObjects(item))
                {
                    yield return product;
                }
            }
        }
    }

    private static bool IsProductObject(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var type))
        {
            return false;
        }

        return type.ValueKind switch
        {
            JsonValueKind.String => string.Equals(type.GetString(), "Product", StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Array => type.EnumerateArray().Any(item => string.Equals(item.GetString(), "Product", StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private static void ApplyJsonLdProduct(ScrapedPartData data, JsonElement product)
    {
        data.Title = FirstValue(data.Title, GetJsonString(product, "name"));
        data.Description = FirstValue(data.Description, FirstParagraph(GetJsonString(product, "description")));
        data.Sku = FirstValue(data.Sku, GetJsonString(product, "sku"));
        data.ModelNumber = FirstValue(data.ModelNumber, GetJsonString(product, "mpn"), GetJsonString(product, "model"));

        if (product.TryGetProperty("brand", out var brand))
        {
            data.Manufacturer = FirstValue(data.Manufacturer, GetJsonString(brand, "name"), brand.ValueKind == JsonValueKind.String ? brand.GetString() : string.Empty);
        }

        if (product.TryGetProperty("offers", out var offers))
        {
            data.Price = FirstValue(data.Price, ExtractOfferPrice(offers));
        }
    }

    private static string ExtractOfferPrice(JsonElement offers)
    {
        if (offers.ValueKind == JsonValueKind.Array)
        {
            foreach (var offer in offers.EnumerateArray())
            {
                var price = ExtractOfferPrice(offer);
                if (!string.IsNullOrWhiteSpace(price))
                {
                    return price;
                }
            }
        }

        return FirstValue(GetJsonString(offers, "price"), GetJsonString(offers, "lowPrice"), GetJsonString(offers, "highPrice"));
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Number => property.GetRawText(),
            _ => string.Empty
        };
    }

    private static string GetMetaContent(string html, string name)
    {
        var escapedName = Regex.Escape(name);
        var match = Regex.Match(
            html,
            $"<meta\\s+(?=[^>]*(?:name|property)=[\"']{escapedName}[\"'])(?=[^>]*content=[\"'](?<content>.*?)[\"'])[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return CleanText(match.Groups["content"].Value);
    }

    private static string GetTitle(string html)
    {
        var match = Regex.Match(html, "<title[^>]*>(?<title>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return CleanText(match.Groups["title"].Value);
    }

    private static string MatchJsonString(string html, string propertyName)
    {
        var escapedName = Regex.Escape(propertyName);
        var match = Regex.Match(
            html,
            $"[\"']{escapedName}[\"']\\s*:\\s*(?:[\"'](?<value>.*?)[\"']|(?<value>\\d+(?:\\.\\d+)?))",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return CleanText(match.Groups["value"].Value);
    }

    private static string FirstParagraph(string value)
    {
        var clean = CleanText(value);
        if (string.IsNullOrWhiteSpace(clean))
        {
            return string.Empty;
        }

        var paragraphs = Regex.Split(clean, @"(?:\r?\n){2,}");
        return paragraphs.FirstOrDefault(paragraph => !string.IsNullOrWhiteSpace(paragraph)) ?? clean;
    }

    private static string FirstValue(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(value);
        var withoutTags = Regex.Replace(decoded, "<.*?>", " ", RegexOptions.Singleline);
        return Regex.Replace(withoutTags, "\\s+", " ").Trim();
    }

    private sealed class ScrapedPartData
    {
        public string Manufacturer { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string ModelNumber { get; set; } = string.Empty;

        public bool HasAnyValue => !string.IsNullOrWhiteSpace(Manufacturer)
            || !string.IsNullOrWhiteSpace(Title)
            || !string.IsNullOrWhiteSpace(Price)
            || !string.IsNullOrWhiteSpace(Description)
            || !string.IsNullOrWhiteSpace(Sku)
            || !string.IsNullOrWhiteSpace(ModelNumber);

        public ScrapedPartData Trimmed()
        {
            Manufacturer = CleanText(Manufacturer);
            Title = CleanText(Title);
            Price = CleanText(Price);
            Description = FirstParagraph(Description);
            Sku = CleanText(Sku);
            ModelNumber = CleanText(ModelNumber);
            return this;
        }
    }
}
