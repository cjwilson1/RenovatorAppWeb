using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.ViewModels;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RenovatorApp.Web.Controllers;

public sealed class MileageTrackingController : Controller
{
    private readonly RenovatorAppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public MileageTrackingController(
        RenovatorAppDbContext dbContext,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var trips = await _dbContext.MileageTracking
            .AsNoTracking()
            .OrderByDescending(trip => trip.TrackingStartedAtUtc)
            .Select(trip => ToRowViewModel(trip))
            .ToListAsync(cancellationToken);

        return View(new MileageTrackingIndexViewModel
        {
            Trips = trips
        });
    }

    public async Task<IActionResult> Trip(Guid id, CancellationToken cancellationToken)
    {
        var trip = await _dbContext.MileageTracking
            .Include(item => item.Waypoints)
            .FirstOrDefaultAsync(item => item.UniqueId == id, cancellationToken);

        if (trip is null)
        {
            return NotFound();
        }

        await PopulateMissingWaypointLocationsAsync(trip.Waypoints, cancellationToken);

        return View(new MileageTrackingTripViewModel
        {
            Trip = ToRowViewModel(trip),
            MapImageUrl = BuildStaticMapImageUrl(trip.Waypoints),
            Waypoints = trip.Waypoints
                .OrderBy(waypoint => waypoint.WaypointTime)
                .Select(waypoint => new MileageTrackingWaypointViewModel
                {
                    WaypointTimeUtc = waypoint.WaypointTime,
                    CumulativeMiles = waypoint.CumulativeMiles,
                    GpsCoordinates = waypoint.GpsCoordinates,
                    Location = waypoint.Location ?? string.Empty
                })
                .ToList()
        });
    }

    private static MileageTrackingRowViewModel ToRowViewModel(MileageTracking trip)
    {
        return new MileageTrackingRowViewModel
        {
            UniqueId = trip.UniqueId,
            StartTimeUtc = trip.TrackingStartedAtUtc,
            ElapsedTime = trip.TotalTime,
            TotalMileage = trip.TotalMileage,
            EndTimeUtc = trip.TrackingStartedAtUtc.Add(trip.TotalTime)
        };
    }

    private string BuildStaticMapImageUrl(IEnumerable<MileageTrackingWaypoint> waypoints)
    {
        var apiKey = GetGeoapifyApiKey();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return string.Empty;
        }

        var mapWaypoints = waypoints
            .OrderBy(waypoint => waypoint.WaypointTime)
            .Select(waypoint => new MapWaypoint(
                TryParseGpsCoordinates(waypoint.GpsCoordinates),
                waypoint.WaypointTime.ToLocalTime().ToString("h:mm", CultureInfo.CurrentCulture)))
            .Where(waypoint => waypoint.Coordinate.HasValue)
            .Select(waypoint => waypoint with { Coordinate = waypoint.Coordinate!.Value })
            .ToList();

        if (mapWaypoints.Count == 0)
        {
            return string.Empty;
        }

        var query = new List<string>
        {
            "style=osm-bright",
            "width=900",
            "height=360",
            "scaleFactor=2",
            "format=png"
        };

        var markers = mapWaypoints
            .Select((waypoint, index) =>
            {
                var coordinate = waypoint.Coordinate!.Value;
                var pinColor = index == 0 ? "%23198754" : "%23d92d20";

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "lonlat:{0:0.##############},{1:0.##############};type:material;color:{2};size:24;text:{3};contentsize:14;contentcolor:%23ffffff;whitecircle:no",
                    coordinate.Longitude,
                    coordinate.Latitude,
                    pinColor,
                    index + 1);
            });

        query.Add($"marker={string.Join('|', markers)}");

        if (mapWaypoints.Count > 1)
        {
            var lineCoordinates = string.Join(
                ',',
                mapWaypoints.Select(waypoint =>
                {
                    var coordinate = waypoint.Coordinate!.Value;

                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:0.##############},{1:0.##############}",
                        coordinate.Longitude,
                        coordinate.Latitude);
                }));

            query.Add($"geometry={Uri.EscapeDataString($"polyline:{lineCoordinates};linecolor:#0d6efd;linewidth:4")}");
        }

        query.Add($"apiKey={Uri.EscapeDataString(apiKey)}");

        return $"https://maps.geoapify.com/v1/staticmap?{string.Join('&', query)}";
    }

    private async Task PopulateMissingWaypointLocationsAsync(
        IEnumerable<MileageTrackingWaypoint> waypoints,
        CancellationToken cancellationToken)
    {
        var apiKey = GetGeoapifyApiKey();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var httpClient = _httpClientFactory.CreateClient("Browser");
        var updated = false;

        foreach (var waypoint in waypoints.Where(waypoint => string.IsNullOrWhiteSpace(waypoint.Location)))
        {
            var coordinate = TryParseGpsCoordinates(waypoint.GpsCoordinates);

            if (!coordinate.HasValue)
            {
                continue;
            }

            var location = await ReverseGeocodeAsync(httpClient, coordinate.Value, apiKey, cancellationToken);

            if (string.IsNullOrWhiteSpace(location))
            {
                continue;
            }

            waypoint.Location = location;
            updated = true;
        }

        if (updated)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<string> ReverseGeocodeAsync(
        HttpClient httpClient,
        MapCoordinate coordinate,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var url = string.Format(
            CultureInfo.InvariantCulture,
            "https://api.geoapify.com/v1/geocode/reverse?lat={0:0.##############}&lon={1:0.##############}&format=json&apiKey={2}",
            coordinate.Latitude,
            coordinate.Longitude,
            Uri.EscapeDataString(apiKey));

        using var response = await httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array
            || results.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var properties = results[0];

        if (properties.TryGetProperty("formatted", out var formatted)
            && formatted.ValueKind == JsonValueKind.String)
        {
            return formatted.GetString()?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    private string GetGeoapifyApiKey()
    {
        return _configuration["Geoapify:ApiKey"]
            ?? _configuration["GEOAPIFY_API_KEY"]
            ?? string.Empty;
    }

    private static MapCoordinate? TryParseGpsCoordinates(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Regex.Match(
            value,
            @"(?<latitude>-?\d+(?:\.\d+)?)\s*,\s*(?<longitude>-?\d+(?:\.\d+)?)",
            RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return null;
        }

        if (!double.TryParse(match.Groups["latitude"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(match.Groups["longitude"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
            || latitude is < -90 or > 90
            || longitude is < -180 or > 180)
        {
            return null;
        }

        return new MapCoordinate(latitude, longitude);
    }

    private readonly record struct MapCoordinate(double Latitude, double Longitude);

    private readonly record struct MapWaypoint(MapCoordinate? Coordinate, string Label);
}
