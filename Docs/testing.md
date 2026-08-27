# StaySphere — Backend Testing (Stage 2)

This document describes the automated backend test suite: what it covers, how it
is structured, and how to run it.

/ Scope of this phase: **backend only**. Frontend (Vitest / RTL) and end-to-end
(Playwright) tests are a separate, later effort and are not part of this suite.

---

## 1. Testing strategy

The suite is layered to match the application's own layers, and each layer is
tested at the cheapest level that still exercises real behaviour:

| Layer | Test style | What it proves | Test double usage |
|-------|-----------|----------------|-------------------|
| **Domain** (`DateRange`, `Reservation`, `Room`, `RoomType`, `Amenity`) | Pure unit tests, no infrastructure | Invariants and the half-open date-overlap rule | none |
| **Application services** (`RoomService`, `ReservationService`) | Service + **real EF Core against in-memory SQLite** | Use-case orchestration, validation, and that our LINQ (capacity filter, overlap predicate, ordering, owned-type mapping) actually translates to SQL and returns the right rows | `IClock` and `IBookingReferenceGenerator` are faked; the database is real |
| **API** | Full ASP.NET Core pipeline via `WebApplicationFactory` + in-memory SQLite | Routing, model binding, status codes, the error envelope, JSON serialization, persistence, and retrieval after creation | only the DB connection and the clock are swapped |
| **Concurrency** | Multiple threads / connections against a **file-backed** SQLite database | The `BEGIN IMMEDIATE` + authoritative re-check strategy holds one winner under contention | real generator, real DB |

Guiding principles applied:

* Test observable behaviour, not implementation details.
* Real SQLite rather than a hand-rolled fake `IStaySphereDbContext`: the overlap
  predicate and owned-type mapping only mean something once they are translated
  to SQL. A fake `IQueryable` would pass while hiding a translation bug.
* Fakes only at genuine boundaries (wall-clock time, random reference strings).
* Deterministic: a `FixedClock` removes "today" ambiguity; no `Thread.Sleep`;
  each test gets its own fresh database.
* No tests for framework behaviour or for EF Core itself.

### No production code was changed

The application was already testable (interfaces for the clock and reference
generator, no static state, `Program` is `public partial`). The only edit was to
`StaySphere.Tests.csproj` (test dependencies and project references). **No
production defects were found** — every requirement example, including the
booking-conflict cases spelled out in `CLAUDE.md`, is met by the current code.

---

## 2. Project layout

```
StaySphere.Tests/
├── Domain/            pure unit tests
├── Application/       service tests over real in-memory SQLite
├── Api/               WebApplicationFactory pipeline tests + middleware unit tests
├── Concurrency/       double-booking race test (file-backed SQLite)
└── TestSupport/
    ├── SqliteTestDatabase.cs   real DbContext over a private in-memory SQLite DB
    ├── StaySphereApiFactory.cs WebApplicationFactory: in-memory DB + fixed clock
    ├── FixedClock.cs           deterministic IClock
    ├── FakeBookingReferenceGenerator.cs  scriptable IBookingReferenceGenerator
    ├── ReservationSeeder.cs    inserts "existing" reservations via the domain factory
    ├── SeededCatalog.cs        well-known ids/prices from the model's seed data
    └── Build.cs                tiny domain-object factory for unit tests
```

Test doubles are deliberately minimal — no generic builder framework. The catalog
(`RoomType`, `Room`, `Amenity`) is the real seed data baked into the EF model, so
tests reference it through `SeededCatalog` instead of re-seeding it by hand.

---

## 3. Unit test scope (Domain)

`DateRangeTests` — the most thorough file, since the half-open interval is the
core rule:

* valid range constructs and reports `Nights`
* check-out **before** check-in → `BusinessRuleViolationException`
* check-out **equal to** check-in → `BusinessRuleViolationException`
* `OverlapsWith` across the full matrix (see §5)
* `OverlapsWith` is symmetric
* `OverlapsWith(null)` → `ArgumentNullException`
* value equality / hash code / `ToString`

`ReservationTests` — `Reservation.Create` guard behaviour:

* produces a `Confirmed`, active reservation and snapshots `TotalPrice`
  (`pricePerNight × nights`)
* trims guest name / email / special requests; blank special requests → `null`
* rejects guest count `< 1`, guest count `> room capacity`; allows `== capacity`
* rejects blank guest name / email / booking reference
* rejects null room / null stay; rejects a room whose `RoomType` navigation was
  not loaded
* `Cancel()` leaves the active state and is idempotent

`RoomTests`, `RoomTypeTests`, `AmenityTests`, `RoomTypeAmenityTests` — constructor
invariants (blank names, non-positive price / capacity, null relations), value
trimming, the placeholder-image fallback, and `RoomType.CalculatePrice`
(including `nights <= 0` → throw).

Getters/setters and the EF-only private constructors are not tested directly.

---

## 4. Integration test scope

### Application services (real in-memory SQLite)

`RoomServiceSearchTests`

* returns rooms when nothing is booked; empty when nothing matches
* **capacity**: excludes rooms whose `MaxGuests < guests`; includes the exact-fit
  room
* **availability**: excludes a room with an overlapping *confirmed* reservation;
  includes it when the overlap is only adjacent, when the overlapping reservation
  is *cancelled*, or when a *different* room of the same type is the one booked
* results ordered by price then room number; amenities projected alphabetically
* validation: missing check-in / check-out / guests, guests `< 1`, check-out not
  after check-in — each throws `ValidationException` with the offending field;
  a fully-empty query reports all three fields at once

`RoomServiceGetByIdTests` — returns full details for an existing room;
`NotFoundException` for an unknown id.

`ReservationServiceCreateTests` (24 tests)

* success returns a confirmation with reference, `Confirmed` status, nights,
  `pricePerNight`, `totalPrice`, amenities, trimmed special requests
* the reservation is persisted as `Confirmed` and is readable from a *separate*
  DbContext
* unique reference generation retries when the first candidate collides; throws
  if it cannot find a free reference
* `NotFoundException` for a missing room; `ValidationException` for
  non-positive `roomId`, missing/!after/past dates, guest count `< 1`, missing or
  too-short name, missing or malformed email, over-limit special requests; the
  boundary cases (`checkIn == today`, special requests exactly 1000 chars,
  `guestCount == capacity`) are allowed
* nothing is persisted when validation fails
* **capacity over the room limit → `ValidationException` on `guestCount`**
* **authoritative re-check**: an overlapping confirmed reservation →
  `RoomUnavailableException`; adjacent → success; a cancelled overlap → success
* **stale search result**: a room returned by an earlier search that is then
  booked by someone else still produces `RoomUnavailableException` on our create
* a rejected second attempt leaves exactly one reservation

`ReservationServiceConflictTests` — the conflict matrix as a single parameterized
test (see §6).

`ReservationServiceGetByReferenceTests` — round-trips a created booking (all
fields), `NotFoundException` for an unknown reference, `ValidationException` for a
blank reference, and documents that the lookup is case-sensitive.

### API (`WebApplicationFactory` + in-memory SQLite + fixed clock)

`RoomsEndpointsTests`

* `GET /api/rooms/search` — 200 with the ordered room list; camelCase JSON;
  excludes a room with an overlapping reservation; 400 (with the error envelope)
  for missing params, check-out not after check-in, `guests=0`, and an
  unparseable date
* `GET /api/rooms/{roomId}` — 200 for an existing room; 404 + `NotFound` envelope
  for an unknown id; 404 for a non-integer id (route constraint)

`ReservationsEndpointsTests`

* `POST /api/reservations` — 201 with a `Location` header and a confirmation body;
  camelCase JSON; the created reference is then retrievable via
  `GET /api/reservations/{reference}` with matching data
* 404 for a missing room; 400 (envelope + field errors) for a past check-in,
  guest count over capacity, invalid name/email, and a non-date `checkIn`
* **409 `BookingConflict`** when the room is already booked for overlapping
  dates; **two adjacent bookings both return 201**
* `GET /api/reservations/{reference}` — 404 + `NotFound` envelope for an unknown
  reference

`ExceptionHandlingMiddlewareTests` — the exception→status table directly:
`ValidationException`→400 (+ field errors), `NotFoundException`→404,
`RoomUnavailableException`→409 `BookingConflict`, generic `DomainException`→400
`BusinessRuleViolation`, cancelled request→499, anything else→500 **without
leaking the internal message**, and clean pass-through when nothing throws.

### Database behaviour

Covered implicitly by the Application and API layers running against real SQLite:
schema is created from the model (catalog seed applied), reservations persist with
the owned `DateRange` mapped to `CheckIn`/`CheckOut`, money round-trips through the
integer-cents converter, room→type→amenity relationships load, and the
availability query returns correct results. These assert *our* queries and
mapping, not EF Core itself.

---

## 5. Important availability test cases

Reference existing stay for the matrix: **2026-09-10 → 2026-09-13**.

| Requested range | Rule exercised | Expected |
|-----------------|----------------|----------|
| 2026-09-10 → 2026-09-13 | exact same range | **conflict** |
| 2026-09-08 → 2026-09-11 | overlap at the start | **conflict** |
| 2026-09-12 → 2026-09-15 | overlap at the end | **conflict** |
| 2026-09-08 → 2026-09-20 | requested contains existing | **conflict** |
| 2026-09-11 → 2026-09-12 | existing contains requested | **conflict** |
| 2026-09-05 → 2026-09-08 | entirely before, not touching | available |
| 2026-09-16 → 2026-09-18 | entirely after, not touching | available |
| 2026-09-08 → 2026-09-10 | adjacent — ends when existing starts | available |
| 2026-09-13 → 2026-09-15 | adjacent — starts when existing ends | available |

These run at three levels: `DateRange.OverlapsWith` (unit), `RoomService.SearchAsync`
(does the SQL exclude the room?), and `ReservationService.CreateAsync` (does the
re-check block the booking?).

Also asserted: a **cancelled** reservation never blocks availability; booking one
physical room does not affect other rooms of the same type.

---

## 6. Booking-conflict test cases

`ReservationServiceConflictTests.CreateReservation_HonoursHalfOpenOverlapRule`
seeds one confirmed reservation, then attempts a second create for the same
physical room and asserts `RoomUnavailableException` + "still only one
reservation" (conflict) or a `Confirmed` result + "two reservations now" (allowed):

| # | Existing | New | Expected |
|---|----------|-----|----------|
| 1 | 09-10 → 09-13 | 09-12 → 09-15 | conflict |
| 2 | 09-10 → 09-13 | 09-13 → 09-15 | allowed (adjacent) |
| 3 | 09-10 → 09-20 | 09-12 → 09-15 | conflict |
| 4 | 09-12 → 09-15 | 09-10 → 09-20 | conflict |
| 5 | 09-10 → 09-13 | 09-08 → 09-10 | allowed (adjacent) |
| 6 | 09-10 → 09-13 | 09-10 → 09-13 | conflict (exact) |
| 7 | 09-10 → 09-13 | 09-08 → 09-11 | conflict (overlap start) |
| 8 | 09-10 → 09-13 | 09-05 → 09-08 | allowed |
| 9 | 09-10 → 09-13 | 09-16 → 09-18 | allowed |

At the API level, `Create_Returns409BookingConflict_...` and
`Create_Allows_TwoBookingsForTheSameRoomOnAdjacentDates` prove the same rule
end-to-end through HTTP.

### Concurrency

`DoubleBookingConcurrencyTests` uses a **file-backed** SQLite database (each
thread gets its own connection) so the write lock is real:

* `...ConfirmsExactlyOne_WhenManyRequestsRaceForTheSameRoomAndDates` — 6 threads
  release from a `Barrier` and call `CreateAsync` for the same room + overlapping
  dates. Asserts exactly **one** success, five `RoomUnavailableException`, and
  exactly one confirmed row in the database afterwards.
* `...ConfirmsAll_WhenConcurrentRequestsBookTheSameRoomOnDisjointDates` — four
  non-overlapping stays for the same room booked concurrently all succeed
  (the lock serializes writers without wrongly rejecting valid bookings).

**This is a smoke test of the strategy, not a proof.** It is timing-dependent and
its result depends on SQLite's locking behaviour — see §8.

---

## 7. How to run the tests

From `Backend/`:

```bash
dotnet build StaySphere.slnx
dotnet test  StaySphere.slnx
```

Filter by area:

```bash
dotnet test --filter "FullyQualifiedName~Domain"
dotnet test --filter "FullyQualifiedName~Application"
dotnet test --filter "FullyQualifiedName~Api"
dotnet test --filter "FullyQualifiedName~Concurrency"
```

Coverage report (Cobertura XML under `StaySphere.Tests/TestResults/`):

```bash
dotnet test StaySphere.slnx --collect:"XPlat Code Coverage"
```

No local setup, external services, or developer database are required — every
test creates and tears down its own SQLite database.

### Current status

* **175 tests, all passing.** Suite runs in ~4 s; verified stable across repeated
  and back-to-back runs (no flakiness, no order dependence — each test owns its
  database, and the API factory clears reservations before every test).
* Coverage of the business core: **`StaySphere.Application` ≈ 97 % line / 100 %
  branch**, **`StaySphere.Domain` ≈ 96 % line / 96 % branch**,
  **`StaySphere.Api` ≈ 94 % line**. The uncovered remainder is intentional (see
  §8).

---

## 8. Known gaps and SQLite / concurrency limitations

**Intentionally not covered**

* `RoomService.SearchAsync`'s defensive `catch`/log/re-throw for an *unexpected*
  infrastructure exception — would require fault injection that the design does
  not expose, and it carries no business logic.
* `ReservationService`'s `DbUpdateException`-at-commit → `RoomUnavailableException`
  path. It is exercised opportunistically by the concurrency test but is not
  forced deterministically (again, no fault-injection seam).
* The `Response.HasStarted` guard in the error middleware (defensive; needs a
  half-written response to trigger).
* `JsonRoomCatalogSeeder`, `DatabaseInitializer`, `Program` startup wiring,
  `StaySphereDbContextFactory` (design-time), and EF migrations — infrastructure
  plumbing, not application behaviour. This is why `StaySphere.Infrastructure`
  branch coverage is low and that is expected.

**SQLite / concurrency limitations the tests cannot escape**

* SQLite has a **single writer**. `BEGIN IMMEDIATE` makes the booking critical
  section explicit and correct, but throughput is one write transaction at a
  time. The concurrency test confirms correctness under a small race; it does not
  characterise behaviour under sustained load.
* There is **no `SELECT … FOR UPDATE`**; the guard relies on the database-level
  write lock taken by `BEGIN IMMEDIATE` plus the busy timeout. Under heavy
  contention a loser could in principle time out with `SQLITE_BUSY` rather than a
  clean `409`; the suite does not attempt to provoke that.
* The concurrency test is inherently **timing-dependent**. It has been observed
  stable here, but a heavily loaded CI machine is a different environment. It
  asserts the invariant that matters (exactly one confirmed reservation) rather
  than exact failure counts of each type where possible.
* A production deployment would move the same EF model to
  PostgreSQL / SQL Server and replace `BEGIN IMMEDIATE` with a range/exclusion
  constraint or `SERIALIZABLE` + retry. The domain and application tests would
  carry over unchanged; only the concurrency test's backing store would differ.

**Behaviour documented rather than judged**

* Booking-reference lookup is case-sensitive (ordinal match). Captured by
  `GetByReference_IsCaseSensitive` so a future change is a deliberate one.
* The backend email check (`MailAddress`) is more permissive than the frontend
  regex (e.g. it accepts a dotless domain). Tests use only unambiguous invalid
  inputs.
