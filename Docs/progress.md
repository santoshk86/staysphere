# Progress

## Stage 1 — Backend  ✅ Complete

| Area | Status |
|------|--------|
| Solution structure (5 projects, dependency direction) | ✅ |
| Domain model (`RoomType`, `Room`, `Amenity`, `RoomTypeAmenity`, `Reservation`, `ReservationStatus`, `DateRange`) | ✅ |
| Domain invariants (range validity, capacity, pricing, reference required) | ✅ |
| EF Core `StaySphereDbContext` + entity configurations | ✅ |
| `InitialCreate` migration | ✅ |
| Seed data (catalog via `HasData`, sample reservations at runtime) | ✅ |
| Indexes / FKs / constraints | ✅ |
| Application services: `SearchAvailableRooms`, `GetRoomDetails`, `CreateReservation`, `GetReservation` | ✅ |
| Availability resolved in SQL (`NOT EXISTS`, half-open overlap) | ✅ |
| Backend validation (dates, capacity, name, email, existence) | ✅ |
| Double-booking safety (`BEGIN IMMEDIATE` + authoritative re-check) | ✅ |
| Human-friendly unique booking reference | ✅ |
| REST endpoints + explicit request/response DTOs | ✅ |
| Centralized error handling (400/404/409/500 envelope) | ✅ |
| Structured logging (startup, attempts, successes, conflicts, failures) | ✅ |
| Configuration (connection string, CORS, logging, env overrides) | ✅ |
| Swagger UI (Development) | ✅ |
| Build + smoke verification | ✅ |
| Documentation (architecture, api, database, decisions, progress) | ✅ |

### Smoke verification performed

`dotnet build` (0 warnings), `dotnet test` (8/8 pass), and manual `curl` against a
running instance:

* search by dates + guest count → filtered results
* capacity filter (guests=4 → only 4-person rooms)
* availability filter (overlapping seeded reservation excluded)
* adjacent range allowed (half-open interval)
* invalid range / missing params → 400
* room details 200 / unknown room 404
* create reservation → 201 + reference; confirmation retrieval → 200
* conflicting reservation → 409; adjacent reservation → 201
* capacity / invalid email / past date → 400; unknown room → 404
* **6 concurrent identical bookings → exactly one 201, five 409**

## Stage 2 — Tests  ⏳ Not started

Backend unit + integration tests, frontend, and E2E are Stage 2 and begin only on
explicit instruction.

## Stage 2 — Frontend  ⏳ Not started

Next.js App Router client consuming the endpoints in `api.md`. No backend changes
expected.
