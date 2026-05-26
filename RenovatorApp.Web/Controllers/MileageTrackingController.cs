using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

public sealed class MileageTrackingController : Controller
{
    private readonly RenovatorAppDbContext _dbContext;

    public MileageTrackingController(RenovatorAppDbContext dbContext)
    {
        _dbContext = dbContext;
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
            .AsNoTracking()
            .Include(item => item.Waypoints)
            .FirstOrDefaultAsync(item => item.UniqueId == id, cancellationToken);

        if (trip is null)
        {
            return NotFound();
        }

        return View(new MileageTrackingTripViewModel
        {
            Trip = ToRowViewModel(trip),
            Waypoints = trip.Waypoints
                .OrderBy(waypoint => waypoint.WaypointTime)
                .Select(waypoint => new MileageTrackingWaypointViewModel
                {
                    WaypointTimeUtc = waypoint.WaypointTime,
                    GpsCoordinates = waypoint.GpsCoordinates
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
}
