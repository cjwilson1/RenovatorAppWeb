using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;
using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Controllers;

public sealed class DocumentsController : Controller
{
    private const int PageSize = 10;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private readonly RenovatorAppDbContext _dbContext;

    public DocumentsController(RenovatorAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        var normalizedSearch = (search ?? string.Empty).Trim();
        var query = _dbContext.Documents
            .AsNoTracking()
            .Include(document => document.Customer)
            .AsQueryable();

        if (normalizedSearch.Length >= 2)
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(document =>
                EF.Functions.ILike(document.DocumentName, pattern)
                || EF.Functions.ILike(document.DocumentType, pattern)
                || EF.Functions.ILike(document.Filename, pattern)
                || (document.Customer != null
                    && (EF.Functions.ILike(document.Customer.GivenName, pattern)
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

    public async Task<IActionResult> Open(Guid id, CancellationToken cancellationToken)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.DocumentId == id, cancellationToken);

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
            .FirstOrDefaultAsync(item => item.DocumentId == id, cancellationToken);

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
            CustomerName = GetCustomerName(document.Customer),
            DocumentType = document.DocumentType,
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
}
