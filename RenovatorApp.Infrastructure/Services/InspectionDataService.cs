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

    public async Task<IReadOnlyList<Inspection>> GetInspectionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Inspections
            .AsNoTracking()
            .Include(inspection => inspection.Property)
                .ThenInclude(property => property.Address)
            .Include(inspection => inspection.Client)
            .OrderByDescending(inspection => inspection.InspectionDate)
            .ThenBy(inspection => inspection.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<Inspection?> GetInspectionDetailAsync(Guid inspectionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Inspections
            .AsNoTracking()
            .Include(inspection => inspection.Client)
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
            .FirstOrDefaultAsync(inspection => inspection.Id == inspectionId, cancellationToken);
    }

    public async Task<IReadOnlyList<Inspector>> GetInspectorsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Inspectors
            .AsNoTracking()
            .OrderBy(inspector => inspector.LastName)
            .ThenBy(inspector => inspector.FirstName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Part>> GetPartsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Parts
            .AsNoTracking()
            .Include(part => part.PartSource)
            .OrderBy(part => part.Name)
            .ToListAsync(cancellationToken);
    }
}
