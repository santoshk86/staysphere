# StaySphere - Claude Code Project Instructions

## 1. Project Overview

**Project Name:** StaySphere

StaySphere is a hotel room booking web application that allows guests to:

1. Search for available hotel rooms.
2. View room details.
3. Submit a reservation.
4. Receive a booking confirmation with a booking reference.

The application should be designed as a maintainable full-stack solution that can evolve into a mobile application in the future.

The current scope is intentionally limited to the guest booking experience.

---

# 2. Primary Goals

The implementation must prioritize:

1. Correct business behavior.
2. Simple and maintainable architecture.
3. Clean object-oriented design.
4. SOLID principles where they provide real value.
5. Testability of business logic and application services.
6. Clear separation of responsibilities.
7. Extensibility without unnecessary abstraction.
8. Clean and responsive user experience.

Do NOT over-engineer the solution.

The goal is:

> Simple enough to understand and maintain, structured enough to extend and test.

---

# 3. Technology Stack

## Frontend

* Next.js
* App Router
* TypeScript
* Tailwind CSS

## Backend

* Latest stable .NET / ASP.NET Core Web API
* C#
* Entity Framework Core

## Database

* SQLite

## Testing

Testing is REQUIRED eventually, but tests will be implemented in a later phase.

Preferred testing stack:

* Backend: xUnit
* Frontend unit/component tests: Vitest + React Testing Library
* End-to-end: Playwright

IMPORTANT:

During the initial implementation phase, DO NOT spend significant effort writing unit,
integration, or E2E tests.

The code must be designed so that tests can be added later easily.

---

# 4. Initial Implementation Strategy

The implementation will happen in two major stages.

## Stage 1 - Functional Implementation

Complete the application end-to-end first.

Focus on:

* Domain model
* Business rules
* Database
* API
* Frontend
* Booking workflow
* Error handling
* Validation
* Availability logic
* Reservation creation
* Confirmation flow

During this stage:

* Do NOT create a large test suite.
* Do NOT implement comprehensive unit tests.
* Do NOT implement comprehensive integration tests.
* Do NOT implement Playwright tests.
* Do NOT stop implementation to write tests for every class.

However:

* Keep business logic isolated.
* Keep dependencies injectable where useful.
* Keep classes cohesive.
* Avoid static/global state.
* Keep APIs deterministic.
* Keep domain behavior independent from infrastructure.
* Make important business rules easy to test later.

A very small smoke/build verification is acceptable during development, but testing should not consume the majority of implementation effort.

## Stage 2 - Test Implementation

After the complete functional requirements are implemented and the application works end-to-end,
we will explicitly enter a dedicated testing phase.

At that time:

* Add backend unit tests.
* Add backend integration tests.
* Add frontend unit/component tests.
* Add Playwright E2E tests.
* Add regression tests.
* Improve coverage of important business rules.
* Fix any design problems exposed by testing.

Do NOT prematurely implement the complete test suite during Stage 1.

---

# 5. Functional Requirements

## Room Search

Guest provides:

* Check-in date
* Check-out date
* Guest count

The application returns only rooms that:

1. Are available for the entire requested date range.
2. Have capacity greater than or equal to the requested guest count.

Example:

```text
Room capacity = 2
Requested guests = 3

Result = Not eligible
```

---

# 6. Date Range Rules

Reservation dates use the following conceptual interval:

```text
[CheckIn, CheckOut)
```

Meaning:

* Check-in date is included.
* Check-out date is excluded.

Therefore:

```text
Existing:  Sep 10 → Sep 13
Requested: Sep 13 → Sep 15

Result: AVAILABLE
```

But:

```text
Existing:  Sep 10 → Sep 13
Requested: Sep 12 → Sep 15

Result: CONFLICT
```

The implementation must correctly handle:

* exact overlap
* partial overlap
* complete containment
* requested range containing existing reservation
* existing reservation containing requested range
* adjacent reservations
* same-day checkout/check-in
* invalid ranges

Do not scatter date-overlap logic throughout the codebase.

Prefer a well-defined domain abstraction/value object or a clearly isolated business rule.

---

# 7. Room Listing

Search results must expose:

* Room type
* Description where appropriate
* Price per night
* Maximum guests
* Amenities
* Image placeholder

Users must be able to select a room and navigate to its details.

---

# 8. Room Details

Room detail view should display:

* Room type
* Description
* Price
* Capacity
* Amenities
* Image placeholder
* Booking CTA

The booking CTA starts the reservation flow.

---

# 9. Booking

Guest enters:

* Guest name
* Guest email
* Special requests

No authentication is required.

The booking can be a single-step form or a multi-step flow.

Prefer the simplest clean UX unless a multi-step flow provides a clear benefit.

---

# 10. Reservation Creation

Reservation creation must:

1. Validate the request.
2. Validate check-in/check-out dates.
3. Validate guest count.
4. Load the requested room.
5. Validate room capacity.
6. Re-check room availability.
7. Create the reservation.
8. Generate a booking reference.
9. Persist the reservation.
10. Return confirmation information.

IMPORTANT:

Never trust availability returned by the previous room-search request.

Search results can become stale.

The final booking operation must perform an authoritative availability check.

---

# 11. Double Booking / Concurrency

Consider this scenario:

```text
Guest A searches → Room available

Guest B searches → Same room available

Guest A books

Guest B attempts to book
```

The backend must revalidate availability at reservation time.

Use a reasonable transactional/persistence strategy supported by SQLite and EF Core.

Do NOT introduce distributed locking, Redis, message brokers, or other infrastructure unless there is a demonstrated requirement.

The solution should document any limitations of SQLite concurrency behavior.

---

# 12. Confirmation

After a successful reservation, display:

* Booking reference
* Guest name
* Guest email
* Room information
* Check-in
* Check-out
* Guest count
* Price/reservation summary
* Special requests where appropriate

---

# 13. Architecture

Use a lightweight layered architecture.

```text
StaySphere
│
├── frontend
│   └── StaySphere.Web
│
└── backend
    │
    ├── StaySphere.Api
    ├── StaySphere.Application
    ├── StaySphere.Domain
    ├── StaySphere.Infrastructure
    └── StaySphere.Tests
```

Dependency direction:

```text
Api
 ↓
Application
 ↓
Domain

Infrastructure
 ↓
Application / Domain
```

The Domain layer must NOT depend on:

* ASP.NET Core
* EF Core
* SQLite
* Next.js
* HTTP
* UI concerns

---

# 14. API Layer

Controllers/endpoints should remain thin.

Controllers should handle:

* HTTP request/response
* model binding
* API-level validation concerns
* HTTP status codes

Controllers should NOT contain:

* reservation business rules
* date-overlap logic
* database queries
* complicated orchestration

Business logic belongs in Application/Domain.

---

# 15. Application Layer

Model application behavior around actual use cases.

Examples:

```text
SearchAvailableRooms
CreateReservation
GetReservation
```

Do not introduce CQRS/MediatR simply for the sake of architecture.

Use straightforward application services unless a real requirement justifies something more complex.

---

# 16. Domain Layer

Model the important business concepts:

```text
RoomType
Room
Amenity
Reservation
ReservationStatus
DateRange
```

Use OOP principles intentionally.

Prefer:

* encapsulated state
* meaningful methods
* constructors/factory methods where useful
* domain invariants
* cohesive responsibilities
* value objects where they simplify business rules

Avoid:

* anemic domain models when meaningful behavior naturally belongs in the domain
* excessive inheritance
* unnecessary interfaces
* unnecessary abstractions

Example concept:

```text
DateRange
    ├── validates range
    └── determines overlap
```

---

# 17. SOLID Principles

Apply SOLID pragmatically.

## Single Responsibility

Each class should have one clear reason to change.

Examples:

```text
Controller
    → HTTP concerns

RoomService
    → Room search use case

ReservationService
    → Reservation workflow

DateRange
    → Date-range behavior

DbContext
    → Persistence
```

## Open/Closed

Keep important business rules structured so additional rules can be added without rewriting unrelated functionality.

Do not introduce abstractions prematurely.

## Liskov Substitution

Use interfaces only when implementations are genuinely substitutable.

## Interface Segregation

Prefer small focused interfaces where abstraction provides value.

Avoid large "god interfaces."

## Dependency Inversion

Application logic should depend on abstractions where it improves testability or separation.

Do not create interfaces for every class.

---

# 18. Dependency Injection

Use ASP.NET Core dependency injection.

Good candidates may include:

```text
IRoomService
IReservationService
IBookingReferenceGenerator
IClock
```

Only introduce an interface when it serves a real purpose.

---

# 19. Persistence

Use:

```text
Entity Framework Core
        ↓
SQLite
```

Create:

* DbContext
* entity configurations
* migrations
* seed data
* useful indexes
* foreign keys
* appropriate relational constraints

Prefer EF Core's capabilities directly.

Do NOT create a generic:

```text
IRepository<T>
```

unless a demonstrated requirement makes it useful.

Do NOT create a Unit of Work abstraction merely to wrap EF Core's existing transaction/change tracking behavior.

---

# 20. Database Model

Minimum expected concepts:

```text
RoomType
Room
Amenity
RoomTypeAmenity
Reservation
```

A reservation should refer to an actual room/inventory unit.

Example:

```text
RoomType
    │
    ├── Room 101
    ├── Room 102
    └── Room 103
```

This allows availability to be determined correctly.

---

# 21. Backend Validation

Validate:

* check-in exists
* check-out exists
* check-out > check-in
* guest count > 0
* guest count does not exceed room capacity
* guest name
* guest email
* room existence
* room availability

Keep validation appropriate to the layer.

Do not duplicate identical business rules unnecessarily across multiple layers.

---

# 22. Error Handling

Provide consistent API error responses.

Handle at minimum:

```text
400 Bad Request
404 Not Found
409 Conflict
500 Internal Server Error
```

Use a centralized error-handling mechanism rather than repeating try/catch logic inside every controller.

A booking conflict should be distinguishable from a general server error.

---

# 23. Frontend Architecture

Use Next.js App Router.

Prefer:

* Server Components where they make sense.
* Client Components only where interactivity requires them.
* Local state for local UI concerns.
* Simple API client abstraction.
* Reusable components only when reuse is real.

Do not introduce Redux or another global state library unless a real requirement appears.

---

# 24. Frontend Pages

Expected pages:

```text
/
 /rooms
 /rooms/[roomId]
 /booking/[roomId]
 /booking/confirmation/[reference]
```

The final route names may be adjusted if the implementation has a better consistent structure.

---

# 25. Frontend States

The UI must properly handle:

* loading
* success
* empty results
* validation error
* API error
* unavailable room
* booking conflict
* submission state
* successful confirmation

Do not create fake frontend business rules that disagree with the backend.

The backend is authoritative for booking availability.

---

# 26. Frontend UX

The UI should be:

* clean
* responsive
* mobile-friendly
* accessible
* easy to understand
* visually consistent

The application should be designed mobile-first because a future mobile client is expected.

However, do not create a mobile application in the current scope.

---

# 27. Security

Authentication is NOT required.

Still follow basic secure development practices:

* validate all API input
* never trust client data
* do not expose sensitive configuration
* use environment variables for configuration
* do not commit secrets
* avoid unsafe string-based SQL
* rely on parameterized EF Core queries
* validate reservation requests server-side

---

# 28. Logging and Observability

Add useful structured logging for important backend operations.

At minimum consider:

* application startup
* search failures
* reservation attempts
* successful reservations
* booking conflicts
* unexpected exceptions

Do not add a full observability platform unless required.

---

# 29. Testing Strategy - FUTURE PHASE

IMPORTANT:

The complete test suite will be implemented AFTER the end-to-end functional implementation is complete.

During initial implementation, code must simply remain testable.

## Backend tests to add later

### Domain tests

Examples:

```text
DateRangeTests
ReservationTests
CapacityRuleTests
AvailabilityRuleTests
```

Test:

* valid date range
* invalid date range
* overlap
* non-overlap
* adjacent dates
* capacity validation
* reservation invariants

### Application tests

Later test:

```text
SearchAvailableRooms
CreateReservation
GetReservation
```

### Integration tests

Later test:

```text
HTTP
 ↓
API
 ↓
Application
 ↓
EF Core
 ↓
SQLite
```

Test:

* search
* booking
* conflict
* persistence
* validation
* API error behavior

---

# 30. Frontend Tests - FUTURE PHASE

After functional implementation is complete, add:

* Vitest
* React Testing Library
* Playwright

Unit/component tests should eventually cover:

## Search

* date inputs
* guest count
* validation
* valid search
* loading
* empty results
* API failure
* room results

## Room listing

* room type
* price
* capacity
* amenities
* navigation

## Room details

* room information
* amenities
* booking CTA
* loading/error behavior

## Booking form

* guest name
* email
* special requests
* validation
* invalid email
* submission
* loading
* booking conflict
* API failure
* duplicate-submission prevention

## Confirmation

* booking reference
* guest details
* room details
* dates
* guest count
* reservation summary

---

# 31. End-to-End Tests - FUTURE PHASE

After frontend and backend functionality are stable, add Playwright tests for:

```text
Search
  ↓
Results
  ↓
Room Details
  ↓
Booking
  ↓
Confirmation
```

Also test important failure flows:

```text
Invalid dates
No available rooms
Room becomes unavailable
Booking conflict
API failure
```

---

# 32. Test Efficiency Rule

During Stage 1, prioritize implementation over comprehensive testing.

DO NOT:

* write tests for every getter/setter
* write tests merely to increase coverage
* create mocks with no meaningful behavior
* build test infrastructure before business functionality exists
* spend large amounts of effort testing code that will likely change during implementation

DO:

* keep business rules isolated
* keep services small
* avoid static dependencies
* use dependency injection
* avoid hidden global state
* keep APIs deterministic
* structure code so later tests are straightforward

---

# 33. Definition of Done - Stage 1

A feature is considered functionally complete when:

* requirements are implemented
* business behavior is correct
* validation is implemented
* API works
* database persistence works
* frontend consumes the real API
* loading/error/empty states are handled
* booking flow works end-to-end
* booking confirmation works
* build succeeds
* lint/type-check succeeds where applicable
* no known blocking defects remain

Comprehensive automated testing is NOT required to declare Stage 1 complete.

Testing will be a separate Stage 2 effort.

---

# 34. Definition of Done - Stage 2

Stage 2 begins only after Stage 1 functionality is complete.

A feature is fully production-ready only after appropriate:

* backend unit tests
* backend integration tests
* frontend unit/component tests
* E2E tests
* regression tests

have been added and are passing.

---

# 35. Implementation Discipline

Before modifying the repository:

1. Inspect the existing structure.
2. Read relevant files.
3. Understand existing responsibilities.
4. Make the smallest appropriate change.
5. Avoid rewriting working code unnecessarily.

When implementing a feature:

1. Understand the requirement.
2. Identify affected layers.
3. Implement the minimal design.
4. Keep responsibilities separated.
5. Run build/type-check/lint as appropriate.
6. Fix implementation errors.
7. Update documentation when architecture or behavior changes.

---

# 36. Avoid Over-Engineering

Do NOT introduce these unless a real requirement appears:

```text
CQRS
MediatR
Event Sourcing
Microservices
Message brokers
Redis
Kubernetes
API Gateway
Distributed locking
Generic Repository
Generic Unit of Work
Domain event infrastructure
Authentication service
Identity server
Complex state management
Complex caching architecture
```

The application is intentionally a small full-stack system.

The architecture should demonstrate engineering judgment by remaining simple.

---

# 37. Future Mobile Application

The system should be structured so a future mobile application can consume the same backend API.

Future:

```text
StaySphere.Web
        │
        ├──────► StaySphere.Api
        │
StaySphere.Mobile
        │
        └──────► StaySphere.Api
```

Do NOT build the mobile application now.

Keep API contracts independent of the Next.js frontend.

---

# 38. Documentation

Maintain:

```text
docs/
├── architecture.md
├── api.md
├── database.md
├── decisions.md
└── progress.md
```

Documentation should remain lightweight.

Document important architectural decisions, not obvious implementation details.

---

# 39. Architectural Decision Rule

When considering a new abstraction, library, pattern, or infrastructure component, ask:

1. What real requirement does it solve?
2. Does it improve maintainability?
3. Does it improve testability?
4. Does it reduce complexity elsewhere?
5. Can the requirement be solved more simply?

Prefer the simplest design that satisfies the requirement.

---

# 40. Claude Code Working Rule

Before implementing a significant feature, briefly state:

* what will be changed
* which layers are affected
* any important business rule involved
* any architectural decision being made

Then implement it.

Do not spend significant token budget generating speculative architecture.

Do not implement future requirements before they are needed.

Do not add comprehensive tests during Stage 1.

When Stage 1 is complete, stop and wait for the explicit instruction to begin Stage 2 testing.

---

# 41. Final Engineering Principle

Optimize the StaySphere solution for:

```text
Correctness
    ↓
Simplicity
    ↓
Maintainability
    ↓
Testability
    ↓
Extensibility
```

Do not optimize for number of patterns, number of projects, or amount of code.

A small, well-designed solution is preferred over a large, over-engineered solution.