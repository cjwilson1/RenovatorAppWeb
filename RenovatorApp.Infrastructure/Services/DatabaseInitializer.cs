using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RenovatorApp.Infrastructure.Data;

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
    }
}
