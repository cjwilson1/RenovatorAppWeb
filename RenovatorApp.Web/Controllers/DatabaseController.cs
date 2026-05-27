using Microsoft.AspNetCore.Mvc;
using RenovatorApp.Web.Services;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

public sealed class DatabaseController : Controller
{
    private const int DefaultPageSize = 20;
    private readonly DatabaseViewerService _databaseViewerService;

    public DatabaseController(DatabaseViewerService databaseViewerService)
    {
        _databaseViewerService = databaseViewerService;
    }

    public async Task<IActionResult> Index(string? tableName, int page = 1, int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var tableNames = _databaseViewerService.GetTableNames();

        if (string.IsNullOrWhiteSpace(tableName))
        {
            return View(new DatabaseTablePageViewModel
            {
                TableNames = tableNames,
                Page = 1,
                PageSize = DefaultPageSize,
                TotalPages = 1
            });
        }

        var model = await _databaseViewerService.GetTablePageAsync(tableName, page, pageSize, cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        await _databaseViewerService.ClearDatabaseAsync(cancellationToken);
        TempData["DatabaseMessage"] = "Database data was cleared.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearMileageTracking(CancellationToken cancellationToken)
    {
        var result = await _databaseViewerService.ClearMileageTrackingAsync(cancellationToken);
        TempData["DatabaseMessage"] = $"Mileage tracking was cleared. Deleted {result.SessionsDeleted:N0} session row(s) and {result.WaypointsDeleted:N0} waypoint row(s).";

        return RedirectToAction(nameof(Index));
    }
}
