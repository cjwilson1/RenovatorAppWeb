using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.Services;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

public sealed class InspectorsController : Controller
{
    private const int PageSize = 10;
    private readonly RenovatorAppDbContext _dbContext;
    private readonly CurrentUserSession _currentUserSession;

    public InspectorsController(RenovatorAppDbContext dbContext, CurrentUserSession currentUserSession)
    {
        _dbContext = dbContext;
        _currentUserSession = currentUserSession;
    }

    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var renoCompanyID = _currentUserSession.RenoCompanyID;
        var query = _dbContext.Inspectors
            .AsNoTracking()
            .Where(inspector => inspector.RenoCompanyID == renoCompanyID);

        var totalInspectors = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalInspectors / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var inspectors = await query
            .OrderByDescending(inspector => inspector.IsDefault)
            .ThenBy(inspector => inspector.FirstName)
            .ThenBy(inspector => inspector.LastName)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(inspector => new InspectorRowViewModel
            {
                Id = inspector.InspectorId,
                FirstName = inspector.FirstName,
                LastName = inspector.LastName,
                HourlyRate = inspector.HourlyRate,
                Phone = inspector.Phone,
                Email = inspector.Email,
                IsDefault = inspector.IsDefault
            })
            .ToListAsync(cancellationToken);

        return View(new InspectorsIndexViewModel
        {
            Inspectors = inspectors,
            Page = page,
            TotalPages = totalPages,
            TotalInspectors = totalInspectors
        });
    }

    [HttpGet]
    public IActionResult Add()
    {
        return View("Edit", new InspectorEditViewModel());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var renoCompanyID = _currentUserSession.RenoCompanyID;
        var inspector = await _dbContext.Inspectors
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.InspectorId == id && item.RenoCompanyID == renoCompanyID, cancellationToken);

        if (inspector is null)
        {
            return NotFound();
        }

        return View(new InspectorEditViewModel
        {
            Id = inspector.InspectorId,
            FirstName = inspector.FirstName,
            LastName = inspector.LastName,
            HourlyRate = inspector.HourlyRate,
            Phone = inspector.Phone,
            Email = inspector.Email,
            IsDefault = inspector.IsDefault
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(InspectorEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Edit", model);
        }

        var renoCompanyID = _currentUserSession.RenoCompanyID;
        Inspector inspector;

        if (model.Id is Guid id)
        {
            inspector = await _dbContext.Inspectors
                .FirstOrDefaultAsync(item => item.InspectorId == id && item.RenoCompanyID == renoCompanyID, cancellationToken)
                ?? throw new InvalidOperationException("Inspector was not found.");
        }
        else
        {
            inspector = new Inspector
            {
                InspectorId = Guid.NewGuid(),
                RenoCompanyID = renoCompanyID
            };
            _dbContext.Inspectors.Add(inspector);
        }

        inspector.FirstName = Clean(model.FirstName);
        inspector.LastName = Clean(model.LastName);
        inspector.HourlyRate = model.HourlyRate;
        inspector.Phone = Clean(model.Phone);
        inspector.Email = Clean(model.Email);
        inspector.IsDefault = model.IsDefault;

        if (inspector.IsDefault)
        {
            await _dbContext.Inspectors
                .Where(item => item.RenoCompanyID == renoCompanyID && item.InspectorId != inspector.InspectorId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsDefault, false), cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private static string Clean(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }
}
