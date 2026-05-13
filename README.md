# RenovatorApp

ASP.NET Core MVC web app with API endpoints, EF Core, and PostgreSQL for RenovatorApp.

## Projects

- `RenovatorApp.Web`: MVC website plus API controllers.
- `RenovatorApp.Infrastructure`: EF Core models, DbContext, and data services.
- `RenovatorApp.Jobs`: command runner for admin/background jobs.

## Local database

Set `ConnectionStrings:RenovatorApp` in `RenovatorApp.Web/appsettings.json` or provide `DATABASE_URL` in the environment.

## Railway deployment

Railway deploys this app from the root `Dockerfile`.

1. Create a new Railway project.
2. Add a PostgreSQL database service to the project.
3. Add a GitHub service for this repository.
4. In the web service variables, set:

```text
DATABASE_URL=${{ Postgres.DATABASE_URL }}
ASPNETCORE_ENVIRONMENT=Production
```

If your PostgreSQL service has a different Railway service name, use that name instead of `Postgres` in the reference variable.

After deployment, open the web service settings and generate a public domain under Networking.

## Build

```powershell
dotnet build RenovatorApp.slnx
```
