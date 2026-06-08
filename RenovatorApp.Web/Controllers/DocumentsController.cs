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
            Page = page,
            PageSize = PageSize,
            TotalDocuments = totalDocuments,
            TotalPages = totalPages,
            Search = normalizedSearch
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id, string documentName, string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .ForCompany(_currentUserSession.RenoCompanyID)
            .FirstOrDefaultAsync(item => item.DocumentId == id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        document.DocumentName = Clean(documentName);
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

    private static string GetCustomerName(Customer? customer)
    {
        if (customer is null)
        {
            return string.Empty;
        }

        return string.Join(" ", new[] { customer.GivenName, customer.FamilyName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string Clean(string? value) => value?.Trim() ?? string.Empty;
}
