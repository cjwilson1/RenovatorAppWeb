using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;

namespace RenovatorApp.Infrastructure.Services;

public sealed class InspectionDataService
{
    private readonly RenovatorAppDbContext _dbContext;

    public InspectionDataService(RenovatorAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Inspection>> GetInspectionsAsync(Guid renoCompanyID, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Inspections
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Include(inspection => inspection.Property)
                .ThenInclude(property => property.Address)
            .Include(inspection => inspection.Customer)
            .OrderByDescending(inspection => inspection.InspectionDate)
            .ThenBy(inspection => inspection.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<Inspection?> GetInspectionDetailAsync(Guid renoCompanyID, Guid inspectionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Inspections
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Include(inspection => inspection.Customer)
                .ThenInclude(customer => customer!.BillAddress)
            .Include(inspection => inspection.Documents)
                .ThenInclude(document => document.DocumentType)
            .Include(inspection => inspection.Property)
                .ThenInclude(property => property.Address)
            .Include(inspection => inspection.Property)
                .ThenInclude(property => property.Buildings)
                    .ThenInclude(building => building.BuildingType)
            .Include(inspection => inspection.Property)
                .ThenInclude(property => property.Buildings)
                    .ThenInclude(building => building.Areas)
                        .ThenInclude(area => area.AreaType)
                            .ThenInclude(areaType => areaType!.Category)
            .Include(inspection => inspection.Property)
                .ThenInclude(property => property.Areas)
                    .ThenInclude(area => area.AreaType)
                        .ThenInclude(areaType => areaType!.Category)
            .Include(inspection => inspection.Property)
                .ThenInclude(property => property.Areas)
                    .ThenInclude(area => area.AreaNotes)
                        .ThenInclude(note => note.EstimateItems)
            .Include(inspection => inspection.Property)
                .ThenInclude(property => property.Areas)
                    .ThenInclude(area => area.AreaNotes)
                        .ThenInclude(note => note.Photos)
            .FirstOrDefaultAsync(inspection => inspection.InspectionId == inspectionId, cancellationToken);
    }

    public async Task<IReadOnlyList<Part>> GetPartsAsync(Guid renoCompanyID, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Parts
            .AsNoTracking()
            .ForCompany(renoCompanyID)
            .Include(part => part.PartSource)
            .OrderBy(part => part.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddDocumentAsync(Guid renoCompanyID, Document document, CancellationToken cancellationToken = default)
    {
        document.RenoCompanyID = renoCompanyID;
        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
