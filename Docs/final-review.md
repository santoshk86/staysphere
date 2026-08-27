# StaySphere — Final Principal Engineering Review (Stage 1)

**Date:** 2026-08-27
**Scope:** End-to-end review of the completed Stage 1 backend
(`Backend/`) and frontend (`frontend/staysphere-web/`) against
[CLAUDE.md](../CLAUDE.md) and the functional requirements in sections 5–12.
**Reviewer stance:** Principal Software Engineer. No code changed. Stage 2
testing is explicitly out of scope.

---

## 1. Verdict

Stage 1 is **functionally complete and fits the brief**. The layering, the
`DateRange` value object, the SQL-resolved availability query, the
`BEGIN IMMEDIATE` + authoritative re-check double-booking strategy, the
centralized error envelope, and the mobile-first Next.js client are all sound and
appropriately un-gold-plated. The solution shows restraint: no CQRS, no generic
repository, no premature interfaces.

**No P0 (blocking) issues were found.** The booking flow is correct end-to-end,
input is validated server-side, availability is re-checked authoritatively, and
the concurrency approach is documented and was smoke-verified (6 concurrent
identical bookings → one `201`, five `409`).

The findings below are improvements. The two most worth acting on before Stage 2:

* **P1-1** — the half-open overlap predicate is written out by hand in three
  places; the brief explicitly forbids scattering it.
* **P1-2** — a fresh `git clone` of the frontend points at the wrong API
  port/scheme because the code fallback disagrees with every doc and
  `.env.local` is git-ignored.

---

## 2. Requirements coverage

| # | Requirement (CLAUDE.md) | Status | Notes |
|---|------------------------|--------|-------|
| 5 | Search by check-in / check-out / guest count | ✅ | [`RoomService.SearchAsync`](../Backend/StaySphere.Application/Rooms/RoomService.cs) |
| 5 | Return only rooms available for the **entire** range | ✅ | Half-open overlap, resolved in SQL (`NOT EXISTS`), nothing filtered in memory |
| 5 | Capacity ≥ guest count (2-capacity vs 3-guest → not eligible) | ✅ | `Where(room => room.RoomType.MaxGuests >= guests)` |
| 6 | `[CheckIn, CheckOut)` interval; adjacent = available; overlap = conflict | ✅ | [`DateRange`](../Backend/StaySphere.Domain/Common/DateRange.cs); all six overlap cases + invalid range covered by [`DateRangeTests`](../Backend/StaySphere.Tests/Domain/DateRangeTests.cs) |
| 6 | Overlap logic defined once, not scattered | ⚠️ | Value object exists, but the predicate is **re-implemented inline** in two services — see **P1-1** |
| 7 | Listing exposes type, description, price/night, max guests, amenities, image placeholder | ✅ | [`RoomDto`](../Backend/StaySphere.Application/Rooms/RoomDtos.cs) + [`RoomCard`](../frontend/staysphere-web/src/components/RoomCard.tsx) |
| 7 | Select a room → navigate to details | ✅ | `RoomCard` → `/rooms/[roomId]` carrying the search query |
| 8 | Details view: type, description, price, capacity, amenities, image, booking CTA | ✅ | [`rooms/[roomId]/page.tsx`](../frontend/staysphere-web/src/app/rooms/[roomId]/page.tsx) |
| 9 | Booking captures guest name, email, special requests; no auth | ✅ | [`BookingForm`](../frontend/staysphere-web/src/components/BookingForm.tsx) |
| 10 | Reservation creation: validate → validate dates → validate guests → load room → validate capacity → **re-check availability** → create → generate reference → persist → return confirmation | ✅ | [`ReservationService.CreateAsync`](../Backend/StaySphere.Application/Reservations/ReservationService.cs) follows the exact order |
| 10 | Never trust availability from the prior search | ✅ | Authoritative re-check inside the transaction |
| 11 | Backend revalidates at reservation time; reasonable SQLite/EF transactional strategy; no distributed infra; document SQLite limits | ✅ | `BEGIN IMMEDIATE` (`decisions.md` §6) + [SQLite limitations section](decisions.md) |
| 12 | Confirmation shows reference, guest name/email, room info, dates, guest count, price summary, special requests | ✅ | [`ReservationConfirmation`](../Backend/StaySphere.Application/Reservations/ReservationDtos.cs) + [confirmation page](../frontend/staysphere-web/src/app/booking/confirmation/[reference]/page.tsx) |
| 21 | Backend validation: dates exist, checkout > checkin, guests > 0, guests ≤ capacity, name, email, room exists, availability | ✅ | `ValidateRequest` + `DateRange` ctor + domain factory |
| 22 | Consistent 400 / 404 / 409 / 500; conflict distinguishable from server error; centralized | ✅ | [`ExceptionHandlingMiddleware`](../Backend/StaySphere.Api/Middleware/ExceptionHandlingMiddleware.cs); `BookingConflict` vs `ServerError` |
| 24 | Routes `/`, `/rooms`, `/rooms/[roomId]`, `/booking/[roomId]`, `/booking/confirmation/[reference]` | ✅ | Present |
| 25 | UI states: loading, success, empty, validation error, API error, unavailable room, booking conflict, submission, confirmation | ✅ | All handled; conflict has a dedicated message + recovery link |
| 25 | No fake frontend business rules disagreeing with backend | ✅ | `validation.ts` is shape-only; backend authoritative (documented) |
| 26 | Clean, responsive, mobile-first, accessible | ✅ (minor) | See **Accessibility** notes — mostly good; a couple of P3 items |
| 28 | Structured logging: startup, search failures, reservation attempts, successes, conflicts, exceptions | ✅ | Present, and PII is kept out of logs |
| 38 | `docs/` with architecture, api, database, decisions, progress | ✅ | All present and genuinely useful |

**Gaps against requirements:** only #6 ("defined once") is partially unmet — see
P1-1. Everything else in scope is implemented.

---

## 3. Findings

### P0 — correctness / security / blocking

**None.**

The areas most likely to hide a P0 were checked specifically:

* **Concurrency.** `BEGIN IMMEDIATE` takes SQLite's RESERVED lock before the
  check-then-insert; because SQLite serializes *all* write transactions
  database-wide, only one booking runs the critical section at a time and later
  writers observe the committed row. The busy timeout (default 30 s) makes them
  wait rather than fail fast. Correct for SQLite and matches `decisions.md`.
* **Half-open date math.** `Start < other.End && other.Start < End` is the
  correct half-open overlap test; adjacent ranges (`End == other.Start`) return
  `false`. Verified against all six required scenarios in the tests.
* **Availability translation.** The `room.Reservations.Any(...)` predicate
  translates to a SQL `EXISTS` subquery over the owned `CheckIn`/`CheckOut`
  columns; no rooms or reservations are loaded for conflict detection.
* **Money ordering.** `ORDER BY price` sorts on the integer-cents column, not
  lexicographic TEXT — the `MoneyConverter` is doing its job.

---

### P1 — important

#### P1-1 — Date-overlap predicate is scattered across three sites

CLAUDE.md §6: *"Do not scatter date-overlap logic throughout the codebase.
Prefer a well-defined domain abstraction/value object or a clearly isolated
business rule."*

The value object exists, but neither query uses it. The raw predicate
`r.Stay.Start < stay.End && stay.Start < r.Stay.End` appears in:

* [`DateRange.OverlapsWith`](../Backend/StaySphere.Domain/Common/DateRange.cs#L33-L37)
* [`RoomService.SearchAsync`](../Backend/StaySphere.Application/Rooms/RoomService.cs#L36-L39)
* [`ReservationService.CreateAsync`](../Backend/StaySphere.Application/Reservations/ReservationService.cs#L62-L66)

If the rule ever changes (e.g. add a `Pending`/`Hold` status that also blocks, or
a minimum-gap-between-stays policy), three edits are needed and they can drift.

**Recommendation:** expose the overlap rule as one EF-translatable expression and
use it verbatim in both queries. Minimal, not over-engineered — e.g. a static
`Expression<Func<Reservation, bool>> Reservation.Overlapping(DateRange range)`
(or on `DateRange`) that also encodes `Status == Confirmed`. `DateRange.OverlapsWith`
can delegate to the same comparison for the in-memory path.

#### P1-2 — Frontend API base URL is wrong out of the box

[`src/lib/config.ts`](../frontend/staysphere-web/src/lib/config.ts#L6-L7) falls
back to `http://localhost:7265`. Port `7265` is the **HTTPS** endpoint in
[`launchSettings.json`](../Backend/StaySphere.Api/Properties/launchSettings.json#L19);
requesting it over `http://` will not connect. Every other source says
`http://localhost:5276`:
[`.env.example`](../frontend/staysphere-web/.env.example),
[`Docs/api.md`](api.md), [`Docs/progress.md`](progress.md).

`.env.local` (which currently carries the correct `5276`) is git-ignored
(`.gitignore` line 38), so a **fresh clone runs on the broken fallback** and the
app cannot reach the API until the developer discovers `.env.example`.

**Recommendation:** change the `config.ts` fallback to `http://localhost:5276`,
and make `progress.md` ("Default `http://localhost:5276`" in one place,
`5276` referenced elsewhere) internally consistent.

#### P1-3 — Unauthenticated, unthrottled PII endpoint

`GET /api/reservations/{reference}`
([`ReservationsController`](../Backend/StaySphere.Api/Controllers/ReservationsController.cs#L47-L54))
returns guest **name, email, and special requests** to anyone who presents a
reference. The brief permits no-auth, but §27 still requires "basic secure
development practices."

The reference is effectively a bearer capability (`STAY-` + 8 Crockford base32 ≈
1.1 × 10¹² values). There is no rate limiting anywhere in the pipeline, so there
is no defence against reference brute-forcing or general endpoint abuse.

**Recommendation (proportionate):** add ASP.NET Core rate limiting
(`builder.Services.AddRateLimiter(...)`, a fixed/sliding window on the
reservations routes). Optionally note in `api.md` that the reference URL is a
capability and should be treated as sensitive. Full auth is not warranted for
this scope.

---

### P2 — maintainability / improvement

#### P2-1 — Transaction helper leaks SQLite specifics and hand-rolls connection management
[`IStaySphereDbContext.BeginImmediateTransactionAsync`](../Backend/StaySphere.Application/Common/IStaySphereDbContext.cs#L26-L31)
puts a SQLite term ("Immediate") into an Application-layer abstraction, and
[the implementation](../Backend/StaySphere.Infrastructure/Persistence/StaySphereDbContext.cs#L26-L44)
manually opens the `SqliteConnection`, calls
`connection.BeginTransaction(IsolationLevel.Serializable, deferred: false)`, then
adopts it with `Database.UseTransactionAsync`. It works and is documented, but it
bypasses EF's own connection lifecycle and depends on the `deferred` overload.
**Recommendation:** rename to intent (`BeginBookingTransactionAsync` /
`BeginSerializableTransactionAsync`), and prefer
`Database.BeginTransactionAsync(IsolationLevel.Serializable)` or an explicit
`BEGIN IMMEDIATE` via `ExecuteSqlRawAsync` inside EF's normal transaction scope.

#### P2-2 — `DbUpdateException` is unconditionally reported as a booking conflict
[`ReservationService.CreateAsync`](../Backend/StaySphere.Application/Reservations/ReservationService.cs#L96-L101)
catches *any* `DbUpdateException` at commit and rethrows `RoomUnavailableException`
(→ 409). A disk error, schema drift, or an unrelated constraint violation would be
shown to the guest as "the room is no longer available."
**Recommendation:** inspect the inner `SqliteException` (busy / unique-index on
`BookingReference`) and only translate those to 409; let anything else surface as
500 so it is logged as an error and not silently misattributed.

#### P2-3 — Same rule validated in three layers
Capacity is checked in
[`ValidateRequest`](../Backend/StaySphere.Application/Reservations/ReservationService.cs#L165-L168)
(`guestCount >= 1`), again in
[`CreateAsync`](../Backend/StaySphere.Application/Reservations/ReservationService.cs#L51-L55)
(`> MaxGuests`, as `ValidationException`), and again in
[`Reservation.Create`](../Backend/StaySphere.Domain/Reservation.cs#L61-L70)
(as `BusinessRuleViolationException`). Date validity is checked in
`ValidateRequest` and again in the `DateRange` constructor. CLAUDE.md §21: *"Do
not duplicate identical business rules unnecessarily across multiple layers."*
This is defensible as defence-in-depth, but the exception types and messages
diverge. **Recommendation:** pick one owner — keep the domain invariant as the
source of truth and have the application translate the domain exception into a
field-level 400, *or* keep the friendly application pre-check and make the domain
check an assertion guard — and say which in a comment.

#### P2-4 — Frontend duplication: param parsing and query-string building
`firstValue()` is copy-pasted into
[`rooms/page.tsx`](../frontend/staysphere-web/src/app/rooms/page.tsx#L12-L14),
[`rooms/[roomId]/page.tsx`](../frontend/staysphere-web/src/app/rooms/[roomId]/page.tsx#L12-L14),
and [`booking/[roomId]/page.tsx`](../frontend/staysphere-web/src/app/booking/[roomId]/page.tsx#L13-L15).
The `?checkIn=…&checkOut=…&guests=…` string is hand-assembled in `RoomCard`,
`rooms/[roomId]`, `booking/[roomId]`, and `BookingForm`.
**Recommendation:** one `src/lib/searchParams.ts` with `parseSearchParams()` and
`buildSearchQuery(criteria)`.

#### P2-5 — Reads materialize full entity graphs, then map in memory
[`RoomService.SearchAsync`](../Backend/StaySphere.Application/Rooms/RoomService.cs#L30-L44)
`Include`s `RoomType → RoomTypeAmenities → Amenity` and calls `RoomDto.FromEntity`
afterwards. Fine at catalog scale; projecting to `RoomDto` inside the query
(`Select`) would emit an explicit column list and stop loading tracked graphs.
Same pattern in `GetByIdAsync` and both reservation reads. Improvement, not a
defect.

#### P2-6 — Two hand-written copies of the amenity projection
`amenities.Select(l => l.Amenity.Name).OrderBy(n => n).ToList()` appears in both
[`RoomDto.FromEntity`](../Backend/StaySphere.Application/Rooms/RoomDtos.cs#L31-L35)
and
[`ReservationConfirmation.FromEntity`](../Backend/StaySphere.Application/Reservations/ReservationDtos.cs#L49).
Small shared helper removes the drift risk.

#### P2-7 — Frontend / backend "today" reference differ
[`todayIso()`](../frontend/staysphere-web/src/lib/validation.ts#L18-L24) uses the
browser's **local** date; [`SystemClock.Today`](../Backend/StaySphere.Infrastructure/Time/SystemClock.cs#L9)
uses **UTC**. Around midnight a guest can pass the client "not in the past" check
and be rejected by the API (or be blocked from a date the API would accept).
**Recommendation:** choose one reference (UTC, or hotel-local via a configured
time zone) and apply it on both sides; note it in `api.md`.

#### P2-8 — Documentation nits
[`decisions.md`](decisions.md) heading numbers run …9, 11, 10, then a second
"10". [`Backend/README.md`](../Backend/README.md#L36-L39) migration command passes
`--startup-project StaySphere.Infrastructure` (works only because of the
design-time factory; unusual and worth a one-line note). Root
[`README.md`](../README.md) is a stub.

---

### P3 — optional / polish

| # | Item | Location |
|---|------|----------|
| P3-1 | `Reservation.Cancel()` and `ReservationStatus.Cancelled` are unused — no cancellation use case in scope (mild YAGNI; harmless) | [`Reservation.cs`](../Backend/StaySphere.Domain/Reservation.cs#L103-L106) |
| P3-2 | `RoomImage` never consumes the API's `imageUrl` (placeholder is per-brief, but the field is fetched, typed and passed unused) | [`RoomImage.tsx`](../frontend/staysphere-web/src/components/RoomImage.tsx) |
| P3-3 | `notFound()` on dynamic routes renders the 404 UI with HTTP `200` (already documented in `progress.md`) — minor SEO | detail / confirmation pages |
| P3-4 | No `generateMetadata` — dynamic pages show a generic `<title>` ("Room details") instead of the room name | `rooms/[roomId]`, `booking/confirmation/[reference]` |
| P3-5 | `1000`-char special-requests limit is a bare literal in four places (backend const, EF `HasMaxLength`, `validation.ts`, `BookingForm.MAX_REQUESTS`) | multiple |
| P3-6 | `BookingForm` double-submit guard reads React state at entry; a `useRef` latch is race-proof. Backend already makes a duplicate submit safe (one `201` / one `409`), so low risk | [`BookingForm.tsx`](../frontend/staysphere-web/src/components/BookingForm.tsx#L51-L54) |
| P3-7 | Email rules differ: backend `MailAddress` accepts `a@b` (no TLD); frontend regex requires a dot | `ReservationService.IsValidEmail` / `validation.ts` |
| P3-8 | `StaySphereDbContextFactory` hard-codes `Data Source=staysphere.db` (design-time only; different path from the API's DB) | [`StaySphereDbContextFactory.cs`](../Backend/StaySphere.Infrastructure/Persistence/StaySphereDbContextFactory.cs) |
| P3-9 | `JsonRoomCatalogSeeder` explicit-id inserts don't advance SQLite's autoincrement counter — only a concern if runtime code ever inserts a `Room` without an explicit id (none today) | [`JsonRoomCatalogSeeder.cs`](../Backend/StaySphere.Infrastructure/Persistence/Seeding/JsonRoomCatalogSeeder.cs) |
| P3-10 | Search accepts an unbounded far-future / multi-year range | `RoomService.BuildValidatedRange` |
| P3-11 | `MigrateAsync()` on every startup is fine for one instance; would race with multiple API instances | [`DatabaseInitializer.cs`](../Backend/StaySphere.Infrastructure/Persistence/DatabaseInitializer.cs#L32) |
| P3-12 | `text-muted` (#64748b) is ~4.4:1 on the page background — passes AA for body text, borderline for the smallest `text-xs` hints | [`globals.css`](../frontend/staysphere-web/src/app/globals.css#L12) |
| P3-13 | `CreateReservationRequest.RoomId` / `GuestCount` are non-nullable `int` (missing → `0`), while the DTO comment talks about nullable-for-absence; "missing" vs "zero" is indistinguishable (service handles both) | [`CreateReservationRequest.cs`](../Backend/StaySphere.Api/Contracts/CreateReservationRequest.cs) |

---

## 4. Assessment by review dimension

**Requirements coverage** — Complete for the Stage 1 scope. Only §6's "defined
once" clause is partially unmet (P1-1). The 10-step reservation workflow is
implemented in the exact prescribed order, including the authoritative re-check.

**Domain modeling** — Good. `RoomType` (shared attributes + `CalculatePrice`),
`Room` (identity + inventory), `Reservation` (factory + invariants + price
snapshot), `DateRange` (validated, immutable, half-open), string-backed
`ReservationStatus`, a `RoomTypeAmenity` join entity. The model matches §16 and
§20. `Reservation.TotalPrice` as a booking-time snapshot is the right call.

**OOP** — Encapsulated state (private setters, private collection fields exposed
as `IReadOnlyCollection`), factory methods with guard clauses, no public
parameterless constructors leaking into the model (EF uses the private ones).
Not anemic. One minor smell: `Reservation.Create` treats "RoomType not loaded"
as a `BusinessRuleViolationException` — a persistence concern surfacing as a
business rule (P3-ish).

**SOLID** — Applied pragmatically, as the brief asks. SRP is clean
(controller / service / `DateRange` / `DbContext`). DIP via `IStaySphereDbContext`,
`IClock`, `IBookingReferenceGenerator` — each earns its place. No interface-per-class.
The one wrinkle is ISP/DIP leakage: `IStaySphereDbContext` exposes `DbSet<T>` and
a SQLite-flavoured transaction method (P2-1) — a deliberate, documented trade to
avoid a repository layer.

**API boundaries** — Controllers are thin: map request → command, call the
service, translate the result to a status code. No business logic, no queries, no
try/catch. Explicit request/response DTOs. `CreatedAtAction` with a `Location`
header. `[ProducesResponseType]` documents the contract. Good.

**Availability logic** — Correct and efficient: capacity filter + `NOT EXISTS`
overlap subquery, translated to SQL, ordered by integer-cents price then room
number, `AsNoTracking`. Nothing filtered in memory. Backed by
`IX_Reservations_RoomId_Status` and the `(CheckIn, CheckOut)` index on the owned
type.

**Date conflict handling** — The half-open rule is right and centrally *defined*
in `DateRange`; it is just not centrally *used* (P1-1). Test coverage already
exercises exact / partial / containment (both directions) / adjacent / invalid.

**Double-booking behavior** — Appropriate for the stack. Authoritative re-check
inside a `BEGIN IMMEDIATE` transaction; commit-time `DbUpdateException` downgraded
to 409 (too broadly — P2-2). Limitations (single writer, no row locks, busy
timeout, WAL) are documented honestly, along with the Postgres/SQL Server
migration path. No distributed locking or brokers — correct restraint.

**EF Core / SQLite usage** — Idiomatic. `IEntityTypeConfiguration` per entity,
owned `DateRange`, `decimal ↔ long` cents converter with a clear rationale,
enum-as-string, sensible indexes and `DeleteBehavior`, `HasData` for static
catalog, runtime idempotent seeding for date-relative sample data and
JSON-file rooms (correctly kept out of migrations). WAL enabled on startup. No
generic repository, no Unit-of-Work wrapper — as instructed.

**Frontend architecture** — App Router used well: Server Components fetch and
render; only `SearchForm` and `BookingForm` are Client Components; the query
string is the single source of truth for a search (shareable, no client store);
results stream under `<Suspense>` with a `key` that resets on new criteria;
per-route `loading.tsx` and an `error.tsx` boundary. No Redux. Matches §23.

**API integration** — All network access goes through
[`src/lib/api.ts`](../frontend/staysphere-web/src/lib/api.ts); no component calls
`fetch`. A single `ApiError` type carries `status` / `code` / `fieldErrors` /
`traceId`, with `isConflict` / `isNotFound` / `isValidation` / `isNetwork`
helpers. `cache: "no-store"` on every call. Types mirror the contracts. The only
integration problem is the base-URL default (P1-2).

**Validation** — Server-side is complete and correctly layered (application
shape/format checks + domain invariants). Client-side is explicitly shape-only
and defers to the backend, with 400 field errors mapped back onto the form.
Duplication across layers is the main critique (P2-3); the front/back "today"
skew is a real edge (P2-7).

**Error handling** — One middleware, one `ApiErrorResponse` envelope, exhaustive
`switch` over exception types, model-binding failures routed through the same
envelope, `Response.HasStarted` guard, `OperationCanceledException` → 499, 5xx
logged as error / 409 as warning / 4xx as info. Frontend mirrors every state
including a dedicated conflict message with a recovery link. This is a strength.

**Accessibility** — Labelled controls with `aria-describedby` / `aria-invalid`
wired centrally via `FormField`; `role="alert"` on errors; focus moved to the
error summary on a failed booking; `aria-busy` on the submitting form; visible
`:focus-visible` outline; semantic landmarks; `role="img"` + `aria-label` on the
placeholder; `<html lang>`. Gaps are minor: no `generateMetadata` (P3-4),
borderline small-text contrast (P3-12), possible double announcement between the
field-level and summary alerts, no skip link (acceptable at this size).

**Maintainability** — High. Small cohesive files, consistent naming, genuinely
useful comments and docs. Detractors: the duplicated frontend helpers (P2-4), the
tri-layer validation (P2-3), and scattered magic numbers (P3-5).

**Extensibility** — The layering and `IClock` / reference-generator seams make
Stage 2 testing straightforward (fakes, SQLite-in-memory, `Program` is
`public partial`). Adding a room type is config-only (`room-seed.json`). The one
extension point that will bite is a new blocking reservation status — it forces
edits in the three overlap sites (P1-1) and the two `Status == Confirmed`
filters.

**Code duplication** — Covered above: P1-1 (overlap predicate), P2-4 (frontend
param/query helpers), P2-6 (amenity projection), P3-5 (char-limit literal).
Nothing egregious.

**Unnecessary abstractions** — Essentially none. `IStaySphereDbContext` is the
only debatable one and it is justified in `decisions.md` (keeps the dependency
direction correct without a repository). No CQRS, MediatR, generic repository,
UoW, domain-event bus, or global state. The brief's "demonstrate judgment by
staying simple" is met.

**Security** — Reasonable for a no-auth brief: parameterized EF queries only,
input validated server-side, CORS restricted to configured origins (and closed
when unset), Swagger dev-only, no secrets in config, generic 500 messages,
CSPRNG-backed non-sequential public reference with a unique index. Weak spots:
no rate limiting and PII returned from an unauthenticated capability URL (P1-3);
`imageUrl` and other client-supplied fields are never echoed unsanitized (React
escapes output anyway).

**Performance** — Fine for the scope and honest about limits. Availability is one
indexed SQL query; reads use `AsNoTracking`. Opportunities: project to DTOs in
the query instead of materializing graphs (P2-5); `BEGIN IMMEDIATE` serializes
all writes (documented); `MigrateAsync` on every boot (P3-11). No caching by
design (volatile data). No N+1 (the overlap check is a subquery, not a load).

---

## 5. Recommended order of work (when Stage 1 changes are authorized)

1. **P1-1** — extract the overlap rule to one shared expression; use it in both
   queries and in `DateRange.OverlapsWith`.
2. **P1-2** — fix the `config.ts` fallback URL; make `progress.md` consistent.
3. **P1-3** — add rate limiting to the API; note the reference-as-capability in
   `api.md`.
4. **P2-2** — narrow the `DbUpdateException` → 409 translation.
5. **P2-1**, **P2-3**, **P2-4** — naming/leak cleanup, single-owner validation,
   frontend helper extraction.
6. P2 remainder, then P3 as time allows.

None of these are Stage 2 testing work, and none add features beyond the
requirements.
