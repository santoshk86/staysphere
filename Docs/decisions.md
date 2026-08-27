# Architectural Decisions (Stage 1 — Backend)

Lightweight ADR list. Each entry: decision, why, alternatives rejected.

## 1. Five projects, lightweight layering — no CQRS / MediatR / repositories

Api / Application / Domain / Infrastructure / Tests, with plain application
services. **Why:** matches the project brief; the domain is small. A generic
`IRepository<T>` and a Unit-of-Work wrapper add indirection over EF Core without
solving a real problem here. **Rejected:** CQRS, MediatR, generic repository,
domain-event infrastructure.

## 2. `IStaySphereDbContext` abstraction in Application

Application services depend on an interface (DbSets + `SaveChangesAsync` +
`BeginImmediateTransactionAsync`) implemented by the EF `DbContext` in
Infrastructure. **Why:** keeps the dependency direction correct
(Infrastructure → Application), lets services be tested without the SQLite
provider, and still uses EF Core directly (no repository layer).
**Rejected:** referencing the concrete `DbContext` from Application (wrong
direction); per-entity repositories (unnecessary abstraction).

## 3. `DateRange` value object owns all overlap logic

A single immutable `DateRange(DateOnly start, DateOnly end)` validates the range
(`end > start`) and implements the half-open overlap rule
`Start < other.End && other.Start < End`. It is mapped as an EF **owned type** to
`CheckIn` / `CheckOut` columns and used verbatim in the availability query.
**Why:** the brief explicitly forbids scattering overlap logic; one definition is
used by both the in-memory rule and the SQL query. **Rejected:** ad-hoc date
comparisons in services/controllers.

## 4. Pricing and capacity live on `RoomType`, not `Room`

All physical rooms of a type share price, capacity, description and amenities, so
those attributes belong on `RoomType`. `Room` only carries identity
(`RoomNumber`) and its type. `Reservation.TotalPrice` is a **snapshot** taken at
booking time so later price changes don't rewrite history. **Rejected:** price
per physical room (no requirement, invites inconsistency).

## 5. Reservations reference a concrete `Room`

Availability is a property of a physical unit, so a reservation points at a
`Room`, not a `RoomType`. Search returns available physical rooms; a client may
group by `roomTypeId` for a category view.

## 6. Double-booking strategy: `BEGIN IMMEDIATE` + authoritative re-check

`CreateReservation` opens the transaction with SQLite `BEGIN IMMEDIATE`
(`SqliteConnection.BeginTransaction(Serializable, deferred: false)` +
`DbContext.Database.UseTransaction`), then re-queries for an overlapping confirmed
reservation before inserting. **Why:** EF's default deferred `BEGIN` only takes a
write lock on first write, leaving a check-then-insert race. `BEGIN IMMEDIATE`
takes the RESERVED lock up front, so only one booking transaction runs the
critical section at a time; others wait for the busy timeout and then observe the
committed row. Verified with 6 concurrent identical requests → exactly one `201`,
five `409`. **Rejected:** distributed locks, Redis, a message broker, a
serializable-isolation retry loop (overkill for SQLite / this scope).

## 7. Money stored as integer cents

A value converter maps `decimal` ↔ `long` cents. **Why:** SQLite has no decimal
type; EF's default maps `decimal` to TEXT, which sorts lexicographically and
breaks `ORDER BY price`. Integer cents keep money exact and correctly ordered.

## 8. Booking reference = `STAY-` + 8 Crockford base32 chars

Generated from a CSPRNG, checked for uniqueness against the DB (retry up to 5×),
backed by a unique index. **Why:** the public identifier must not be a guessable
sequential database id, and it should be readable aloud (no I/L/O/U).

## 9. Centralized error handling via middleware

One `ExceptionHandlingMiddleware` maps exception types to the `ApiErrorResponse`
envelope and status codes; controllers contain no try/catch. Model-binding
failures are routed through the same envelope via
`ApiBehaviorOptions.InvalidModelStateResponseFactory`.

## 11. Extra rooms seeded from JSON files at startup, idempotent by explicit id

`JsonRoomCatalogSeeder` (run from `DatabaseInitializer` after `Migrate()`) loads
`amenities` / `roomTypes` / `rooms` from the files in `Seeding:RoomsFiles`. Each
record has an explicit `id`; a record is inserted only if no row with that id
exists (checked with `_db.Entry(entity).Property(e => e.Id).CurrentValue = id`
before `Add`, which SQLite accepts as an explicit-key insert). **Why:** lets rooms
be added/managed from config without new migrations, and restarting never
duplicates or overwrites. **Not** done inside a migration because migrations must
be deterministic and file-independent. Malformed files or invalid records are
logged and skipped rather than crashing startup.

## 10. Catalog seeded in migration, sample reservations seeded at runtime

Static reference data (`RoomType`, `Room`, `Amenity`, `RoomTypeAmenity`) uses EF
`HasData` so it ships in the migration. Sample reservations are **date-relative**
(`today + n`), which `HasData` cannot express, so `DatabaseInitializer` inserts
them idempotently on startup.

---

# SQLite concurrency — known limitations

* **Single writer.** SQLite serializes all writes with a database-level lock.
  `BEGIN IMMEDIATE` makes our booking critical section explicit and correct, but
  throughput is one write transaction at a time. Fine for this app; not
  representative of a server RDBMS with row-level locking / MVCC.
* **No `SELECT ... FOR UPDATE`.** Row locking does not exist; we rely on the
  whole-database write lock taken by `BEGIN IMMEDIATE`.
* **Busy timeout, not a queue.** Concurrent writers wait up to
  `Default Timeout` (30s, from the connection string) and then fail with
  `SQLITE_BUSY`. `CreateReservation` treats a `DbUpdateException` at commit as a
  `409` conflict rather than surfacing a 500. Under sustained heavy write
  contention some requests could time out.
* **WAL mode** is enabled so reads don't block the writer, but it does not change
  the single-writer rule.
* **Decimal / date comparisons** are done on converted integer (cents) and ISO
  `yyyy-MM-dd` text columns specifically so ordering and range predicates are
  correct in SQLite.
* A production deployment would move the same EF model to PostgreSQL/SQL Server
  and replace `BEGIN IMMEDIATE` with a unique constraint on a range/exclusion
  index or `SERIALIZABLE` + retry. The application/domain code would not change.
