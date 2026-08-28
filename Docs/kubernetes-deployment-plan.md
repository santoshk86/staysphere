# StaySphere Backend — Local Kubernetes Deployment Assessment

**Status:** assessment only. No deployment artifacts, Dockerfiles, manifests, or
source changes have been made. This document defines what is required to run the
existing `StaySphere.Api` (ASP.NET Core 10 + EF Core 10 + SQLite) in:

```
Docker image → local Kubernetes (kind / minikube / Docker Desktop / k3d)
            → ASP.NET Core API → persistent SQLite database file on a volume
```

---

## 1. Current findings (repository inspection)

### 1.1 .NET project structure

| Project | Role | Notable deps |
|---|---|---|
| `Backend/StaySphere.Api` | Composition root, controllers, middleware, Swagger, startup DB init | `Swashbuckle.AspNetCore` 10.2.3 |
| `Backend/StaySphere.Application` | Use cases, DTOs, validation, availability query | `Microsoft.EntityFrameworkCore` (abstraction only) |
| `Backend/StaySphere.Domain` | Entities, `DateRange`, invariants | BCL only |
| `Backend/StaySphere.Infrastructure` | `DbContext`, configs, migrations, seeding | `Microsoft.EntityFrameworkCore.Sqlite` 10.0.11, `...Design` 10.0.11 (`PrivateAssets=all`) |
| `Backend/StaySphere.Tests` | xUnit (not deployed) | — |

* Solution file: `Backend/StaySphere.slnx` (new XML solution format).
* No `Directory.Build.props` / `.targets` / `.editorconfig`.
* Dependency direction: `Api → Application → Domain`, `Api/Infrastructure → Application/Domain`. Clean; nothing in Domain/Application blocks containerisation.

### 1.2 Target framework / version

* **`net10.0`** for every project (`<TargetFramework>net10.0</TargetFramework>`).
* Requires **.NET SDK 10** to build and the **ASP.NET Core 10 runtime** to run.
* Container base images needed: `mcr.microsoft.com/dotnet/sdk:10.0` (build) and
  `mcr.microsoft.com/dotnet/aspnet:10.0` (runtime).

### 1.3 `Program.cs` (`Backend/StaySphere.Api/Program.cs`)

Startup sequence:

1. `WebApplication.CreateBuilder(args)` — default host config (env vars, `appsettings*.json` from content root = working directory, command line).
2. `AddApplication()` + `AddInfrastructure(builder.Configuration)`.
3. `AddControllers()` + custom `InvalidModelStateResponseFactory` (model-binding errors mapped to the `ApiErrorResponse` envelope).
4. `AddEndpointsApiExplorer()` + `AddSwaggerGen(...)` — **always registered**.
5. CORS: policy `"frontend"` built from `Cors:AllowedOrigins` (string array). If the array is empty the policy is registered **with no origins** (effectively no cross-origin access).
6. `UseMiddleware<ExceptionHandlingMiddleware>()`.
7. **Only when `app.Environment.IsDevelopment()`**: `UseSwagger()`, `UseSwaggerUI()`, and `GET /` → `Redirect("/swagger")`.
8. `UseCors("frontend")`, `MapControllers()`.
9. **On every startup**: resolves `DatabaseInitializer` from a scope and `await initializer.InitializeAsync()`.
10. `app.Run()`.

Key implications:
* **No `UseHttpsRedirection()`** — the app is HTTP-only by default. Good for in-cluster; TLS is expected to terminate at an ingress/proxy.
* **No `MapHealthChecks` / `AddHealthChecks`** — there is **no health endpoint**. (The `Microsoft.*.HealthChecks` strings under `obj/`/`bin/` are transitive framework metadata, not usage.)
* In a **non-Development** environment there is **no Swagger and no `GET /` route** — `/` returns 404. Do not use `/` as a probe target in Production.
* `Program` is `public partial` (integration-test hook) — irrelevant to deployment but harmless.

### 1.4 `appsettings.json`

```jsonc
{
  "ConnectionStrings": { "StaySphere": "Data Source=staysphere.db" },
  "Cors":   { "AllowedOrigins": [ "http://localhost:3000" ] },
  "Seeding":{ "RoomsFiles": [ "Data/room-seed.json" ] },
  "Logging":{ "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning", "Microsoft.EntityFrameworkCore.Database.Command": "Warning" } },
  "AllowedHosts": "*"
}
```

* Connection string is a **relative** SQLite path: `staysphere.db`.
* CORS default allows only `http://localhost:3000`.
* `AllowedHosts: "*"` — no host filtering, fine for a cluster behind an ingress.

### 1.5 `appsettings.Development.json`

Only raises log levels (`Microsoft.AspNetCore` and EF command logging to `Information`). No connection string, no ports, no CORS. There is **no `appsettings.Production.json`** or any other environment file.

### 1.6 EF Core configuration

`Backend/StaySphere.Infrastructure/DependencyInjection.cs`:

```csharp
var connectionString = configuration.GetConnectionString("StaySphere")
    ?? "Data Source=staysphere.db";
services.AddDbContext<StaySphereDbContext>(o => o.UseSqlite(connectionString));
services.AddScoped<IStaySphereDbContext>(sp => sp.GetRequiredService<StaySphereDbContext>());
services.AddScoped<JsonRoomCatalogSeeder>();
services.AddScoped<DatabaseInitializer>();
services.AddSingleton<IClock, SystemClock>();
services.AddSingleton<IBookingReferenceGenerator, BookingReferenceGenerator>();
```

* Provider: `Microsoft.EntityFrameworkCore.Sqlite`.
* Migrations are **compiled into `StaySphere.Infrastructure.dll`** (`20260827174159_InitialCreate`), so `MigrateAsync()` at runtime needs **no `dotnet-ef` CLI** in the image.
* `StaySphereDbContextFactory` (design-time only) hard-codes `Data Source=staysphere.db`; used by `dotnet ef` on a developer machine, **never at runtime**.
* `StaySphereDbContext.BeginImmediateTransactionAsync()` uses `SqliteConnection` + `BEGIN IMMEDIATE` for the booking critical section — this is a **single-file, single-writer** design (see §3).

### 1.7 SQLite connection string

* Effective value comes from `ConnectionStrings:StaySphere` (config) or the code fallback — both are `Data Source=staysphere.db`.
* The path is **relative to the process current working directory (content root)**, not to `AppContext.BaseDirectory`.
  * `dotnet run` → CWD is the project folder → `Backend/StaySphere.Api/staysphere.db` (this untracked file already exists locally).
  * Container → CWD is the image `WORKDIR` (e.g. `/app`) → DB would be created at `/app/staysphere.db` (inside the container's ephemeral layer unless a volume is mounted there).
* **Override mechanism (already supported, no code change):**
  * config key `ConnectionStrings:StaySphere`, or
  * environment variable **`ConnectionStrings__StaySphere`** (double underscore), or
  * `appsettings.Production.json`.

### 1.8 Database initialization / migration behaviour

`Backend/StaySphere.Infrastructure/Persistence/DatabaseInitializer.cs`, run once per process start from `Program.cs`:

1. `await _db.Database.MigrateAsync()` — creates the DB file if missing and applies any pending migrations. Idempotent when there is nothing pending.
2. `PRAGMA journal_mode=WAL;` — persisted in the file; effectively one-time. Creates `staysphere.db-wal` and `staysphere.db-shm` sidecar files in the same directory.
3. `JsonRoomCatalogSeeder.SeedAsync()` — see §1.9.
4. `SeedSampleReservationsAsync()` — see §1.9.

There is **no `EnsureDeleted` / `EnsureCreated` / drop**. Startup never destroys data.

### 1.9 Database seed behaviour

* **Catalog reference data** (4 room types, 8 rooms, 10 amenities, amenity links) is embedded in the migration via `HasData` (`InsertData`). It materialises when the migration is applied — i.e. on first boot against an empty database.
* **`JsonRoomCatalogSeeder`** reads every file in `Seeding:RoomsFiles` (default `Data/room-seed.json`, resolved against `AppContext.BaseDirectory` — so it must be published next to the DLLs; it is, via `CopyToOutputDirectory=PreserveNewest`). Insert only when **no row with that explicit `id` exists**. Idempotent; safe on every restart; malformed files are logged and skipped, not fatal. The shipped `Data/room-seed.json` adds 2 amenities, 1 room type, 8 rooms.
* **Sample reservations** are inserted **only if the `Reservations` table is empty**. Two date-relative confirmed reservations (`today+3..+6`, `today+10..+12`). Once any reservation exists (seeded or real), this block is skipped forever.

**Net effect:** with a persistent volume the seed runs meaningfully **once** (first boot on an empty file), then every subsequent boot is a no-op. Nothing is recreated or overwritten per start.

### 1.10 Application listening URL / ports

* **No `UseUrls` / `ASPNETCORE_URLS` / `ASPNETCORE_HTTP_PORTS` in source or `appsettings*.json`. No `Kestrel` config section.**
* `Backend/StaySphere.Api/Properties/launchSettings.json` sets `http://localhost:5276` (and `https://localhost:7265`) — but **`launchSettings.json` is a Visual Studio / `dotnet run` artifact only. It is not packaged and is ignored in containers / Kubernetes.**
* Therefore in a container the port is whatever the ASP.NET Core defaults / env vars say:
  * `mcr.microsoft.com/dotnet/aspnet:10.0` sets `ASPNETCORE_HTTP_PORTS=8080` → app listens on **`http://+:8080`** with no extra configuration.
  * Fully overridable at runtime via `ASPNETCORE_HTTP_PORTS` or `ASPNETCORE_URLS` (e.g. `http://+:5000`).

### 1.11 Health endpoint

**None exists.** Options for Kubernetes probes without touching business code:

* Minimal/no code change: TCP socket probe on the HTTP port for liveness + readiness (proves Kestrel is up; does not prove the DB opened).
* Recommended small, deployment-only addition (see §6, treat as part of the deployment work, not this assessment): register `AddHealthChecks()` and `MapHealthChecks("/health")` (liveness) and a readiness check that calls `DbContext.Database.CanConnectAsync()`. `Microsoft.Extensions.Diagnostics.HealthChecks` ships with the framework — no new package.

### 1.12 Swagger configuration

* `AddSwaggerGen` with a single `v1` doc (`"StaySphere API"`), always registered.
* `UseSwagger()` / `UseSwaggerUI()` / root redirect are **gated on `IsDevelopment()`**.
* Consequence for K8s: if `ASPNETCORE_ENVIRONMENT=Production`, Swagger UI is **off** and `/` is 404. For a local cluster it may be convenient to run the pod as `ASPNETCORE_ENVIRONMENT=Development` (enables Swagger at `/swagger`) — acceptable for a local box, not for anything shared.

### 1.13 CORS configuration

* One policy, `"frontend"`, applied globally via `app.UseCors("frontend")`.
* Origins come from `Cors:AllowedOrigins` (`string[]`). Default: `["http://localhost:3000"]`. `AllowAnyHeader().AllowAnyMethod()`, credentials not allowed.
* Empty/missing array ⇒ policy with no origins ⇒ browsers get no CORS headers.
* Override at runtime with indexed env vars: `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, … (each replaces one array slot).

### 1.14 Frontend API URL assumptions

* `frontend/staysphere-web/src/lib/config.ts`: `API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL?.trim() || "http://localhost:7265"`.
* `.env.example` / `.env.local`: `NEXT_PUBLIC_API_BASE_URL=http://localhost:5276` (note: differs from the code fallback `7265`).
* `src/lib/api.ts` calls the API with `fetch(..., { cache: "no-store" })`. These calls run **in the browser / on the Next server** and hit the API **directly**.
* Because the var is `NEXT_PUBLIC_*`, it is **baked at build time** and consumed by the user's browser. The API URL must be reachable **from outside the cluster** (NodePort / ingress host / `kubectl port-forward`), not just from a ClusterIP. The frontend is **out of scope** for this task but this constrains how the API Service is exposed and what `Cors:AllowedOrigins` must contain.

### 1.15 Existing Docker files

**None.** No `Dockerfile`, `docker-compose*.yml`, `.dockerignore`, or `containerapp` config anywhere in the repo.

### 1.16 Existing Kubernetes files

**None.** No `*.yaml` / `*.yml` manifests, no Helm chart, no Kustomize, no Skaffold/Tilt config.

### 1.17 Existing scripts / documentation

* No `*.sh` / `*.ps1` scripts.
* `Backend/README.md` documents local run (`dotnet run --project StaySphere.Api`), the config table (`ConnectionStrings:StaySphere` / `__` form, `Cors:AllowedOrigins`, `Seeding:RoomsFiles`), and that `staysphere.db` is created in the API working directory and git-ignored.
* `Docs/decisions.md` §6 and the "SQLite concurrency — known limitations" section document the single-writer model, `BEGIN IMMEDIATE`, WAL, 30s busy timeout, and that production would move to PostgreSQL/SQL Server without app changes.
* `Docs/database.md` documents schema, seed, and that the connection string is overridable via `ConnectionStrings__StaySphere`.
* `.gitignore` excludes `*.db`, `*.db-shm`, `*.db-wal`, `*.sqlite`, `.env`, `.env.local`.

---

## 2. Answers to the required questions

| # | Question | Answer |
|---|---|---|
| 1 | What port does ASP.NET Core currently listen on? | No port is set in code/config. Locally it uses `launchSettings.json` → **HTTP 5276** (HTTPS 7265). In a container it uses the `aspnet:10.0` image default → **HTTP 8080**. `launchSettings.json` does not apply in containers. |
| 2 | Can the port be configured via environment variables? | **Yes**, with no code change: `ASPNETCORE_HTTP_PORTS` (e.g. `8080`) or `ASPNETCORE_URLS` (e.g. `http://+:8080`). |
| 3 | Is the SQLite database path hardcoded? | It has a **hardcoded default** (`Data Source=staysphere.db`, both in `appsettings.json` and as a code fallback), but it is **read from configuration first**, so it is overridable. The path is **relative to the process working directory**. |
| 4 | Can the database path be configured for VS / Docker / Kubernetes? | **Yes, all three**, via `ConnectionStrings:StaySphere` (config), env var `ConnectionStrings__StaySphere`, or an env-specific `appsettings` file. VS: default relative file. Docker/K8s: set `ConnectionStrings__StaySphere=Data Source=/data/staysphere.db` pointing at a mounted volume. |
| 5 | Does the app automatically run EF Core migrations? | **Yes.** `DatabaseInitializer.InitializeAsync()` calls `_db.Database.MigrateAsync()` on **every startup**. It creates the file if absent and applies pending migrations; a no-op when up to date. |
| 6 | Does the seed process recreate data every startup? | **No.** Catalog data is migration `HasData` (applied once). `JsonRoomCatalogSeeder` inserts only ids not already present. Sample reservations are inserted **only when `Reservations` is empty**. Restarts do not overwrite or duplicate. |
| 7 | Is the app safe to run against a persistent SQLite file? | **Yes**, given the constraints: exactly **one writer** (single pod replica), a volume with correct POSIX file locking (node-local, not NFS/SMB), and the WAL sidecar files (`-wal`, `-shm`) living in the same mounted directory. Startup is non-destructive. |
| 8 | Does the API need environment-specific configuration? | **Minimal.** Required: `ConnectionStrings__StaySphere` (volume path), `Cors__AllowedOrigins__0` (frontend origin), and the listen port (`ASPNETCORE_HTTP_PORTS`, or accept 8080). Optional: `ASPNETCORE_ENVIRONMENT` (`Development` to expose Swagger locally; otherwise `Production`), log levels. No secrets exist. |
| 9 | What CORS configuration is needed when the frontend runs separately? | Set `Cors__AllowedOrigins__0` (and further indices) to the **exact browser origin(s)** of the frontend — scheme + host + port, no trailing slash — e.g. `http://localhost:3000`, or the NodePort/ingress origin the frontend is actually served from. The existing policy already sends `AllowAnyHeader`/`AllowAnyMethod`; credentials are not used. |
| 10 | Existing deployment assumptions that conflict with containers? | (a) `launchSettings.json` ports (5276/7265) are VS-only and will mislead if assumed. (b) Relative DB path resolves against CWD, so the DB lands inside the container FS unless redirected to a volume. (c) `migrate-on-startup` + more than one replica ⇒ migration race + multi-writer corruption on one SQLite file. (d) Swagger and `GET /` only exist in `Development`. (e) `.env.local`/code disagree on the API port (`5276` vs `7265`) — frontend concern, but note it. (f) No health endpoint for probes. (g) HTTP-only (no HTTPS redirect) — fine behind an ingress, intentional. |

---

## 3. Deployment risks

| Risk | Severity | Detail / mitigation |
|---|---|---|
| **Data loss on pod restart** | High | Without a PersistentVolume the SQLite file lives in the container's writable layer and is lost on every restart/reschedule. → PVC mounted at the DB directory. |
| **SQLite corruption on networked storage** | High | WAL + SQLite rely on correct `fcntl` byte-range locks. NFS/SMB/some CSI drivers implement these unreliably. → Use a **node-local** volume (kind/minikube `local-path`, Docker Desktop `hostPath`, or a `local` PV). |
| **Multi-writer corruption / migration race** | High | Two+ replicas open the same file, and each runs `MigrateAsync()` at boot. SQLite has one writer; concurrent schema/`BEGIN IMMEDIATE` writers can corrupt or deadlock. → **`replicas: 1`** and Deployment **`strategy: Recreate`** (never run old+new pod together on the same volume). |
| **Volume not writable by container user** | Medium | Recent .NET images may run as a non-root user; a freshly provisioned volume may be root-owned. → `securityContext.fsGroup`, or an `initContainer` that `chown`s the mount, or `runAsUser: 0` for a local box. The process must be able to create `staysphere.db`, `-wal`, `-shm` and lock files in the directory. |
| **Wrong port assumption** | Medium | Manifests copied around often hard-code 5276. The container listens on 8080 (image default) unless `ASPNETCORE_HTTP_PORTS`/`ASPNETCORE_URLS` says otherwise. → Pin the port explicitly in the manifest and match `containerPort`, Service `targetPort`, and probes. |
| **No health endpoint** | Medium | K8s probes have nothing HTTP to hit that confirms readiness. → Add `/health` (deployment-support change) **or** use TCP probes and accept they don't test the DB. |
| **CORS blocks the browser** | Medium | Default origins are `http://localhost:3000` only. If the frontend is served from a NodePort/ingress origin, every API call fails CORS. → Set `Cors__AllowedOrigins__*`. |
| **Frontend cannot reach a ClusterIP** | Medium | `NEXT_PUBLIC_API_BASE_URL` is used by the browser. → Expose the API via NodePort / ingress / `port-forward`; the frontend build must point at that externally-reachable URL. |
| **Swagger absent in Production** | Low | Expected. If manual API poking is wanted locally, run the pod with `ASPNETCORE_ENVIRONMENT=Development`. |
| **`staysphere.db` accidentally baked into the image** | Low | The local file exists at `Backend/StaySphere.Api/staysphere.db`. `dotnet publish` does not include it (not `Content`), but a naive `COPY . .` before publish plus a bad `.dockerignore` could. → `.dockerignore` must exclude `*.db*`, `bin/`, `obj/`, `.vs/`, `frontend/`. |
| **Busy-timeout failures under load** | Low (local) | Concurrent writers wait up to ~30s then get `SQLITE_BUSY`; `CreateReservation` maps commit `DbUpdateException` to 409. Acceptable for a local single-replica deployment; documented in `decisions.md`. |
| **Clock / timezone** | Low | Seed and `CreatedAtUtc` use `DateTime.UtcNow` / `DateTimeOffset.UtcNow`; container TZ is irrelevant. No action. |

---

## 4. Configuration changes required

All of these are **runtime configuration only — no source code changes**.

### 4.1 New file: `Backend/StaySphere.Api/appsettings.Production.json` (optional but recommended)

Lets image defaults be sane without long env-var lists. Values still overridable by env vars. Minimum useful content:

```jsonc
{
  "ConnectionStrings": { "StaySphere": "Data Source=/data/staysphere.db" },
  "Cors": { "AllowedOrigins": [ "http://localhost:3000" ] }
}
```

(If you prefer "config lives only in K8s", skip this file and set everything via ConfigMap env vars.)

### 4.2 Environment variables (ConfigMap → container `env`)

| Variable | Value (example) | Purpose |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` (or `Development` for local Swagger) | selects `appsettings.{env}.json`, Swagger gate |
| `ASPNETCORE_HTTP_PORTS` | `8080` | explicit listen port (or accept image default 8080) |
| `ConnectionStrings__StaySphere` | `Data Source=/data/staysphere.db` | SQLite file on the mounted volume |
| `Cors__AllowedOrigins__0` | `http://localhost:3000` | first allowed browser origin |
| `Cors__AllowedOrigins__1` | `http://localhost:30080` | (optional) NodePort/ingress origin of the frontend |
| `Logging__LogLevel__Default` | `Information` | (optional) |

No secrets are involved, so a `ConfigMap` is sufficient; no `Secret` object is required.

### 4.3 `.dockerignore` (new, at build context root)

Exclude at minimum: `**/bin/`, `**/obj/`, `**/.vs/`, `**/*.db`, `**/*.db-wal`, `**/*.db-shm`, `**/*.sqlite`, `frontend/`, `Docs/`, `**/*.user`, `.git/`, `Tests/`.

### 4.4 Deployment-support source addition (recommended, treat as part of the deploy work)

Add a health endpoint in `Program.cs`:

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<StaySphereDbContext>("db");
...
app.MapHealthChecks("/health");            // liveness: process up
app.MapHealthChecks("/health/ready");      // readiness: + DB reachable (filter by tag/predicate)
```

Uses only in-framework packages. If this is disallowed for now, fall back to TCP probes (see §7). This is the only code change contemplated and it is additive, deployment-only, and touches no business logic.

---

## 5. Recommended Docker approach

**Multi-stage build, build context = repo root (or `Backend/`).**

```dockerfile
# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Backend/StaySphere.slnx Backend/
COPY Backend/StaySphere.Api/StaySphere.Api.csproj                     Backend/StaySphere.Api/
COPY Backend/StaySphere.Application/StaySphere.Application.csproj       Backend/StaySphere.Application/
COPY Backend/StaySphere.Domain/StaySphere.Domain.csproj                Backend/StaySphere.Domain/
COPY Backend/StaySphere.Infrastructure/StaySphere.Infrastructure.csproj Backend/StaySphere.Infrastructure/
RUN dotnet restore Backend/StaySphere.Api/StaySphere.Api.csproj
COPY Backend/ Backend/
RUN dotnet publish Backend/StaySphere.Api/StaySphere.Api.csproj -c Release -o /app --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# DB path/CORS/env supplied by Kubernetes, not baked here.
ENTRYPOINT ["dotnet", "StaySphere.Api.dll"]
```

Notes:
* Restore only the `Api` project — it transitively pulls Application/Infrastructure/Domain. The Tests project is excluded from the image.
* `Data/room-seed.json` is carried into `/app/Data/` automatically by `dotnet publish` (it is `Content` / `CopyToOutputDirectory`). The JSON seeder resolves it via `AppContext.BaseDirectory` = `/app`, so it is found.
* Do **not** `COPY` `staysphere.db`; `.dockerignore` enforces this.
* Keep the DB **out** of `WORKDIR` at runtime — it belongs on the mounted volume at `/data` (set by `ConnectionStrings__StaySphere`).
* Optional hardening for later: `-noble-chiseled` runtime image, `USER $APP_UID`, trimmed/AOT — not required for a local cluster.
* Build for the cluster's node arch (arm64 on Apple Silicon `minikube`/`kind`).

**Image load into the local cluster** (no registry): `kind load docker-image staysphere-api:local` / `minikube image load ...` / k3d `--import`. Set `imagePullPolicy: IfNotPresent` (or `Never`).

---

## 6. Recommended Kubernetes approach

Single namespace, single stateful-ish Deployment (Deployment + PVC is adequate here; a StatefulSet is not needed for one replica).

**Objects:**

1. **Namespace** `staysphere`.
2. **ConfigMap** `staysphere-api-config` — the env vars from §4.2.
3. **PersistentVolumeClaim** `staysphere-db` — `accessModes: [ReadWriteOnce]`, small (e.g. `1Gi`), `storageClassName` = the local provisioner (`standard`/`local-path`/`hostpath` depending on the distro).
4. **Deployment** `staysphere-api`:
   * `replicas: 1` (hard requirement — see §3).
   * `strategy.type: Recreate` (never two pods on the volume at once).
   * `containers[0].image: staysphere-api:local`, `imagePullPolicy: IfNotPresent`.
   * `ports: [{ containerPort: 8080 }]`.
   * `envFrom: [{ configMapRef: { name: staysphere-api-config } }]`.
   * `volumeMounts: [{ name: db, mountPath: /data }]`; `volumes: [{ name: db, persistentVolumeClaim: { claimName: staysphere-db } }]`.
   * `securityContext` (pod): `fsGroup: 1654` (or the image's app GID) so `/data` is group-writable; `runAsNonRoot: true` if the image supports it. For a throwaway local box, `runAsUser: 0` is an acceptable shortcut.
   * Probes:
     * with `/health` added: `livenessProbe` HTTP GET `/health` :8080; `readinessProbe` HTTP GET `/health/ready` :8080; `startupProbe` HTTP GET `/health` with generous `failureThreshold` to cover first-boot migration.
     * without code change: `livenessProbe`/`readinessProbe` `tcpSocket: { port: 8080 }`, `startupProbe` TCP with `failureThreshold: 30, periodSeconds: 2`.
   * `resources`: requests `100m` / `128Mi`, limits `500m` / `256Mi` (tune later).
5. **Service** `staysphere-api` — `type: ClusterIP`, `port: 80`, `targetPort: 8080`. For browser/frontend access add one of:
   * `type: NodePort` (e.g. `nodePort: 30080`), or
   * an Ingress (`staysphere.localdev` etc.), or
   * `kubectl port-forward svc/staysphere-api 8080:80` for ad-hoc use.

**Migration/seed execution:** left to the app's own startup path (`DatabaseInitializer`). With a single replica and `Recreate` this is safe and simple; a separate migration `Job`/`initContainer` is **not** warranted (the migrations assembly is already in the image and the operation is idempotent). Give the `startupProbe` enough slack for the first-boot migrate + seed.

**Optional `initContainer`** (only if the volume comes up root-owned and `fsGroup` is not honoured by the local provisioner): `busybox` running `mkdir -p /data && chown -R 1654:1654 /data`.

---

## 7. SQLite persistence approach

* **One PVC, `ReadWriteOnce`, node-local storage class.** Mount it at a directory (`/data`), not a file path — SQLite needs to create `staysphere.db`, `staysphere.db-wal`, `staysphere.db-shm`, and lock files beside it.
* **Point the app at it:** `ConnectionStrings__StaySphere=Data Source=/data/staysphere.db`.
* **Exactly one writer:** `replicas: 1` + `strategy: Recreate`. Do not scale up; do not use `RollingUpdate` on this Deployment.
* **Avoid networked filesystems** (NFS, SMB, CephFS/`RWX`). Use `local-path` (kind/k3d), `hostpath`/Docker Desktop storage, or a `local` PV bound to the node. WAL locking on network FS risks `SQLITE_IOERR` / corruption.
* **Backups (local):** scale to 0, copy the three `staysphere.db*` files off the volume (or `sqlite3 /data/staysphere.db ".backup /data/backup.db"` while running), scale back to 1.
* **Reset to clean seed:** delete the PVC (or the files) and let the next pod re-run migrate + seed.
* **Known limits** (from `Docs/decisions.md`): single-writer throughput, no row locking, 30s busy timeout then `SQLITE_BUSY`→409. Acceptable for local/dev; production would swap SQLite for PostgreSQL/SQL Server with no application/domain change.

---

## 8. Port mapping

| Hop | Value | Set by |
|---|---|---|
| Kestrel listen | `http://+:8080` | `ASPNETCORE_HTTP_PORTS=8080` (or `aspnet:10.0` image default) |
| `containerPort` | `8080` | Deployment |
| Service `port` → `targetPort` | `80` → `8080` | Service |
| External (browser / frontend) | NodePort `30080`, or ingress `:80`, or `port-forward 8080:80` | Service type / Ingress / kubectl |
| Frontend `NEXT_PUBLIC_API_BASE_URL` | must equal the External row (e.g. `http://localhost:30080`) | frontend build-time env (out of scope, noted) |

Local dev ports `5276` / `7265` from `launchSettings.json` are **not used** in the container and should not appear in any manifest.

---

## 9. Environment configuration summary

| Concern | Local Visual Studio (`dotnet run`) | Docker / Kubernetes |
|---|---|---|
| Env name | `Development` (from `launchSettings.json`) | `Production` (or `Development` to expose Swagger locally) |
| Port | 5276 / 7265 (`launchSettings.json`) | 8080 (`ASPNETCORE_HTTP_PORTS`) |
| DB path | `staysphere.db` next to the project (CWD) | `/data/staysphere.db` on a PVC (`ConnectionStrings__StaySphere`) |
| CORS origins | `http://localhost:3000` (`appsettings.json`) | `Cors__AllowedOrigins__*` = real frontend origin(s) |
| Swagger | on (`/swagger`, `/` redirect) | off unless env = `Development` |
| Seed files | `Data/room-seed.json` from output dir | same, baked into the image at `/app/Data/` |
| Secrets | none | none (ConfigMap only) |
| Migrations | auto on startup | auto on startup (single replica) |

---

## 10. Migration & seed considerations

* **Auto-migrate on boot is retained.** It is idempotent and, with a single replica + `Recreate` strategy, race-free. No migration `Job` needed.
* **First boot on an empty volume:** `MigrateAsync()` creates the schema and the `HasData` catalog; `JsonRoomCatalogSeeder` adds the JSON rooms; sample reservations are inserted (table empty). This can take a few seconds — size the `startupProbe` accordingly (`failureThreshold` × `periodSeconds` ≥ ~60s).
* **Subsequent boots:** `MigrateAsync()` is a no-op (nothing pending); JSON seeder finds all ids present and inserts nothing; sample-reservation block is skipped (reservations exist). No data churn.
* **Adding rooms later:** edit `Data/room-seed.json` with new unique `id`s and rebuild the image; the seeder picks them up on the next restart without a migration.
* **Schema changes later:** add an EF migration on a dev machine (`dotnet ef migrations add …` via the existing design-time factory), rebuild the image; the new migration applies automatically on the next pod start. The rollout must be `Recreate` so the new pod is the only one touching the file when it migrates.
* **Multi-replica is unsupported** with this persistence model — both for the writer lock and for the concurrent-migration hazard. If horizontal scale is ever needed, switch the provider to PostgreSQL/SQL Server (app/domain code unchanged per `decisions.md`) and move migration execution to a pre-deploy `Job`.
* **WAL sidecar files** (`-wal`, `-shm`) must share the mounted directory with the main file; a checkpoint on clean shutdown folds the WAL back into the main file, but do not assume a graceful stop — the volume must retain all three.

---

## 11. Recommended implementation sequence

1. **Add `.dockerignore`** at the build-context root (exclude `bin/`, `obj/`, `.vs/`, `*.db*`, `*.sqlite`, `frontend/`, `Docs/`, `Tests/`, `.git/`, `*.user`).
2. **(Recommended) Add health endpoints** in `Program.cs` — `AddHealthChecks().AddDbContextCheck<StaySphereDbContext>()`, `MapHealthChecks("/health")` and `"/health/ready"`. Framework-only; additive. Skip only if a no-code-change constraint stands, then use TCP probes.
3. **(Optional) Add `appsettings.Production.json`** with `/data` connection string and default CORS origins, so the image is sane without a long env list.
4. **Write the multi-stage `Dockerfile`** (§5). Build locally: `docker build -t staysphere-api:local .`.
5. **Smoke-test the container directly:**
   `docker run --rm -p 8080:8080 -e ConnectionStrings__StaySphere='Data Source=/data/staysphere.db' -v staysphere_db:/data staysphere-api:local`
   then hit `GET /api/rooms/search?checkIn=…&checkOut=…&guests=2` and `GET /health`. Restart the container and confirm data persists and seed does not duplicate.
6. **Provision the local cluster** (kind / minikube / k3d / Docker Desktop) and load the image (`kind load docker-image …` / `minikube image load …`).
7. **Author manifests** in `deploy/k8s/` (or a Kustomize base): `namespace.yaml`, `configmap.yaml`, `pvc.yaml`, `deployment.yaml` (`replicas: 1`, `strategy: Recreate`, volume at `/data`, probes, `securityContext.fsGroup`), `service.yaml` (ClusterIP + NodePort).
8. **Apply and verify:** `kubectl apply -k deploy/k8s/`; watch the pod reach Ready; check logs for `"StaySphere API ready."` and the migrate/seed log lines; `kubectl port-forward svc/staysphere-api 8080:80` and exercise search + create + confirmation.
9. **Restart-persistence test:** `kubectl rollout restart deploy/staysphere-api`; confirm the previously created reservation is still retrievable and no seed duplication occurred.
10. **Wire CORS/exposure for the (future) frontend:** set `Cors__AllowedOrigins__*` to the frontend origin and expose the API via NodePort/ingress; rebuild the frontend with `NEXT_PUBLIC_API_BASE_URL` = that external URL. (Frontend deployment itself is out of scope.)
11. **Document** the final commands and manifest layout in `Backend/README.md` / `Docs/` and record the container decision in `Docs/decisions.md`.

---

## 12. Docker image — implemented

Artifacts added (no application source changed):

| File | Purpose |
|---|---|
| `Backend/Dockerfile` | Production-style multi-stage build (`sdk:10.0` → publish → `aspnet:10.0`). |
| `Backend/.dockerignore` | Trims the build context; **excludes `*.db` / `*.db-wal` / `*.db-shm` / `*.sqlite`**, `bin`/`obj`, `StaySphere.Tests/`, `.vs`/`.vscode`/`.idea`, `.git`, `**/*.md`. |

### 12.1 Image design

* **Build stage** `mcr.microsoft.com/dotnet/sdk:10.0` — matches `net10.0`. Copies the four required project files (`StaySphere.Api`, `StaySphere.Application`, `StaySphere.Domain`, `StaySphere.Infrastructure`) and runs `dotnet restore StaySphere.Api/StaySphere.Api.csproj` as its own cached layer; the test project is not restored or built. Then `dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false`.
* **Runtime stage** `mcr.microsoft.com/dotnet/aspnet:10.0` — matching ASP.NET Core runtime, no SDK. Only the publish output is copied in.
* **Non-root:** runs as `USER $APP_UID` (the `app` user / uid `1654` provided by the aspnet base image). `/app/data` is pre-created and `chown`ed to that uid so named volumes inherit correct ownership.
* **Port:** `ENV ASPNETCORE_HTTP_PORTS=8080` + `EXPOSE 8080`. HTTP only (no HTTPS redirect in the app).
* **Entrypoint:** `["dotnet", "StaySphere.Api.dll"]`.
* Seed file ships correctly: `dotnet publish` places `Data/room-seed.json` next to the DLLs (`/app/Data/`), where `JsonRoomCatalogSeeder` resolves it via `AppContext.BaseDirectory`.
* A local `dotnet publish` of `StaySphere.Api` was run to validate the build stage: it restores/builds all four projects, emits `StaySphere.Api.dll` + `appsettings*.json` + `Data/room-seed.json`, and **no `*.db` file appears in the output**.

### 12.2 Connection-string key — important

The application reads **`ConnectionStrings:StaySphere`** (`configuration.GetConnectionString("StaySphere")` in `StaySphere.Infrastructure/DependencyInjection.cs`). It does **not** read `ConnectionStrings:DefaultConnection`. Setting `ConnectionStrings__DefaultConnection` has no effect — the app would fall back to its default `Data Source=staysphere.db` (relative to `/app`, i.e. **not** on the mounted volume) and reservations would not survive a restart.

The correct environment variable is:

```
ConnectionStrings__StaySphere=Data Source=/app/data/staysphere.db
```

Renaming the key in code was rejected: it would change application configuration contract purely to satisfy Docker, which the task forbids.

### 12.3 Build command

```bash
# build context is the Backend/ directory
docker build -t staysphere-api:local Backend
```

### 12.4 Run command (local, persistent SQLite on a bind mount)

```bash
mkdir -p ./.data/staysphere            # host directory for the database

docker run --rm --name staysphere-api \
  -p 8080:8080 \
  -e ConnectionStrings__StaySphere='Data Source=/app/data/staysphere.db' \
  -e Cors__AllowedOrigins__0='http://localhost:3000' \
  -v "$(pwd)/.data/staysphere:/app/data" \
  staysphere-api:local
```

* Add `-e ASPNETCORE_ENVIRONMENT=Development` to enable Swagger UI at `/swagger` for manual poking (the image otherwise runs as `Production`).
* Bind-mount ownership: on Docker Desktop (Windows/macOS) the mount is accessible to the container user. On native Linux, ensure the host dir is writable by uid `1654` (`sudo chown -R 1654:1654 ./.data/staysphere`) or use a named volume (`-v staysphere_db:/app/data`), which inherits the image's ownership.

### 12.5 Port

| | Value |
|---|---|
| Container listen (Kestrel) | `8080` (`ASPNETCORE_HTTP_PORTS`) |
| `EXPOSE` | `8080` |
| Host publish (example) | `-p 8080:8080` |

### 12.6 Database mount

| | Value |
|---|---|
| Container path | `/app/data` (a **directory** — holds `staysphere.db`, `-wal`, `-shm`, lock files) |
| DB file | `/app/data/staysphere.db` |
| Local source | bind mount `./.data/staysphere` **or** named volume `staysphere_db` |
| K8s equivalent | PVC mounted at `/app/data`; `ConnectionStrings__StaySphere=Data Source=/app/data/staysphere.db` |

> Note: earlier sections use `/data` as the illustrative mount path; the container standardises on **`/app/data`**. Use `/app/data` consistently in the Kubernetes manifests.

### 12.7 Environment variables

| Variable | Value (local) | Required? |
|---|---|---|
| `ConnectionStrings__StaySphere` | `Data Source=/app/data/staysphere.db` | **Yes** — puts the DB on the volume |
| `Cors__AllowedOrigins__0` | `http://localhost:3000` | Only when a browser frontend calls the API |
| `ASPNETCORE_HTTP_PORTS` | `8080` (already the image default) | No — override only to change the port |
| `ASPNETCORE_ENVIRONMENT` | unset → `Production`; `Development` enables Swagger | No |

No secrets are involved.

### 12.8 Container verification — STATUS: PASSED (2026-08-28)

Run on Docker Desktop 4.88.1, engine 29.7.2, `linux/amd64`, using a named volume
`staysphere_data` mounted at `/app/data`.

| # | Check | Result |
|---|---|---|
| 1 | `docker build -t staysphere-api:local Backend` | Image built, `402MB`. Restore layer cached from the 4 `.csproj` copies; test project not built. |
| 2 | `docker run -d -p 8080:8080 -e ConnectionStrings__StaySphere='Data Source=/app/data/staysphere.db' -v staysphere_data:/app/data staysphere-api:local` | Container `Up`, `0.0.0.0:8080->8080/tcp`. |
| 3 | Runs as non-root | `docker exec … id` → `uid=1654(app) gid=1654(app)`. |
| 4 | First-boot logs | `StaySphere API starting in Production environment.` → `Applying migration '20260827174159_InitialCreate'.` → `Room seed file /app/Data/room-seed.json: added 11 new record(s).` → `Seeded 2 sample reservation(s).` → `StaySphere API ready.` → `Now listening on: http://[::]:8080`. |
| 5 | DB created on the volume | `/app/data/` contains `staysphere.db`, `staysphere.db-wal`, `staysphere.db-shm`, all owned `app:app` (WAL mode active). |
| 6 | Read path | `GET /api/rooms/search?checkIn=<+90d>&checkOut=<+93d>&guests=2` → `200`, 16 rooms (8 migration + 8 JSON seed, minus none for that window). |
| 7 | Write path | `POST /api/reservations` (roomId 1, 2 guests) → `201`, `bookingReference: STAY-F5AGK950`, `totalPrice: 297`, `status: Confirmed`. |
| 8 | Read-back | `GET /api/reservations/STAY-F5AGK950` → `200`, identical payload. |
| 9 | **Restart persistence** | `docker rm -f staysphere-api` then re-`docker run` on the **same volume**. Second-boot logs: `No migrations were applied. The database is already up to date.` + `Room seed file …: already up to date, nothing added.` + **no** `Seeded … sample reservation(s).` line. `GET /api/reservations/STAY-F5AGK950` → `200` — the reservation created before the restart survived. |

Application behaviour was not modified for Docker. Test container and volume were
removed after the run; the `staysphere-api:local` image is retained.

---

## 13. Kubernetes manifests — implemented

Plain YAML under `k8s/` (no Helm, no Kustomize — a single local service does not
need them). Every object lives in the `staysphere` namespace.

```
k8s/
├── namespace.yaml     Namespace "staysphere"
├── configmap.yaml     externalized non-secret configuration
├── pvc.yaml           SQLite PersistentVolumeClaim (ReadWriteOnce, 1Gi)
├── deployment.yaml    staysphere-api Deployment (replicas: 1, Recreate)
└── service.yaml       staysphere-api Service (NodePort 30080)
```

`configmap.yaml` is the one file beyond the four in the brief. It exists to
satisfy the "keep configuration externalized" requirement — connection string,
CORS origin, environment and port live there, not in the image and not inline in
the Deployment. If a strict four-file layout is preferred, its keys can be moved
into `deployment.yaml` under `spec.template.spec.containers[0].env` with no other
change.

### 13.1 `namespace.yaml`

`kind: Namespace`, `name: staysphere`. All other manifests set
`metadata.namespace: staysphere` explicitly, so they are not sensitive to the
caller's current context.

### 13.2 `configmap.yaml` — `staysphere-api-config`

| Key | Value | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Swagger off; set `Development` to expose `/swagger` |
| `ASPNETCORE_HTTP_PORTS` | `8080` | matches the image default and the container port |
| `ConnectionStrings__StaySphere` | `Data Source=/app/data/staysphere.db` | **the key the app actually reads** (see §12.2) |
| `ConnectionStrings__DefaultConnection` | `Data Source=/app/data/staysphere.db` | brief-requested key; currently unused by the code, set to the same file so behaviour cannot diverge |
| `Cors__AllowedOrigins__0` | `http://localhost:3000` | for the not-yet-deployed frontend |

No `Secret` is used — the application has no secrets. If one is ever introduced
it must go in a `Secret` that is **not** committed.

### 13.3 `pvc.yaml` — `staysphere-data`

`accessModes: [ReadWriteOnce]`, `resources.requests.storage: 1Gi`, **no
`storageClassName`** so the cluster's default provisioner is used. On this
machine that resolves to `standard (default)` — provisioner
`rancher.io/local-path`, `volumeBindingMode: WaitForFirstConsumer`,
`reclaimPolicy: Delete` (a `hostpath` class also exists but is not the default).
`WaitForFirstConsumer` means the PVC stays `Pending` until the Deployment pod is
scheduled — that is expected, not an error. The volume is node-local, which
SQLite WAL locking requires. Mounted by the Deployment at `/app/data`; the
database file is `/app/data/staysphere.db`. The database is never in a
ConfigMap/Secret and never in the container filesystem.

### 13.4 `deployment.yaml` — `staysphere-api`

* `replicas: 1`, `strategy.type: Recreate` — a single SQLite writer, and the RWO
  volume binds to one pod; `Recreate` stops the old pod before the new one
  starts so they never share the file. **No HorizontalPodAutoscaler.**
* Labels/selector: `app: staysphere-api` (selector is minimal and immutable);
  `app.kubernetes.io/{name,part-of}` added on the template for identification.
* Container `api`, image **`staysphere-api:local`**, `imagePullPolicy: IfNotPresent`
  (local image on the node / shared daemon; no registry).
* Port: `containerPort: 8080` named `http`.
* Config: `envFrom: [{ configMapRef: { name: staysphere-api-config } }]`.
* Storage: `volumeMounts: [{ name: data, mountPath: /app/data }]` ←
  `volumes: [{ name: data, persistentVolumeClaim: { claimName: staysphere-data } }]`.
* Pod `securityContext`: `runAsNonRoot: true`, `runAsUser/Group: 1654`,
  `fsGroup: 1654` — makes the PVC group-writable by the image's `app` user so
  SQLite can create `staysphere.db` + `-wal`/`-shm`/lock files.
* Container `securityContext`: `allowPrivilegeEscalation: false`,
  `capabilities.drop: [ALL]`.
* **Probes — TCP, not HTTP**: the API has **no health endpoint** (§1.11), so
  `startupProbe`, `livenessProbe` and `readinessProbe` all use `tcpSocket: http`.
  `startupProbe` allows `30 × 5s = 150s` for the first-boot EF Core migrate +
  seed before liveness/readiness take over. If a `/health` endpoint is added
  later, switch the three probes to `httpGet`.
* `resources`: requests `50m` CPU / `128Mi`, limit `256Mi` memory (no CPU limit).
  Nothing else.

### 13.5 `service.yaml` — `staysphere-api`

`type: NodePort`, `selector.app: staysphere-api`, `port 80 → targetPort http
(8080)`, `nodePort: 30080`. No Ingress.

**Host access depends on the local distro.** On classic (dockershim) Docker
Desktop and on minikube (`minikube service`), NodePort 30080 is published to the
host. On the **kind-based Docker Desktop Kubernetes actually running here it is
NOT** — verified: `127.0.0.1:30080` is refused and the node IP `172.18.0.2` is
not routable from Windows. Use `kubectl -n staysphere port-forward
service/staysphere-api <local>:80` instead (see §15.1/§15.6). The NodePort is
kept in the manifest for portability to distros that do publish it.

### 13.6 Apply order

`kubectl apply -f k8s/` processes files alphabetically, so `configmap.yaml` would
be sent before `namespace.yaml` exists. Create the namespace first:

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/
```

(`--dry-run=client` is order-insensitive — it never contacts the API server.)

### 13.7 Validation — STATUS: PASSED (schema-validated 2026-08-28)

`kubectl apply --dry-run=client -f k8s/` on this machine only reaches
`kubectl` v1.36 bundled with Docker Desktop, whose Kubernetes is **not enabled**;
`--dry-run=client` still needs a reachable API server to build its REST mapper,
so it errored with `dial tcp 127.0.0.1:6443: connection refused`. Validation was
therefore done **offline with `kubeconform` v0.8.0** against the Kubernetes
v1.31 OpenAPI schemas in **strict mode** (unknown fields rejected):

```
$ kubeconform -strict -summary -kubernetes-version 1.31.0 k8s/
k8s/configmap.yaml   - ConfigMap staysphere-api-config   is valid
k8s/pvc.yaml         - PersistentVolumeClaim staysphere-data is valid
k8s/deployment.yaml  - Deployment staysphere-api          is valid
k8s/namespace.yaml   - Namespace staysphere               is valid
k8s/service.yaml     - Service staysphere-api             is valid
Summary: 5 resources found in 5 files - Valid: 5, Invalid: 0, Errors: 0, Skipped: 0
```

Once a cluster is running, also run:

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply --dry-run=server -f k8s/        # server-side admission check
```

Checklist review:

| Check | Result |
|---|---|
| Correct namespace | `staysphere` created in `namespace.yaml`; `metadata.namespace: staysphere` set on all 4 other objects |
| Correct PVC mount | `deployment` mounts `staysphere-data` at `/app/data`; DB path `Data Source=/app/data/staysphere.db` |
| Correct service selector | `service.selector.app=staysphere-api` == `deployment` pod-template label `app` |
| Correct port | container `8080` (name `http`) → Service `port 80` / `targetPort http` → `nodePort 30080`; `ASPNETCORE_HTTP_PORTS=8080` |
| Correct probes | startup/liveness/readiness `tcpSocket: http` (no health endpoint exists); startup budget `30 × 5s = 150s` |
| Correct env var | `ConnectionStrings__StaySphere` (effective) + `ConnectionStrings__DefaultConnection` (brief) via `envFrom` ConfigMap; verified working in the container run (§12.8) |
| Correct image name | `staysphere-api:local`, `imagePullPolicy: IfNotPresent` |
| Single replica | `replicas: 1`, `strategy: Recreate`, no HPA |

### 13.8 SQLite vs horizontal scaling — statement of record

SQLite is used here **only** because this is a local, single-instance
demonstration: one pod, one node-local `ReadWriteOnce` volume, one writer. It is
**not** the target database for a horizontally scaled production deployment —
multiple replicas cannot safely share one SQLite file, and `MigrateAsync()` on
startup would race across pods. For production, keep the same EF Core model and
switch the provider to PostgreSQL or SQL Server (per `Docs/decisions.md`), move
migrations to a pre-deploy `Job`, and only then raise `replicas` / add an HPA.

---

## 14. Local Kubernetes environment & image loading — verified (2026-08-28)

### 14.1 Which local Kubernetes is available

| Distribution | Present? | Notes |
|---|---|---|
| **Docker Desktop Kubernetes** | **Yes — the only one** | Docker Desktop 4.88.1. Kube context `docker-desktop`. Was **disabled**; enabled for this task (§14.2). Under the hood it is a **single-node `kind` cluster** (`docker desktop kubernetes status` → `Mode: kind`), node `desktop-control-plane`, `kind` cluster name `desktop`, Kubernetes `v1.36.1`, runtime `containerd://2.3.1`. |
| Minikube | No | not on `PATH` |
| kind (standalone CLI) | No | not on `PATH` (Docker Desktop embeds kind but exposes no `kind` binary) |
| Rancher Desktop | No | — |
| k3d / microk8s / other | No | — |

So the target is **Docker Desktop Kubernetes**, and because it is kind-based the
image-availability rules are kind's, not the classic shared-daemon behaviour.

### 14.2 Enabling the cluster (done once)

There is no `docker desktop enable kubernetes` subcommand in this version, so it
was enabled by config + restart:

```bash
# add "KubernetesEnabled": true to the Docker Desktop settings store, then:
#   Windows: %APPDATA%\Docker\settings-store.json
#   macOS:   ~/Library/Group Containers/group.com.docker/settings-store.json
docker desktop restart
# wait until: docker desktop kubernetes status  -> State: running
```

`kubectl get nodes` after enable:

```
NAME                    STATUS   ROLES           AGE   VERSION
desktop-control-plane   Ready    control-plane   3m    v1.36.1
```

To undo later: remove the key (or uncheck **Settings → Kubernetes → Enable
Kubernetes**) and `docker desktop restart`; `docker desktop kubernetes
reset-cluster` wipes cluster state.

### 14.3 Making `staysphere-api:local` available to the cluster

The kind node has its **own containerd image store**, separate from the Docker
engine. A local `docker build` image is **not** visible to it — proven with a
throwaway pod:

```
Warning  ErrImageNeverPull  kubelet  Container image "staysphere-api:local" is not present with pull policy of Never
```

It must be loaded into the node. No public registry, no push. The minimal way
(no extra tooling — this is exactly what `kind load docker-image` does
internally):

```bash
docker save staysphere-api:local -o staysphere-api.tar
docker exec -i desktop-control-plane \
  ctr --namespace=k8s.io images import --all-platforms - < staysphere-api.tar
```

`desktop-control-plane` is not shown by `docker ps` (Docker Desktop hides its
infra containers) but `docker exec` into it works. Verify on the node:

```
$ docker exec desktop-control-plane crictl images | grep staysphere
docker.io/library/staysphere-api   local   bd3230c12d27e   116MB
```

Re-running the throwaway pod after the import:

```
Normal  Pulled  kubelet  Container image "staysphere-api:local" already present on machine and can be accessed by the pod
```

→ **image availability confirmed.** The diagnostic pod was deleted; no
application manifests were applied.

**Equivalent one-liner if the standalone `kind` CLI is installed** (Docker
Desktop's cluster is named `desktop`):

```bash
kind load docker-image staysphere-api:local --name desktop
```

**Re-load after every `docker build`** — `ctr images import` overwrites the tag,
so just run the `save` + `import` pair again. The Deployment sets
`imagePullPolicy: IfNotPresent`, so once the tag is on the node the kubelet uses
it and never contacts a registry.

### 14.4 Exact command sequence

Run from the repo root. Steps 1–2 are the only ones needed now; 3–6 are the
deploy (still **not executed** — run them when ready).

```bash
# 1. Build image  (build context = Backend/)
docker build -t staysphere-api:local Backend

# 2. Load image into the Docker Desktop (kind) node
docker save staysphere-api:local -o staysphere-api.tar
docker exec -i desktop-control-plane \
  ctr --namespace=k8s.io images import --all-platforms - < staysphere-api.tar
docker exec desktop-control-plane crictl images | grep staysphere   # verify

# 3. Apply namespace
kubectl apply -f k8s/namespace.yaml

# 4. Apply PVC  (+ ConfigMap it shares the manifest set with)
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/pvc.yaml

# 5. Apply deployment
kubectl apply -f k8s/deployment.yaml

# 6. Apply service
kubectl apply -f k8s/service.yaml

# --- or, after step 3, apply the rest in one go (order-independent for the server) ---
# kubectl apply -f k8s/
```

Windows note: the `docker` / `kubectl` binaries ship under
`%LOCALAPPDATA%\Programs\DockerDesktop\resources\bin`; open a fresh shell after
installing Docker Desktop so that directory is on `PATH`. The commands above use
POSIX `< file` redirection — in PowerShell use
`Get-Content staysphere-api.tar -Raw | docker exec -i … ctr … import -` or run
them from Git Bash / WSL.

---

## 15. Backend deployed to local Kubernetes — verified (2026-08-28)

Applied in order: `namespace.yaml` → `configmap.yaml` → `pvc.yaml` →
`deployment.yaml` → `service.yaml` (ConfigMap before the Deployment because
`envFrom` needs it). Frontend not deployed.

### 15.1 Final status

| Item | Value |
|---|---|
| **Namespace** | `staysphere` — Active |
| **Deployment** | `staysphere-api` — `READY 1/1`, `UP-TO-DATE 1`, `AVAILABLE 1`, image `staysphere-api:local` |
| **Pod** | `staysphere-api-6d797984cd-4v6rl` — `1/1 Running`, `RESTARTS 0`, node `desktop-control-plane`, IP `10.244.0.9` (replacement pod from the persistence test; the first was `…-9jnzf`) |
| **Service** | `staysphere-api` — `NodePort`, ClusterIP `10.96.36.127`, `80:30080/TCP`, selector `app=staysphere-api` |
| **PVC** | `staysphere-data` — **Bound** to `pvc-8d9f35ef-…`, `1Gi`, `RWO`, StorageClass `standard` (`rancher.io/local-path`), reclaim `Delete` |
| **Image used** | `staysphere-api:local` (`imagePullPolicy: IfNotPresent`); on the node as `docker.io/library/staysphere-api:local`, digest `sha256:316f1b49…` (the `ctr images import` ref shows as `import-2026-08-28@sha256:…` — same content) |
| **SQLite location** | `/app/data/staysphere.db` inside the pod, on an `ext4` PVC mount (`/dev/sdf`), backed by the node's local-path dir; `staysphere.db` + `-wal` + `-shm` present, owned `app:app` |
| **API URL (local)** | `http://127.0.0.1:<localPort>` via `kubectl -n staysphere port-forward service/staysphere-api <localPort>:80`. **NodePort 30080 is NOT reachable from the host** on this Docker Desktop *kind* cluster (127.0.0.1:30080 refused; node IP 172.18.0.2 not routable from Windows) — `port-forward` is the correct access method here. |

### 15.2 API access — what responds

| Endpoint | Result |
|---|---|
| `GET /api/rooms/search?checkIn=&checkOut=&guests=` | `200`, 16 rooms (8 migration + 8 JSON-seed) |
| `GET /api/rooms/{id}` | `200` with amenities |
| `POST /api/reservations` | `201` with `bookingReference` |
| `GET /api/reservations/{ref}` | `200` |
| `GET /swagger/index.html` | `404` — Swagger is Development-only; pod runs `ASPNETCORE_ENVIRONMENT=Production` |
| `GET /health` | `404` — no health endpoint exists in the app (probes are TCP, §13.4) |
| `GET /` | `404` — root redirect is Development-only |

### 15.3 Database & seed verification

`kubectl exec -n staysphere <pod> -- ls -la /app/data` →
`staysphere.db`, `staysphere.db-wal`, `staysphere.db-shm` (owned `app:app`);
`df` confirms `/app/data` is a distinct `ext4` mount, not the container rootfs.
First-boot pod logs: `Applying migration '20260827174159_InitialCreate'` →
`room-seed.json: added 11 new record(s)` → `Seeded 2 sample reservation(s)`.
Seeded reservations reachable via API: `STAY-SEED101` (Ava Thompson, room 101)
and `STAY-SEED201` (Liam Carter, room 201).

### 15.4 Persistence test — PASSED

| Step | Result |
|---|---|
| 1. Create reservation | `POST /api/reservations` room 1, 2026-12-26..29 → `201`, **`STAY-EMTDT275`**, total 297 |
| 2. Booking reference recorded | `STAY-EMTDT275` |
| 3. Delete API pod | `kubectl delete pod -n staysphere staysphere-api-6d797984cd-9jnzf` |
| 4. Replacement pod | `staysphere-api-6d797984cd-4v6rl` reached `1/1 Running` in ~12s; logs: `No migrations were applied. The database is already up to date.` / `already up to date, nothing added.` / **no** `Seeded … sample reservation(s).` line |
| 5. Query reservation | `GET /api/reservations/STAY-EMTDT275` on the new pod |
| 6. Confirm | `200` — identical payload (guest, room, dates, `totalPrice 297`, original `createdAtUtc`). Seeded rows and an adjacent booking `STAY-A5ERYPD1` also survived. |

`Old pod → deleted → new pod → same PVC (`pvc-8d9f35ef-…`) → same SQLite file → reservation still present.` The database is on the PVC, never in the container filesystem — persistence configuration is correct, no fix needed.

### 15.5 Double-booking check — behavior intact (no logic changed)

Against the deployed API (verified on both the original and the replacement pod):

| Request | Result |
|---|---|
| Room 1, 12-26..12-29 (exact overlap with `STAY-EMTDT275`) | `409 BookingConflict` — "The selected room is no longer available for the requested dates." |
| Room 1, 12-28..12-31 (partial overlap) | `409` |
| Room 1, 12-29..01-01 (adjacent, `checkIn == existing checkOut`) | `201` — half-open `[CheckIn, CheckOut)` rule honored |
| Room 2, 12-26..12-29 (different physical room, same dates) | `201` — availability is per-room |

### 15.6 Reconnecting to the API later

```bash
kubectl -n staysphere port-forward service/staysphere-api 8080:80
# then: http://127.0.0.1:8080/api/rooms/search?checkIn=2026-12-01&checkOut=2026-12-03&guests=2
```

Verification `port-forward` processes were stopped after the run; the Deployment,
Service and PVC remain running in the cluster.

### 15.7 Commands used

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/pvc.yaml
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
kubectl rollout status deployment/staysphere-api -n staysphere --timeout=150s
kubectl get namespaces
kubectl get pvc,pods,deployment,service -n staysphere
kubectl logs -n staysphere deployment/staysphere-api
kubectl -n staysphere port-forward service/staysphere-api 18080:80
kubectl exec -n staysphere <pod> -- ls -la /app/data
kubectl delete pod -n staysphere <pod>                 # persistence test
kubectl wait --for=condition=Ready pod -n staysphere -l app=staysphere-api --timeout=150s
```

---

## 16. Frontend → Kubernetes backend integration — verified (2026-08-28)

Scope: **configuration only.** No frontend components, business logic, or the
backend were changed. Only environment/config files were touched.

### 16.1 How the frontend gets its API base URL

Single mechanism, single source of truth:

```
NEXT_PUBLIC_API_BASE_URL  (env)
      │  read once
      ▼
src/lib/config.ts   →  export const API_BASE_URL   (trailing slash trimmed;
      │                                              fallback "http://localhost:8080")
      ▼
src/lib/api.ts      →  every request: `${API_BASE_URL}${path}`
      ▼
searchRooms / getRoom / createReservation / getReservation
```

No React component references `process.env` or a literal API URL — `grep -rn
"process.env" src` returns only `config.ts`. So the only change needed is the env
value.

### 16.2 Configuration change

| File | Change |
|---|---|
| `frontend/staysphere-web/.env.local` | `NEXT_PUBLIC_API_BASE_URL=http://localhost:8080` (was `http://localhost:5276`, the Visual Studio port) |
| `frontend/staysphere-web/.env.example` | same value + a comment listing the VS port and the Kubernetes port-forward command |
| `frontend/staysphere-web/src/lib/config.ts` | comment-only tidy — replaced a stray `http://localhost:7265` doc line with a pointer to `.env.local`; the fallback default is `http://localhost:8080` |

`.env.local` is git-ignored (`.gitignore`), so the environment-specific URL is not
committed.

### 16.3 URLs

| Role | URL |
|---|---|
| **Local frontend** | `http://localhost:3000` (`next dev -p 3000`; browser Origin `http://localhost:3000`) |
| **Kubernetes backend (as the frontend sees it)** | `http://localhost:8080` |
| How that maps to the cluster | `kubectl -n staysphere port-forward service/staysphere-api 8080:80` → Service `staysphere-api` (ClusterIP, `80→8080`) → pod `:8080`. NodePort `30080` is **not** host-routable on this Docker Desktop *kind* cluster (§13.5), so port-forward is the access path. The port-forward must be running whenever the frontend is used. |

`http://localhost:8080` is reachable both from the **Next.js server process**
(Server Components: room search, room details, booking-page load, confirmation —
these fetch server-side, no CORS) and from the **browser** (`BookingForm.tsx` is
`"use client"` and POSTs the reservation directly — CORS applies).

### 16.4 CORS

The backend already allows exactly the frontend origin — no change required and
**no wildcard**:

* ConfigMap `staysphere-api-config` → `Cors__AllowedOrigins__0 = http://localhost:3000`
  → `Program.cs` builds the `frontend` policy with `WithOrigins("http://localhost:3000")`.

Verified against the deployed API through the port-forward:

| Probe | Result |
|---|---|
| `OPTIONS /api/reservations` with `Origin: http://localhost:3000`, `Access-Control-Request-Method: POST` | `204` + `Access-Control-Allow-Origin: http://localhost:3000`, `Access-Control-Allow-Methods: POST`, `Access-Control-Allow-Headers: content-type` |
| `POST /api/reservations` with `Origin: http://localhost:3000` | `201` + `Access-Control-Allow-Origin: http://localhost:3000` |
| `GET /api/rooms/search` with `Origin: http://evil.example` | `200` but **no** `Access-Control-Allow-Origin` header → browser would block it |

To change the allowed origin later, edit the ConfigMap and
`kubectl rollout restart deployment/staysphere-api -n staysphere` (it is read at
startup). It is already environment-specific (per-namespace ConfigMap).

### 16.5 End-to-end validation (frontend on :3000 → K8s API on :8080)

| Step | How | Result |
|---|---|---|
| Search → Results | `GET localhost:3000/rooms?checkIn=2027-02-01&checkOut=2027-02-04&guests=2` | `200`; all five room types rendered from live API data |
| Room details | `GET localhost:3000/rooms/1?…` | `200`; "Standard Queen", amenities |
| Booking page | `GET localhost:3000/booking/2?…` | `200`; "Complete your booking" / "Guest details" form |
| Reservation creation | browser-equivalent `POST localhost:8080/api/reservations` with `Origin: http://localhost:3000` | `201`, ref **`STAY-AY1ZMPQJ`**, `Access-Control-Allow-Origin` present. API pod log: `Reservation STAY-AY1ZMPQJ confirmed for room 2 …, total 297` |
| Confirmation retrieval | `GET localhost:3000/booking/confirmation/STAY-AY1ZMPQJ` | `200`, `<title>Booking confirmed · StaySphere</title>`, shows ref + guest "Frontend E2E" + email |

All requests reached the Kubernetes-hosted API (pod logs + `port-forward`
"Handling connection for 8080"). Backend behavior unchanged; no API defect found.

### 16.6 Running it

```bash
# 1. cluster + backend already deployed (§15). Expose it locally:
kubectl -n staysphere port-forward service/staysphere-api 8080:80

# 2. in another shell:
cd frontend/staysphere-web
npm run dev            # http://localhost:3000  (.env.local -> NEXT_PUBLIC_API_BASE_URL=http://localhost:8080)
```

---

*End of document. The StaySphere backend + SQLite persistence run on the local
Docker Desktop (kind) Kubernetes cluster in namespace `staysphere` (pod
`1/1 Running`, Service `NodePort 80:30080` reached via `port-forward`, PVC
`staysphere-data` Bound); migrations + seed ran on first boot; a reservation
survived pod deletion/recreation on the same PVC (§15.4); double-booking still
returns `409` (§15.5). The existing Next.js frontend, pointed at the cluster only
via `NEXT_PUBLIC_API_BASE_URL` in `.env.local`, completes the full
search → results → details → booking → confirmation flow against the
Kubernetes-hosted API with the existing `http://localhost:3000` CORS allow-list
(§16). No application source changed; frontend not deployed to Kubernetes.*
