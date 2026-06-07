using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Infrastructure.Services;
using RenovatorApp.Web.Services;
using RenovatorApp.Web.ViewModels;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RenovatorApp.Web.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
public sealed class SettingsController : Controller
{
    private static readonly int[] PageSizeOptions = [10, 15, 25, 50, 100];
    private static readonly JsonSerializerOptions PartsTransferJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    private readonly RenovatorAppDbContext _dbContext;
    private readonly InspectionDataService _inspectionDataService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CurrentUserSession _currentUserSession;

    public SettingsController(
        RenovatorAppDbContext dbContext,
        InspectionDataService inspectionDataService,
        IHttpClientFactory httpClientFactory,
        CurrentUserSession currentUserSession)
    {
        _dbContext = dbContext;
        _inspectionDataService = inspectionDataService;
        _httpClientFactory = httpClientFactory;
        _currentUserSession = currentUserSession;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var quickBooksSettings = await _dbContext.AppSettings
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .Where(setting => setting.Name.StartsWith("QuickBooks:"))
            .ToDictionaryAsync(setting => setting.Name, setting => setting.Value, cancellationToken);

        quickBooksSettings.TryGetValue("QuickBooks:RealmId", out var realmId);
        quickBooksSettings.TryGetValue("QuickBooks:AccessTokenExpiresAtUtc", out var accessTokenExpiresAtValue);
        var accessTokenExpiresAtUtc = DateTime.TryParse(
            accessTokenExpiresAtValue,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsedAccessTokenExpiresAtUtc)
            ? parsedAccessTokenExpiresAtUtc
            : (DateTime?)null;

        var isConfigured = !string.IsNullOrWhiteSpace(GetQuickBooksConfigValue("ClientId"))
            && !string.IsNullOrWhiteSpace(GetQuickBooksConfigValue("ClientSecret"));

        return View(new SettingsIndexViewModel
        {
            QuickBooks = new QuickBooksConnectionViewModel
            {
                IsConfigured = isConfigured,
                IsConnected = !string.IsNullOrWhiteSpace(realmId),
                RealmId = realmId ?? string.Empty,
                Environment = GetQuickBooksConfigValue("Environment") is { Length: > 0 } environment ? environment : "sandbox",
                AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
                StatusMessage = TempData["QuickBooksStatus"] as string ?? string.Empty
            }
        });
    }

    public async Task<IActionResult> PartsManager(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (!PageSizeOptions.Contains(pageSize))
        {
            pageSize = 10;
        }

        var parts = await _inspectionDataService.GetPartsAsync(_currentUserSession.RenoCompanyID, cancellationToken);
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
            PageSizeOptions = PageSizeOptions,
            StatusMessage = TempData["PartsManagerStatus"] as string ?? string.Empty
        });
    }

    public IActionResult Downloads()
    {
        return View();
    }

    public async Task<IActionResult> DefaultSettings(CancellationToken cancellationToken)
    {
        var defaultState = await _dbContext.AppSettings
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .Where(setting => setting.Name == "defaultstate")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return View(new DefaultSettingsViewModel
        {
            DefaultState = defaultState ?? string.Empty,
            States = StateOptionsProvider.GetStates(),
            StatusMessage = TempData["DefaultSettingsStatus"] as string ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DefaultSettings(DefaultSettingsViewModel update, CancellationToken cancellationToken)
    {
        var selectedState = (update.DefaultState ?? string.Empty).Trim().ToUpperInvariant();

        if (!StateOptionsProvider.GetStates().Any(state => state.Abbreviation == selectedState))
        {
            ModelState.AddModelError(nameof(update.DefaultState), "Choose a valid state.");
            return View(new DefaultSettingsViewModel
            {
                DefaultState = selectedState,
                States = StateOptionsProvider.GetStates()
            });
        }

        var setting = await _dbContext.AppSettings
            .ForCompany(_currentUserSession.RenoCompanyID)
            .FirstOrDefaultAsync(item => item.Name == "defaultstate", cancellationToken);

        if (setting is null)
        {
            setting = new AppSetting
            {
                RenoCompanyID = _currentUserSession.RenoCompanyID,
                Name = "defaultstate"
            };
            _dbContext.AppSettings.Add(setting);
        }

        setting.Value = selectedState;
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["DefaultSettingsStatus"] = "Default settings saved.";
        return RedirectToAction(nameof(DefaultSettings));
    }

    public async Task<IActionResult> FindPart(CancellationToken cancellationToken)
    {
        var parts = await _inspectionDataService.GetPartsAsync(_currentUserSession.RenoCompanyID, cancellationToken);

        return View(parts.Select(ToPartViewModel).ToList());
    }

    public async Task<IActionResult> EditPart(Guid id, CancellationToken cancellationToken)
    {
        var part = await _dbContext.Parts.ForCompany(_currentUserSession.RenoCompanyID).FirstOrDefaultAsync(item => item.PartId == id, cancellationToken);
        if (part is null)
        {
            return NotFound();
        }

        return View(new EditPartViewModel
        {
            PartId = part.PartId,
            Manufacturer = part.Manufacturer,
            Title = part.Name,
            Price = part.Cost.ToString("0.##", CultureInfo.InvariantCulture),
            Description = part.Description,
            Sku = part.Sku,
            ModelNumber = part.ModelNumber,
            ImageUrl = part.ImageUrl
        });
    }

    public IActionResult AddPart()
    {
        return View(new AddPartViewModel());
    }

    public async Task<IActionResult> ExportParts(CancellationToken cancellationToken)
    {
        var partSources = await _dbContext.PartSources
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .OrderBy(source => source.Name)
            .Select(source => new PartsTransferPartSource(
                source.PartSourceId,
                source.Name))
            .ToListAsync(cancellationToken);

        var parts = await _dbContext.Parts
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .OrderBy(part => part.Name)
            .Select(part => new PartsTransferPart(
                part.PartId,
                part.PartSourceId,
                part.Name,
                part.Description,
                part.ModelNumber,
                part.Manufacturer,
                part.Sku,
                part.Url,
                part.ImageUrl,
                part.Cost,
                part.IsPackage,
                part.PackageUnits))
            .ToListAsync(cancellationToken);

        var export = new PartsTransferFile(
            1,
            DateTime.UtcNow,
            partSources,
            parts);

        var json = JsonSerializer.Serialize(export, PartsTransferJsonOptions);
        var fileName = $"renovator-parts-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

        return File(Encoding.UTF8.GetBytes(json), "application/json", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportParts(IFormFile? importFile, CancellationToken cancellationToken)
    {
        if (importFile is null || importFile.Length == 0)
        {
            TempData["PartsManagerStatus"] = "Choose a parts export file before importing.";
            return RedirectToAction(nameof(PartsManager));
        }

        PartsTransferFile? import;
        try
        {
            await using var stream = importFile.OpenReadStream();
            import = await JsonSerializer.DeserializeAsync<PartsTransferFile>(stream, PartsTransferJsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            TempData["PartsManagerStatus"] = "The selected file is not a valid parts export file.";
            return RedirectToAction(nameof(PartsManager));
        }

        if (import is null || import.Version < 1)
        {
            TempData["PartsManagerStatus"] = "The selected file is not a valid parts export file.";
            return RedirectToAction(nameof(PartsManager));
        }

        var result = await ImportPartsAsync(import, cancellationToken);

        TempData["PartsManagerStatus"] =
            $"Import complete. Part sources: {result.PartSourcesCreated} added, {result.PartSourcesUpdated} updated. Parts: {result.PartsCreated} added, {result.PartsUpdated} updated.";

        return RedirectToAction(nameof(PartsManager));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPart(AddPartViewModel model, CancellationToken cancellationToken)
    {
        model.LookupDebugMessages ??= [];
        AddLookupDebug(model, "Find clicked.");

        if (string.IsNullOrWhiteSpace(model.Url))
        {
            AddLookupDebug(model, "URL validation failed: no URL was entered.");
            model.ErrorMessage = "Enter a URL before clicking Find.";
            return View(model);
        }

        if (!Uri.TryCreate(model.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            AddLookupDebug(model, $"URL validation failed: '{model.Url}' is not a valid http or https URL.");
            model.ErrorMessage = "Enter a valid http or https URL.";
            return View(model);
        }

        AddLookupDebug(model, $"URL validated: {uri}");

        try
        {
            AddLookupDebug(model, "Preparing fallback HTTP client request.");
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

            var scraped = await TryScrapeWithBrowserAsync(uri, model.LookupDebugMessages, cancellationToken);
            if (!scraped.HasAnyValue)
            {
                AddLookupDebug(model, "Playwright did not return product details. Trying fallback HTTP request.");
                using var response = await client.SendAsync(request, cancellationToken);
                AddLookupDebug(model, $"Fallback HTTP response: {(int)response.StatusCode} {response.ReasonPhrase}.");

                if (!response.IsSuccessStatusCode)
                {
                    model.ErrorMessage = response.StatusCode == HttpStatusCode.Forbidden
                        ? "The page refused the lookup request with a 403 Forbidden response. Playwright also could not extract product details from the rendered page."
                        : $"The page could not be opened. Status code: {(int)response.StatusCode}.";
                    return View(model);
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                AddLookupDebug(model, $"Fallback HTTP returned {html.Length:N0} characters of HTML.");
                scraped = ScrapePartPage(html);
                AddLookupDebug(model, scraped.HasAnyValue
                    ? "Fallback HTML scraper found product details."
                    : "Fallback HTML scraper did not find product details.");
            }
            else
            {
                AddLookupDebug(model, "Playwright found product details.");
            }

            model.Manufacturer = scraped.Manufacturer;
            model.Title = scraped.Title;
            model.Price = scraped.Price;
            model.Description = scraped.Description;
            model.Sku = scraped.Sku;
            model.ModelNumber = scraped.ModelNumber;
            ModelState.Clear();
            AddLookupDebug(model, "Copied extracted values into the Add Part form fields.");

            if (!scraped.HasAnyValue)
            {
                AddLookupDebug(model, "Lookup finished without any extracted values.");
                model.ErrorMessage = "The page opened, but product details could not be found in the page HTML.";
            }
            else
            {
                AddLookupDebug(model, $"Lookup finished. Filled fields: {CountFilledFields(scraped)} of 6.");
            }
        }
        catch (TaskCanceledException)
        {
            AddLookupDebug(model, "The lookup was canceled or timed out.");
            model.ErrorMessage = "The page request timed out.";
        }
        catch (HttpRequestException exception)
        {
            AddLookupDebug(model, $"HTTP request error: {exception.Message}");
            model.ErrorMessage = $"The page could not be opened. {exception.Message}";
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePart(AddPartViewModel model, CancellationToken cancellationToken)
    {
        NormalizeAddPartModel(model);

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            model.ErrorMessage = "Title of product is required before adding the part.";
            ModelState.Clear();
            return View("AddPart", model);
        }

        var sourceName = GetPartSourceName(model.Url);
        var source = await _dbContext.PartSources
            .ForCompany(_currentUserSession.RenoCompanyID)
            .FirstOrDefaultAsync(partSource => partSource.Name == sourceName, cancellationToken);

        if (source is null)
        {
            source = new PartSource
            {
                RenoCompanyID = _currentUserSession.RenoCompanyID,
                Name = sourceName
            };

            _dbContext.PartSources.Add(source);
        }

        _dbContext.Parts.Add(new Part
        {
            RenoCompanyID = _currentUserSession.RenoCompanyID,
            PartSourceId = source.PartSourceId,
            Name = model.Title,
            Description = model.Description,
            ModelNumber = model.ModelNumber,
            Manufacturer = model.Manufacturer,
            Sku = model.Sku,
            Url = model.Url,
            ImageUrl = model.ImageUrl,
            Cost = ParseCost(model.Price),
            IsPackage = false,
            PackageUnits = 0
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(PartsManager));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPart(EditPartViewModel model, CancellationToken cancellationToken)
    {
        NormalizeEditPartModel(model);

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            model.ErrorMessage = "Title of product is required before saving the part.";
            ModelState.Clear();
            return View(model);
        }

        var part = await _dbContext.Parts.ForCompany(_currentUserSession.RenoCompanyID).FirstOrDefaultAsync(item => item.PartId == model.PartId, cancellationToken);
        if (part is null)
        {
            return NotFound();
        }

        part.Name = model.Title;
        part.Description = model.Description;
        part.ModelNumber = model.ModelNumber;
        part.Manufacturer = model.Manufacturer;
        part.Sku = model.Sku;
        part.ImageUrl = model.ImageUrl;
        part.Cost = ParseCost(model.Price);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(PartsManager));
    }

    private static void NormalizeAddPartModel(AddPartViewModel model)
    {
        model.Url = model.Url?.Trim() ?? string.Empty;
        model.Manufacturer = model.Manufacturer?.Trim() ?? string.Empty;
        model.Title = model.Title?.Trim() ?? string.Empty;
        model.Price = model.Price?.Trim() ?? string.Empty;
        model.Description = model.Description?.Trim() ?? string.Empty;
        model.Sku = model.Sku?.Trim() ?? string.Empty;
        model.ModelNumber = model.ModelNumber?.Trim() ?? string.Empty;
        model.ImageUrl = model.ImageUrl?.Trim() ?? string.Empty;
        model.ErrorMessage = model.ErrorMessage?.Trim() ?? string.Empty;
        model.LookupDebugMessages ??= [];
    }

    private static void NormalizeEditPartModel(EditPartViewModel model)
    {
        model.Manufacturer = model.Manufacturer?.Trim() ?? string.Empty;
        model.Title = model.Title?.Trim() ?? string.Empty;
        model.Price = model.Price?.Trim() ?? string.Empty;
        model.Description = model.Description?.Trim() ?? string.Empty;
        model.Sku = model.Sku?.Trim() ?? string.Empty;
        model.ModelNumber = model.ModelNumber?.Trim() ?? string.Empty;
        model.ImageUrl = model.ImageUrl?.Trim() ?? string.Empty;
        model.ErrorMessage = model.ErrorMessage?.Trim() ?? string.Empty;
    }

    private async Task<PartsImportResult> ImportPartsAsync(PartsTransferFile import, CancellationToken cancellationToken)
    {
        var existingSources = await _dbContext.PartSources.ForCompany(_currentUserSession.RenoCompanyID).ToListAsync(cancellationToken);
        var sourceIdMap = new Dictionary<Guid, Guid>();
        var result = new PartsImportResult();

        foreach (var importedSource in import.PartSources)
        {
            var importedName = TrimTransferText(importedSource.Name);
            if (string.IsNullOrWhiteSpace(importedName))
            {
                continue;
            }

            var source = existingSources.FirstOrDefault(item => item.PartSourceId == importedSource.PartSourceId)
                ?? existingSources.FirstOrDefault(item => item.Name.Equals(importedName, StringComparison.OrdinalIgnoreCase));

            if (source is null)
            {
                source = new PartSource
                {
                    PartSourceId = importedSource.PartSourceId,
                    RenoCompanyID = _currentUserSession.RenoCompanyID,
                    Name = importedName
                };

                _dbContext.PartSources.Add(source);
                existingSources.Add(source);
                result.PartSourcesCreated++;
            }
            else
            {
                if (source.Name != importedName)
                {
                    source.Name = importedName;
                }

                result.PartSourcesUpdated++;
            }

            sourceIdMap[importedSource.PartSourceId] = source.PartSourceId;
        }

        var existingParts = await _dbContext.Parts.ForCompany(_currentUserSession.RenoCompanyID).ToListAsync(cancellationToken);

        foreach (var importedPart in import.Parts)
        {
            var normalizedPart = NormalizeTransferPart(importedPart);
            if (string.IsNullOrWhiteSpace(normalizedPart.Name))
            {
                continue;
            }

            if (!sourceIdMap.TryGetValue(normalizedPart.PartSourceId, out var mappedSourceId))
            {
                continue;
            }

            var part = FindExistingPart(existingParts, normalizedPart);

            if (part is null)
            {
                part = new Part
                {
                    PartId = normalizedPart.PartId,
                    RenoCompanyID = _currentUserSession.RenoCompanyID,
                    PartSourceId = mappedSourceId
                };

                _dbContext.Parts.Add(part);
                existingParts.Add(part);
                result.PartsCreated++;
            }
            else
            {
                result.PartsUpdated++;
            }

            part.PartSourceId = mappedSourceId;
            part.Name = normalizedPart.Name;
            part.Description = normalizedPart.Description;
            part.ModelNumber = normalizedPart.ModelNumber;
            part.Manufacturer = normalizedPart.Manufacturer;
            part.Sku = normalizedPart.Sku;
            part.Url = normalizedPart.Url;
            part.ImageUrl = normalizedPart.ImageUrl;
            part.Cost = normalizedPart.Cost;
            part.IsPackage = normalizedPart.IsPackage;
            part.PackageUnits = normalizedPart.PackageUnits;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static Part? FindExistingPart(IReadOnlyList<Part> existingParts, PartsTransferPart importedPart)
    {
        var byId = existingParts.FirstOrDefault(part => part.PartId == importedPart.PartId);
        if (byId is not null)
        {
            return byId;
        }

        if (!string.IsNullOrWhiteSpace(importedPart.Url))
        {
            var byUrl = existingParts.FirstOrDefault(part => part.Url.Equals(importedPart.Url, StringComparison.OrdinalIgnoreCase));
            if (byUrl is not null)
            {
                return byUrl;
            }
        }

        if (!string.IsNullOrWhiteSpace(importedPart.Sku) && !string.IsNullOrWhiteSpace(importedPart.Manufacturer))
        {
            var bySku = existingParts.FirstOrDefault(part =>
                part.Sku.Equals(importedPart.Sku, StringComparison.OrdinalIgnoreCase)
                && part.Manufacturer.Equals(importedPart.Manufacturer, StringComparison.OrdinalIgnoreCase));

            if (bySku is not null)
            {
                return bySku;
            }
        }

        if (!string.IsNullOrWhiteSpace(importedPart.ModelNumber) && !string.IsNullOrWhiteSpace(importedPart.Manufacturer))
        {
            return existingParts.FirstOrDefault(part =>
                part.ModelNumber.Equals(importedPart.ModelNumber, StringComparison.OrdinalIgnoreCase)
                && part.Manufacturer.Equals(importedPart.Manufacturer, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static PartsTransferPart NormalizeTransferPart(PartsTransferPart part)
    {
        return part with
        {
            Name = TrimTransferText(part.Name),
            Description = TrimTransferText(part.Description),
            ModelNumber = TrimTransferText(part.ModelNumber),
            Manufacturer = TrimTransferText(part.Manufacturer),
            Sku = TrimTransferText(part.Sku),
            Url = TrimTransferText(part.Url),
            ImageUrl = TrimTransferText(part.ImageUrl),
            PackageUnits = Math.Max(0, part.PackageUnits)
        };
    }

    private static string TrimTransferText(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static async Task<ScrapedPartData> TryScrapeWithBrowserAsync(Uri uri, ICollection<string> debugMessages, CancellationToken cancellationToken)
    {
        try
        {
            AddLookupDebug(debugMessages, "Starting Playwright.");
            using var playwright = await Playwright.CreateAsync();
            var context = await LaunchPersistentBrowserContextAsync(playwright, debugMessages);

            var page = await context.NewPageAsync();
            AddLookupDebug(debugMessages, $"Navigating Chromium to {uri}.");
            await page.GotoAsync(uri.ToString(), new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });
            AddLookupDebug(debugMessages, "Chromium reached DOMContentLoaded.");

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
                {
                    Timeout = 10000
                });
                AddLookupDebug(debugMessages, "Chromium reached NetworkIdle.");
            }
            catch (TimeoutException)
            {
                AddLookupDebug(debugMessages, "Chromium did not reach NetworkIdle within 10 seconds. Continuing with the loaded DOM.");
            }
            catch (PlaywrightException exception)
            {
                AddLookupDebug(debugMessages, $"NetworkIdle wait failed: {exception.Message}");
            }

            cancellationToken.ThrowIfCancellationRequested();

            AddLookupDebug(debugMessages, $"Browser current URL: {page.Url}");
            AddLookupDebug(debugMessages, $"Browser page title: {await page.TitleAsync()}");
            AddLookupDebug(debugMessages, "Reading page body text.");
            var bodyText = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 5000 });
            AddLookupDebug(debugMessages, $"Page body text length: {bodyText.Length:N0} characters.");
            AddLookupDebug(debugMessages, $"Page body preview: {PreviewText(bodyText)}");

            if (LooksLikeBlockedPage(bodyText, await page.TitleAsync(), page.Url))
            {
                AddLookupDebug(debugMessages, "The rendered page looks like a block, access denied, or verification page.");
                AddLookupDebug(debugMessages, "Trying one automatic browser refresh.");
                await page.ReloadAsync(new PageReloadOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 30000
                });
                await page.WaitForTimeoutAsync(2500);
                AddLookupDebug(debugMessages, $"Browser page title after refresh: {await page.TitleAsync()}");
                bodyText = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 5000 });
                AddLookupDebug(debugMessages, $"Page body preview after refresh: {PreviewText(bodyText)}");

                if (!LooksLikeBlockedPage(bodyText, await page.TitleAsync(), page.Url))
                {
                    AddLookupDebug(debugMessages, "Automatic refresh appears to have reached a non-error page.");
                }
                else
                {
                AddLookupDebug(debugMessages, "A visible Chromium window should be open. Waiting 60 seconds in case manual verification is needed.");
                await page.WaitForTimeoutAsync(60000);

                AddLookupDebug(debugMessages, "Rechecking page after the manual verification wait.");
                AddLookupDebug(debugMessages, $"Browser current URL after wait: {page.Url}");
                AddLookupDebug(debugMessages, $"Browser page title after wait: {await page.TitleAsync()}");
                bodyText = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 5000 });
                AddLookupDebug(debugMessages, $"Page body text length after wait: {bodyText.Length:N0} characters.");
                AddLookupDebug(debugMessages, $"Page body preview after wait: {PreviewText(bodyText)}");

                if (LooksLikeBlockedPage(bodyText, await page.TitleAsync(), page.Url))
                {
                    AddLookupDebug(debugMessages, "The page still looks blocked after the manual verification wait. Treating it as no product data.");
                    await context.CloseAsync();
                    return new ScrapedPartData();
                }
                }
            }

            AddLookupDebug(debugMessages, "Trying product selectors for title, price, manufacturer, description, SKU, and model.");
            var data = new ScrapedPartData
            {
                Title = await FirstTextAsync(page, "h1", "[data-testid='product-title']", ".product-details__title"),
                Price = await FirstTextAsync(page, "[data-testid='price-simple']", "[data-testid='price']", ".price-format__main-price"),
                Manufacturer = await FirstTextAsync(page, "[data-testid='attribute-brandname'] .attribute__value", "[data-testid='brand-name']", ".product-details__brand-name"),
                Description = FirstParagraph(await FirstTextAsync(page, "#product-overview p", "[data-testid='product-overview'] p", ".product-overview__description")),
                Sku = ExtractNumberAfterLabel(bodyText, "SKU"),
                ModelNumber = ExtractNumberAfterLabel(bodyText, "Model")
            }.Trimmed();

            if (!data.HasAnyValue)
            {
                AddLookupDebug(debugMessages, "Selector extraction found no fields. Reading rendered HTML and running HTML scraper.");
                data = ScrapePartPage(await page.ContentAsync());
            }

            if (LooksLikeInvalidProductData(data))
            {
                AddLookupDebug(debugMessages, "Extracted values look like an error page instead of product data. Treating it as no product data.");
                await context.CloseAsync();
                return new ScrapedPartData();
            }

            await context.CloseAsync();
            AddLookupDebug(debugMessages, $"Playwright extraction filled {CountFilledFields(data)} of 6 fields.");
            AddLookupDebug(debugMessages, $"Extracted values: {DescribeExtractedValues(data)}");
            return data;
        }
        catch (PlaywrightException exception)
        {
            AddLookupDebug(debugMessages, $"Playwright error: {exception.Message}");
            return new ScrapedPartData();
        }
        catch (TimeoutException exception)
        {
            AddLookupDebug(debugMessages, $"Playwright timeout: {exception.Message}");
            return new ScrapedPartData();
        }
    }

    private static void AddLookupDebug(AddPartViewModel model, string message)
    {
        AddLookupDebug(model.LookupDebugMessages, message);
    }

    private static void AddLookupDebug(ICollection<string> debugMessages, string message)
    {
        debugMessages.Add($"{DateTime.Now:HH:mm:ss} - {message}");
    }

    private static int CountFilledFields(ScrapedPartData data)
    {
        var values = new[]
        {
            data.Manufacturer,
            data.Title,
            data.Price,
            data.Description,
            data.Sku,
            data.ModelNumber
        };

        return values.Count(value => !string.IsNullOrWhiteSpace(value));
    }

    private static async Task<string> FirstTextAsync(IPage page, params string[] selectors)
    {
        foreach (var selector in selectors)
        {
            try
            {
                var locator = page.Locator(selector).First;
                if (await locator.CountAsync() == 0)
                {
                    continue;
                }

                var text = await locator.InnerTextAsync(new LocatorInnerTextOptions { Timeout = 1500 });
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
            catch (PlaywrightException)
            {
                continue;
            }
        }

        return string.Empty;
    }

    private static string ExtractNumberAfterLabel(string text, string label)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var match = Regex.Match(
            text,
            $"{Regex.Escape(label)}\\s*#?\\s*(?<value>[A-Za-z0-9_.-]+)",
            RegexOptions.IgnoreCase);

        return CleanText(match.Groups["value"].Value);
    }

    private static string GetPartSourceName(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Host.Contains("homedepot.com", StringComparison.OrdinalIgnoreCase))
        {
            return "Home Depot";
        }

        return "Manual";
    }

    private static decimal ParseCost(string price)
    {
        if (string.IsNullOrWhiteSpace(price))
        {
            return 0;
        }

        var match = Regex.Match(price, @"\d+(?:,\d{3})*(?:\.\d{1,2})?");
        if (!match.Success)
        {
            return 0;
        }

        return decimal.TryParse(
            match.Value.Replace(",", string.Empty),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var cost)
            ? cost
            : 0;
    }

    private static bool LooksLikeBlockedPage(string bodyText, string pageTitle, string pageUrl)
    {
        var combined = $"{bodyText} {pageTitle} {pageUrl}";
        var blockedPatterns = new[]
        {
            "access denied",
            "pardon our interruption",
            "verify you are human",
            "verify you're human",
            "captcha",
            "blocked",
            "error page",
            "oops!! something went wrong",
            "something went wrong",
            "refresh page",
            "request unsuccessful",
            "unusual traffic",
            "permission to access"
        };

        return blockedPatterns.Any(pattern => combined.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<IBrowserContext> LaunchPersistentBrowserContextAsync(IPlaywright playwright, ICollection<string> debugMessages)
    {
        var baseUserDataDir = GetPlaywrightUserDataDirectory();
        var browserAttempts = new (string Label, string? Channel)[]
        {
            ("Microsoft Edge", "msedge"),
            ("Google Chrome", "chrome"),
            ("bundled Chromium", null)
        };

        PlaywrightException? lastException = null;

        foreach (var browserAttempt in browserAttempts)
        {
            var userDataDir = Path.Combine(baseUserDataDir, browserAttempt.Channel ?? "chromium");
            Directory.CreateDirectory(userDataDir);
            AddLookupDebug(debugMessages, $"Trying visible {browserAttempt.Label} with persistent profile: {userDataDir}");

            try
            {
                return await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                {
                    Channel = browserAttempt.Channel,
                    Headless = false,
                    Locale = "en-US",
                    ViewportSize = new ViewportSize { Width = 1366, Height = 900 },
                    Args =
                    [
                        "--start-maximized"
                    ]
                });
            }
            catch (PlaywrightException exception)
            {
                lastException = exception;
                AddLookupDebug(debugMessages, $"{browserAttempt.Label} launch failed: {exception.Message}");
            }
        }

        throw lastException ?? new PlaywrightException("No Playwright browser could be launched.");
    }

    private static string GetPlaywrightUserDataDirectory()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userDataDir = Path.Combine(localApplicationData, "RenovatorApp", "Playwright", "HomeDepotProfile");
        Directory.CreateDirectory(userDataDir);
        return userDataDir;
    }

    private static bool LooksLikeInvalidProductData(ScrapedPartData data)
    {
        if (string.Equals(data.Title, "Error Page", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return CountFilledFields(data) == 1
            && !string.IsNullOrWhiteSpace(data.Title)
            && data.Title.Contains("error", StringComparison.OrdinalIgnoreCase);
    }

    private static string PreviewText(string value)
    {
        var clean = CleanText(value);
        if (clean.Length <= 500)
        {
            return string.IsNullOrWhiteSpace(clean) ? "(empty)" : clean;
        }

        return $"{clean[..500]}...";
    }

    private static string DescribeExtractedValues(ScrapedPartData data)
    {
        var values = new[]
        {
            $"Manufacturer='{PreviewText(data.Manufacturer)}'",
            $"Title='{PreviewText(data.Title)}'",
            $"Price='{PreviewText(data.Price)}'",
            $"Description='{PreviewText(data.Description)}'",
            $"Sku='{PreviewText(data.Sku)}'",
            $"Model='{PreviewText(data.ModelNumber)}'"
        };

        return string.Join("; ", values);
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
            ImageUrl = part.ImageUrl,
            IsPackage = part.IsPackage,
            PackageUnits = part.PackageUnits
        };
    }

    private string GetQuickBooksConfigValue(string name)
    {
        return HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()[$"QuickBooks:{name}"] ?? string.Empty;
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

    private sealed record PartsTransferFile(
        int Version,
        DateTime ExportedAtUtc,
        IReadOnlyList<PartsTransferPartSource> PartSources,
        IReadOnlyList<PartsTransferPart> Parts);

    private sealed record PartsTransferPartSource(
        Guid PartSourceId,
        string Name);

    private sealed record PartsTransferPart(
        Guid PartId,
        Guid PartSourceId,
        string Name,
        string Description,
        string ModelNumber,
        string Manufacturer,
        string Sku,
        string Url,
        string ImageUrl,
        decimal Cost,
        bool IsPackage,
        int PackageUnits);

    private sealed class PartsImportResult
    {
        public int PartSourcesCreated { get; set; }
        public int PartSourcesUpdated { get; set; }
        public int PartsCreated { get; set; }
        public int PartsUpdated { get; set; }
    }
}
