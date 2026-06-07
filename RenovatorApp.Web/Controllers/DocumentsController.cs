using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.Services;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

public sealed class DocumentsController : Controller
{
    private const int PageSize = 10;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private readonly RenovatorAppDbContext _dbContext;
    private readonly CurrentUserSession _currentUserSession;

    public DocumentsController(RenovatorAppDbContext dbContext, CurrentUserSession currentUserSession)
    {
        _dbContext = dbContext;
        _currentUserSession = currentUserSession;
    }

    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = (search ?? string.Empty).Trim();
        var query = _dbContext.Documents
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .Include(document => document.Customer)
            .Include(document => document.DocumentType)
            .Include(document => document.Inspection)
            .AsQueryable();

        if (normalizedSearch.Length >= 2)
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(document =>
                EF.Functions.ILike(document.DocumentName, pattern)
                || (document.DocumentType != null && EF.Functions.ILike(document.DocumentType.Name, pattern))
                || EF.Functions.ILike(document.Filename, pattern)
                || (document.Inspection != null
                    && EF.Functions.ILike(document.Inspection.Title, pattern))
                || (document.Customer != null
                    && (EF.Functions.ILike(document.Customer.GivenName, pattern)
                        || EF.Functions.ILike(document.Customer.DisplayName, pattern)
                        || EF.Functions.ILike(document.Customer.CompanyName, pattern)
                        || EF.Functions.ILike(document.Customer.FamilyName, pattern))));
        }

        var totalDocuments = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalDocuments / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var documents = await query
            .OrderByDescending(document => document.CreateDate)
            .ThenBy(document => document.DocumentName)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(document => ToRowViewModel(document))
            .ToListAsync(cancellationToken);

        return View(new DocumentsIndexViewModel
        {
            Documents = documents,
            Customers = await GetCustomerAssignmentOptionsAsync(cancellationToken),
            Inspections = await GetInspectionAssignmentOptionsAsync(cancellationToken),
            Page = page,
            PageSize = PageSize,
            TotalDocuments = totalDocuments,
            TotalPages = totalPages,
            Search = normalizedSearch
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(Guid id, Guid? customerId, Guid? inspectionId, string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var renoCompanyId = _currentUserSession.RenoCompanyID;
        var document = await _dbContext.Documents
            .ForCompany(renoCompanyId)
            .FirstOrDefaultAsync(item => item.DocumentId == id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        if (customerId.HasValue)
        {
            var customerExists = await _dbContext.Customers
                .AsNoTracking()
                .ForCompany(renoCompanyId)
                .AnyAsync(customer => customer.CustomerId == customerId.Value, cancellationToken);

            if (!customerExists)
            {
                return NotFound();
            }
        }

        if (inspectionId.HasValue)
        {
            var inspectionExists = await _dbContext.Inspections
                .AsNoTracking()
                .ForCompany(renoCompanyId)
                .AnyAsync(inspection => inspection.InspectionId == inspectionId.Value, cancellationToken);

            if (!inspectionExists)
            {
                return NotFound();
            }
        }

        document.CustomerId = customerId;
        document.InspectionId = inspectionId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index), new { search, page });
    }

    public async Task<IActionResult> Open(Guid id, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.DocumentId == id && item.RenoCompanyID == _currentUserSession.RenoCompanyID, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        var documentPath = Path.GetFullPath(document.Path);
        if (!System.IO.File.Exists(documentPath))
        {
            return NotFound();
        }

        if (!ContentTypeProvider.TryGetContentType(document.Filename, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return PhysicalFile(documentPath, contentType);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(item => item.DocumentId == id && item.RenoCompanyID == _currentUserSession.RenoCompanyID, cancellationToken);

        if (document is not null)
        {
            _dbContext.Documents.Remove(document);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return RedirectToAction(nameof(Index), new { search, page });
    }

    private static DocumentRowViewModel ToRowViewModel(Document document)
    {
        return new DocumentRowViewModel
        {
            DocumentId = document.DocumentId,
            DocumentName = document.DocumentName,
            CustomerId = document.CustomerId,
            CustomerName = GetCustomerName(document.Customer),
            InspectionId = document.InspectionId,
            InspectionTitle = document.Inspection?.Title ?? string.Empty,
            DocumentType = document.DocumentType?.Name ?? string.Empty,
            Filename = document.Filename,
            CreateDate = document.CreateDate
        };
    }

    private async Task<IReadOnlyList<DocumentAssignmentOptionViewModel>> GetCustomerAssignmentOptionsAsync(CancellationToken cancellationToken)
    {
        var customers = await _dbContext.Customers
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .OrderBy(customer => customer.DisplayName)
            .ThenBy(customer => customer.FamilyName)
            .ToListAsync(cancellationToken);

        return customers
            .Select(customer => new DocumentAssignmentOptionViewModel
            {
                Id = customer.CustomerId,
                Name = string.IsNullOrWhiteSpace(customer.DisplayName)
                    ? GetCustomerName(customer)
                    : customer.DisplayName
            })
            .ToList();
    }

    private async Task<IReadOnlyList<DocumentAssignmentOptionViewModel>> GetInspectionAssignmentOptionsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Inspections
            .AsNoTracking()
            .ForCompany(_currentUserSession.RenoCompanyID)
            .OrderByDescending(inspection => inspection.InspectionDate)
            .ThenBy(inspection => inspection.Title)
            .Select(inspection => new DocumentAssignmentOptionViewModel
            {
                Id = inspection.InspectionId,
                Name = inspection.Title
            })
            .ToListAsync(cancellationToken);
    }

    private static string GetCustomerName(Customer? customer)
    {
        if (customer is null)
        {
            return string.Empty;
        }

        return string.Join(" ", new[] { customer.GivenName, customer.FamilyName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
