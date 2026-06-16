# RenovatorApp

ASP.NET Core MVC web app with API endpoints, EF Core, and PostgreSQL for RenovatorApp.

## Projects

- `RenovatorApp.Web`: MVC website plus API controllers.
- `RenovatorApp.Infrastructure`: EF Core models, DbContext, and data services.
- `RenovatorApp.Jobs`: command runner for admin/background jobs.

## Local database

Set `ConnectionStrings:RenovatorApp` in `RenovatorApp.Web/appsettings.json` or provide `DATABASE_URL` in the environment.

## Railway deployment

Railway deploys this app from the root `Dockerfile`. The root `railway.json` forces Railway to use the Dockerfile builder instead of Railpack.

1. Create a new Railway project.
2. Add a PostgreSQL database service to the project.
3. Add a GitHub service for this repository.
4. In the web service variables, set:

```text
DATABASE_URL=${{ Postgres.DATABASE_URL }}
ASPNETCORE_ENVIRONMENT=Production
```

If your PostgreSQL service has a different Railway service name, use that name instead of `Postgres` in the reference variable.

To require the mobile app to authenticate when syncing, also set:

```text
MOBILE_SYNC_API_KEY=your-long-random-secret
```

When this value is present, mobile sync requests must include an `X-Api-Key` header with the same value.

After deployment, open the web service settings and generate a public domain under Networking.

If Railway tries to use Railpack or reports `Script start.sh not found`, confirm the web service has:

- Root Directory: blank, or the repository root.
- Builder: Dockerfile.
- Dockerfile Path: `Dockerfile`.
- Start Command: empty, so Railway uses the Dockerfile `ENTRYPOINT`.

## Build

```powershell
dotnet build RenovatorApp.slnx
```

## Mobile sync API

The mobile app should sync through the web API, not by connecting directly to PostgreSQL.

```http
POST /api/sync
X-Api-Key: your-long-random-secret
Content-Type: application/json
```

The sync endpoint accepts graph data in dependency order: lookup tables, inspectors, customers, properties, addresses, buildings, inspections, areas, notes, estimate items, and photos. All arrays are optional so the phone can send only the rows that changed.

```json
{
  "deviceId": "phone-123",
  "lastSyncedAtUtc": "2026-05-13T20:00:00Z",
  "inspectors": [],
  "customers": [],
  "properties": [],
  "addresses": [],
  "buildings": [],
  "inspections": [],
  "inspectionAreas": [],
  "inspectionAreaNotes": [],
  "inspectionAreaNoteEstimateItems": [],
  "inspectionAreaNotePhotos": []
}
```

Rows are upserted by their GUID primary keys. For entities with `UpdatedAtUtc`, the server keeps its row when it is newer than the incoming mobile row and returns a `conflict` result for that item.

The response includes `serverChanges`, a current server snapshot for the company. The mobile app should upsert these rows locally and, during login or full sync, remove local rows that are no longer present in the returned snapshot. This is how web-side edits and hard deletes are synced back to the mobile app until server-side tombstone tables exist.
