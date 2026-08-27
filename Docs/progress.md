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

## Stage 2 — Frontend  ✅ Functionally complete

Next.js App Router client consuming the endpoints in `api.md`. No backend changes
were made.

### Location & stack

`Frontend/staysphere-web/` — Next.js 16 (App Router, Turbopack), React 19,
TypeScript, Tailwind CSS v4. Package name must be lowercase, so the folder is
`staysphere-web` rather than the `StaySphere.Web` shown in `architecture.md`.

### Routes

| Route | Rendering | Purpose |
|-------|-----------|---------|
| `/` | Static | Landing page with the search form. |
| `/rooms?checkIn&checkOut&guests` | Dynamic (SSR) | Search results. The query string is the single source of truth for the search — shareable, no client store. Fetches `GET /api/rooms/search`. Handles invalid params, empty results, and API errors. Results stream in under a `<Suspense>` fallback so the search form stays interactive. |
| `/rooms/[roomId]?checkIn&checkOut&guests` | Dynamic (SSR) | Room details. Fetches `GET /api/rooms/{roomId}`; unknown room → `notFound()`. Carries the search dates through to the booking CTA. Shows a capacity heads-up if `guests > maxGuests`. |
| `/booking/[roomId]?checkIn&checkOut&guests` | Dynamic (SSR) | Booking screen. SSR loads the room + stay summary + price breakdown; the guest-detail form is a Client Component. Missing/invalid dates or over-capacity guests short-circuit to a guidance message. |
| `/booking/confirmation/[reference]` | Dynamic (SSR) | Confirmation. Fetches `GET /api/reservations/{reference}`; unknown reference → `notFound()`. |

### API integration

* All API access goes through `src/lib/api.ts` (typed functions
  `searchRooms` / `getRoom` / `createReservation` / `getReservation`, plus an
  `ApiError` class that carries the API's error envelope: `status`, `code`,
  `fieldErrors`, `traceId`). No component calls `fetch` directly.
* Base URL comes from `NEXT_PUBLIC_API_BASE_URL` (see `.env.example`), read once
  in `src/lib/config.ts`. Default `http://localhost:5276`. The browser calls the
  API directly; the backend already allows `http://localhost:3000` via CORS, so
  the dev server must run on port 3000.
* All GETs use `cache: "no-store"` — availability and reservations are volatile.
* Types in `src/lib/types.ts` mirror the contracts in `api.md` exactly
  (camelCase field names).

### UI decisions

* **Backend is authoritative.** `src/lib/validation.ts` does *shape-only* checks
  (required fields, email format, check-out after check-in, guest count ≥ 1) for
  fast feedback. Every business rule — real availability, capacity, date
  conflicts, past-date rejection — is enforced by the API and its response
  drives the UI. Frontend capacity checks only mirror `maxGuests` from the room
  payload; they never contradict the backend.
* **409 conflict** on booking shows a dedicated "this room was just booked"
  message with a direct link back to search for the same dates. Handled in
  `BookingForm` alongside 400 (field errors mapped back onto the form), 404, 5xx
  and network failure.
* **Duplicate-submit prevention:** the booking form ignores re-entry while a
  request is in flight or after success, disables the submit button, and sets
  `aria-busy`.
* **Server vs Client Components:** pages are Server Components that fetch data;
  only `SearchForm` and `BookingForm` are Client Components (they need form
  state and navigation). No Redux / global state library.
* **Image placeholder:** the API's `imageUrl` points at backend static SVGs; per
  the brief the frontend renders a consistent decorative placeholder
  (`RoomImage`) instead of loading a real photo.
* **Accessibility:** labelled controls with `aria-describedby`/`aria-invalid`
  wiring via `FormField`, `role="alert"` on errors, focus moved to the error
  summary on a failed booking, visible `:focus-visible` outline, semantic
  landmarks, keyboard-operable throughout.
* Single light theme (no half-finished dark mode) for visual consistency;
  mobile-first Tailwind layout.

### Verified manually (API on :5276, prod build on :3000)

Search (valid / invalid range / invalid guests / empty / no query) · results
list · room details · unknown room → not-found · over-capacity guidance ·
booking form (valid / missing dates / over capacity) · create reservation →
201 → confirmation page renders reference, guest, dates, price, special
requests · overlapping rebooking → 409 · invalid email/name → 400 field errors.
`tsc --noEmit`, `eslint`, and `next build` all pass clean; no console/hydration
warnings.

### Known limitations

* **Currency:** the API returns bare numbers with no currency code; the UI
  formats them as USD (`src/lib/format.ts`).
* **Not-found HTTP status:** `notFound()` on the dynamic detail/confirmation
  routes renders the correct "Page not found" UI but with a `200` status,
  because Next streams the layout shell before the page segment resolves.
  Unknown top-level paths still return a real `404`.
* **No retry/backoff** on transient API failures — the user is shown an error
  and retries manually.
* Tests (Vitest / RTL / Playwright) are deliberately deferred to the dedicated
  testing phase; components are structured for it (isolated validation, isolated
  API layer, no hidden global state).
