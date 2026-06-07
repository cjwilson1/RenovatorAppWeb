using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;

namespace RenovatorApp.Infrastructure.Services;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RenovatorAppDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Part\" ADD COLUMN IF NOT EXISTS \"ImageUrl\" text NOT NULL DEFAULT '';",
            cancellationToken);
        await SeedDocumentTypesAsync(dbContext, cancellationToken);
    }

    private static async Task SeedDocumentTypesAsync(RenovatorAppDbContext dbContext, CancellationToken cancellationToken)
    {
        await UpsertDocumentTypeAsync(dbContext, DocumentType.InspectionId, "Inspection", cancellationToken);
        await UpsertDocumentTypeAsync(dbContext, DocumentType.CustomerDataSheetId, "Customer DataSheet", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertDocumentTypeAsync(
        RenovatorAppDbContext dbContext,
        Guid documentTypeId,
        string name,
        CancellationToken cancellationToken)
    {
        var documentType = await dbContext.DocumentTypes
            .FirstOrDefaultAsync(item => item.DocumentTypeId == documentTypeId, cancellationToken);

        if (documentType is null)
        {
            dbContext.DocumentTypes.Add(new DocumentType
            {
                DocumentTypeId = documentTypeId,
                Name = name
            });
            return;
        }

        documentType.Name = name;
    }
}
