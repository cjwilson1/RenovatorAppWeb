using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Services;

var builder = Host.CreateApplicationBuilder(args);
var connectionString = GetDatabaseConnectionString(builder.Configuration);

builder.Services.AddDbContext<RenovatorAppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<InspectionDataService>();

using var host = builder.Build();

if (args.Contains("migrate", StringComparer.OrdinalIgnoreCase))
{
    await host.Services.InitializeDatabaseAsync();
    Console.WriteLine("Database migration complete.");
    return;
}

Console.WriteLine("RenovatorApp.Jobs commands:");
Console.WriteLine("  migrate    Apply EF Core migrations");

static string GetDatabaseConnectionString(IConfiguration configuration)
{
    var databaseUrl = configuration["DATABASE_URL"]
        ?? configuration["POSTGRES_URL"]
        ?? configuration["POSTGRESQL_URL"];

    if (!string.IsNullOrWhiteSpace(databaseUrl) && Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri))
    {
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? string.Empty);
        var password = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? string.Empty);
        var database = uri.AbsolutePath.TrimStart('/');

        return $"Host={uri.Host};Port={uri.Port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
    }

    var connectionString = configuration.GetConnectionString("RenovatorApp")
        ?? configuration["POSTGRESQLCONNSTR_RENOVATORAPP"]
        ?? configuration["POSTGRESQLCONNSTR_RenovatorApp"]
        ?? configuration["CONNECTION_STRING"]
        ?? configuration["DATABASE_CONNECTION_STRING"]
        ?? configuration["POSTGRES_CONNECTION_STRING"]
        ?? configuration["POSTGRESQL_CONNECTION_STRING"]
        ?? configuration["ConnectionStrings__RenovatorApp"];

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        return connectionString;
    }

    var pgHost = configuration["PGHOST"];
    var pgDatabase = configuration["PGDATABASE"];
    var pgUser = configuration["PGUSER"];
    var pgPassword = configuration["PGPASSWORD"];

    if (!string.IsNullOrWhiteSpace(pgHost)
        && !string.IsNullOrWhiteSpace(pgDatabase)
        && !string.IsNullOrWhiteSpace(pgUser)
        && !string.IsNullOrWhiteSpace(pgPassword))
    {
        var pgPort = configuration["PGPORT"] ?? "5432";
        var sslMode = configuration["PGSSLMODE"] ?? "Require";

        return $"Host={pgHost};Port={pgPort};Database={pgDatabase};Username={pgUser};Password={pgPassword};SSL Mode={sslMode};Trust Server Certificate=true";
    }

    var presentSettings = GetPresentConnectionSettingNames(configuration);

    throw new InvalidOperationException(
        $"Configure a PostgreSQL connection using one of: DATABASE_URL, POSTGRES_URL, POSTGRESQL_URL, ConnectionStrings__RenovatorApp, CONNECTION_STRING, DATABASE_CONNECTION_STRING, POSTGRES_CONNECTION_STRING, POSTGRESQL_CONNECTION_STRING, POSTGRESQLCONNSTR_RenovatorApp, or PGHOST/PGDATABASE/PGUSER/PGPASSWORD. Present recognized settings: {presentSettings}.");
}

static string GetPresentConnectionSettingNames(IConfiguration configuration)
{
    var knownSettings = new[]
    {
        "DATABASE_URL",
        "POSTGRES_URL",
        "POSTGRESQL_URL",
        "ConnectionStrings:RenovatorApp",
        "ConnectionStrings__RenovatorApp",
        "CONNECTION_STRING",
        "DATABASE_CONNECTION_STRING",
        "POSTGRES_CONNECTION_STRING",
        "POSTGRESQL_CONNECTION_STRING",
        "POSTGRESQLCONNSTR_RENOVATORAPP",
        "POSTGRESQLCONNSTR_RenovatorApp",
        "PGHOST",
        "PGDATABASE",
        "PGUSER",
        "PGPASSWORD",
        "PGPORT",
        "PGSSLMODE"
    };

    var presentSettings = knownSettings
        .Where(name => !string.IsNullOrWhiteSpace(configuration[name]))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return presentSettings.Length == 0 ? "none" : string.Join(", ", presentSettings);
}
