# StaySphere — Architecture (Backend, Stage 1)

## Overview

StaySphere is a small full-stack hotel booking system. Stage 1 delivers the
backend: an ASP.NET Core Web API over EF Core / SQLite that exposes stable REST
contracts for room search, room details, reservation creation and confirmation
retrieval. The Next.js frontend is Stage 2 and is not part of this deliverable.

## Solution layout

```
Backend/
├── StaySphere.Api            ASP.NET Core Web API (controllers, middleware, DI composition)
├── StaySphere.Application     Use cases, DTOs, service interfaces, validation
├── StaySphere.Domain          Entities, value objects, business invariants (no framework deps)
├── StaySphere.Infrastructure  EF Core DbContext, entity configs, migrations, seeding, adapters
└── StaySphere.Tests           xUnit project (smoke coverage only in Stage 1)
```

### Dependency direction

```
Api ──▶ Application ──▶ Domain
Api ──▶ Infrastructure ──▶ Application, Domain
```

* **Domain** has no dependency on ASP.NET Core, EF Core, SQLite or HTTP. It only
  references the BCL.
* **Application** depends on Domain and on `Microsoft.EntityFrameworkCore` for the
  `IStaySphereDbContext` abstraction (DbSet + `SaveChangesAsync` + transaction).
  It does not depend on the SQLite provider or on Infrastructure.
* **Infrastructure** implements the Application abstractions (`IStaySphereDbContext`,
  `IClock`, `IBookingReferenceGenerator`) and owns all EF Core mapping.
* **Api** is the composition root: it wires DI, hosts controllers, and runs
  migrations + seeding on startup.

## Layer responsibilities

| Layer | Responsibilities | Does NOT contain |
|-------|------------------|------------------|
| Api | HTTP binding, status codes, Swagger, centralized error handling, DI wiring | business rules, queries |
| Application | use-case orchestration, request validation, DTO mapping, availability query | HTTP, EF provider details |
| Domain | invariants, `DateRange` overlap rule, capacity rule, pricing, reservation lifecycle | persistence, framework code |
| Infrastructure | `DbContext`, entity configs, migrations, seed, SQLite transaction strategy | use-case logic |

## Key flows

### Room search — `GET /api/rooms/search`
`RoomsController` → `RoomService.SearchAsync` → single EF query that filters by
capacity and excludes rooms with an overlapping confirmed reservation (the
overlap predicate is translated to SQL — nothing is filtered in memory) →
`RoomDto` list.

### Reservation creation — `POST /api/reservations`
`ReservationsController` → `ReservationService.CreateAsync`:

1. Validate the request (dates, guest count, name, email).
2. Load the room + its type + amenities.
3. Check capacity against `RoomType.MaxGuests`.
4. `BEGIN IMMEDIATE` transaction (serializes concurrent bookings — see `decisions.md`).
5. Authoritative availability re-check for the specific room.
6. `Reservation.Create(...)` enforces domain invariants and snapshots the price.
7. Generate a unique booking reference, persist, commit.
8. Return `ReservationConfirmation`.

### Error handling
`ExceptionHandlingMiddleware` maps exception types to a single `ApiErrorResponse`
envelope: `ValidationException` → 400, `NotFoundException` → 404,
`RoomUnavailableException` → 409, other `DomainException` → 400, anything else → 500.

## Testability notes (Stage 2 enablement)

* All business rules live in Domain or Application services — not in controllers
  or EF configuration.
* Services depend on interfaces (`IStaySphereDbContext`, `IClock`,
  `IBookingReferenceGenerator`), so they can be unit-tested with fakes or an
  in-memory / SQLite-in-memory context.
* No static or global mutable state; time is injected via `IClock`.
* `Program` is `public partial` so a future `WebApplicationFactory` integration
  test host can reference it.
