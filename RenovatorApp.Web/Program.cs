using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Services;
using RenovatorApp.Web.Services;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
Exception? databaseStartupException = null;

var port = builder.Configuration["PORT"];

if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

try
{
    var connectionString = GetDatabaseConnectionString(builder.Configuration);
    builder.Services.AddDbContext<RenovatorAppDbContext>(options => options.UseNpgsql(connectionString));
}
catch (Exception exception)
{
    databaseStartupException = exception;
}

builder.Services.AddScoped<InspectionDataService>();
builder.Services.AddScoped<MobileSyncDataService>();
builder.Services.AddScoped<DatabaseViewerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (databaseStartupException is null)
{
    try
    {
        await app.Services.InitializeDatabaseAsync();
    }
    catch (Exception exception)
    {
        databaseStartupException = exception;
    }
}

if (databaseStartupException is not null)
{
    var startupException = databaseStartupException;

    app.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(BuildDatabaseStartupErrorPage(startupException));
    });

    app.Run();
    return;
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

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

static string BuildDatabaseStartupErrorPage(Exception exception)
{
    var exceptionType = WebUtility.HtmlEncode(exception.GetType().Name);
    var exceptionMessage = WebUtility.HtmlEncode(exception.Message);

    return $$"""
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Database Startup Error - RenovatorApp</title>
            <style>
                body { font-family: Arial, sans-serif; margin: 0; color: #1f2933; background: #f4f6f8; }
                main { max-width: 880px; margin: 48px auto; padding: 0 24px; }
                section { background: white; border: 1px solid #d9e2ec; border-radius: 8px; padding: 28px; }
                h1 { margin: 0 0 12px; font-size: 28px; }
                h2 { margin-top: 28px; font-size: 18px; }
                p { line-height: 1.5; }
                code, pre { font-family: Consolas, "Courier New", monospace; }
                pre { overflow-x: auto; background: #102a43; color: #f0f4f8; padding: 16px; border-radius: 6px; }
                .error { border-left: 4px solid #d64545; padding: 12px 16px; background: #fff5f5; }
            </style>
        </head>
        <body>
            <main>
                <section>
                    <h1>RenovatorApp could not connect to the database</h1>
                    <p>The web server is running, but startup database initialization failed. Fix the PostgreSQL connection and refresh this page.</p>

                    <div class="error">
                        <strong>{{exceptionType}}</strong>
                        <p>{{exceptionMessage}}</p>
                    </div>

                    <h2>Local setup</h2>
                    <p>Update <code>RenovatorApp.Web/appsettings.json</code> or set a Rider environment variable with your local PostgreSQL credentials.</p>
                    <pre>ConnectionStrings__RenovatorApp=Host=localhost;Port=5432;Database=renovatorapp;Username=postgres;Password=YOUR_PASSWORD</pre>

                    <h2>Expected database</h2>
                    <p>The app expects a PostgreSQL database named <code>renovatorapp</code>. After the app can log in, EF Core migrations run automatically.</p>
                </section>
            </main>
        </body>
        </html>
        """;
}
