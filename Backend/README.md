# StaySphere Backend

ASP.NET Core Web API + EF Core + SQLite. See `../Docs` for architecture, API
contracts, schema and design decisions.

## Prerequisites

* .NET SDK 10
* EF Core tools (only needed to create/inspect migrations):
  `dotnet tool install --global dotnet-ef --version 10.*`

## Run

```bash
cd Backend
dotnet run --project StaySphere.Api
```

On startup the API applies migrations, enables WAL mode and seeds sample data,
then listens on the URL from `StaySphere.Api/Properties/launchSettings.json`
(dev default `http://localhost:5276`). Swagger UI: `/swagger`.

The SQLite file `staysphere.db` is created in the API working directory and is
git-ignored. Delete it to start from a clean seed.

## Build & test

```bash
dotnet build StaySphere.slnx
dotnet test  StaySphere.slnx
```

## Migrations

```bash
dotnet ef migrations add <Name> \
  --project StaySphere.Infrastructure \
  --startup-project StaySphere.Infrastructure \
  --output-dir Persistence/Migrations
```

A design-time factory (`StaySphereDbContextFactory`) supplies the context, so the
API host does not need to start.

## Configuration

| Setting | Key | Default |
|---------|-----|---------|
| Database | `ConnectionStrings:StaySphere` / `ConnectionStrings__StaySphere` | `Data Source=staysphere.db` |
| CORS origins | `Cors:AllowedOrigins` | `["http://localhost:3000"]` |
| Room seed files | `Seeding:RoomsFiles` | `["Data/room-seed.json"]` |

### Adding rooms via JSON

Edit `StaySphere.Api/Data/room-seed.json` (or add another file and list it in
`Seeding:RoomsFiles`). On the next start, `JsonRoomCatalogSeeder` inserts any
`amenities` / `roomTypes` / `rooms` whose explicit `id` is not already in the
database; existing rows are left untouched, so it is safe to run every time.
See `../Docs/database.md` for the file format.

## Projects

| Project | Role |
|---------|------|
| `StaySphere.Api` | HTTP endpoints, middleware, DI composition |
| `StaySphere.Application` | use cases, DTOs, validation, availability query |
| `StaySphere.Domain` | entities, `DateRange`, invariants (no framework deps) |
| `StaySphere.Infrastructure` | `DbContext`, configs, migrations, seeding, adapters |
| `StaySphere.Tests` | xUnit (smoke only in Stage 1) |
