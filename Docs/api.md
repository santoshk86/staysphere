# StaySphere API Contracts (v1)

Base URL (dev): `http://localhost:5276`
Content type: `application/json`. All dates are `yyyy-MM-dd` (calendar dates, no time zone).
Interactive docs: `GET /swagger` (Development only).

Reservation date semantics: the stay interval is **half-open** `[checkIn, checkOut)`.
A stay that checks out on day *D* does not conflict with one that checks in on day *D*.

---

## Error envelope

Every non-2xx response uses this shape:

```json
{
  "status": 409,
  "error": "BookingConflict",
  "message": "The selected room is no longer available for the requested dates.",
  "errors": { "field": ["message", "..."] },
  "traceId": "0HN..."
}
```

`errors` is populated only for validation failures (`error: "ValidationFailed"`).

| `error` value | HTTP | Meaning |
|---------------|------|---------|
| `ValidationFailed` | 400 | Request failed validation; see `errors`. |
| `BusinessRuleViolation` | 400 | A domain invariant was violated. |
| `NotFound` | 404 | Room or reservation does not exist. |
| `BookingConflict` | 409 | Room is no longer available for the requested dates. |
| `ServerError` | 500 | Unexpected failure. |

---

## GET /api/rooms/search

Search physical rooms available for the **entire** requested range with capacity
for the guest count.

Query parameters (all required):

| Param | Type | Rules |
|-------|------|-------|
| `checkIn` | date | required |
| `checkOut` | date | required, must be after `checkIn` |
| `guests` | int | required, ≥ 1 |

**200 OK** — array (possibly empty), ordered by price then room number:

```json
[
  {
    "roomId": 2,
    "roomNumber": "102",
    "roomTypeId": 1,
    "roomType": "Standard Queen",
    "description": "A comfortable room with a queen bed, ...",
    "pricePerNight": 99.00,
    "maxGuests": 2,
    "amenities": ["Air conditioning", "Flat-screen TV", "Free Wi-Fi"],
    "imageUrl": "/images/rooms/standard-queen.svg"
  }
]
```

**400** — missing/invalid parameters.

> Search returns one entry per available physical room. A client that wants a
> per-category listing can group by `roomTypeId`.

---

## GET /api/rooms/{roomId}

Full details for a single physical room.

* **200 OK** — a single object with the same shape as a search result.
* **404** — room not found.

---

## POST /api/reservations

Create a reservation. Availability is **re-checked authoritatively** here; the
result of a prior search is never trusted.

Request body:

```json
{
  "roomId": 4,
  "checkIn": "2026-10-06",
  "checkOut": "2026-10-09",
  "guestCount": 2,
  "guestName": "Jordan Blake",
  "guestEmail": "jordan.blake@example.com",
  "specialRequests": "Late check-in around 11pm"
}
```

Validation:

| Field | Rules |
|-------|-------|
| `roomId` | required, must exist |
| `checkIn` | required, not in the past |
| `checkOut` | required, after `checkIn` |
| `guestCount` | ≥ 1 and ≤ room capacity |
| `guestName` | required, ≥ 2 characters |
| `guestEmail` | required, valid email |
| `specialRequests` | optional, ≤ 1000 characters |

Responses:

* **201 Created** — `Location` header points at the confirmation resource; body is
  the `ReservationConfirmation` (see below).
* **400** — validation failure.
* **404** — room not found.
* **409** — room not available for the requested dates.

---

## GET /api/reservations/{reference}

Retrieve a booking confirmation by its public reference (e.g. `STAY-MJXR4R8V`).

* **200 OK**:

```json
{
  "bookingReference": "STAY-MJXR4R8V",
  "guestName": "Jordan Blake",
  "guestEmail": "jordan.blake@example.com",
  "specialRequests": "Late check-in around 11pm",
  "roomId": 4,
  "roomNumber": "201",
  "roomType": "Deluxe King",
  "description": "A spacious room with a king bed, ...",
  "amenities": ["Air conditioning", "City view", "Coffee machine", "Flat-screen TV", "Free Wi-Fi", "Mini bar"],
  "imageUrl": "/images/rooms/deluxe-king.svg",
  "checkIn": "2026-10-06",
  "checkOut": "2026-10-09",
  "nights": 3,
  "guestCount": 2,
  "pricePerNight": 159.00,
  "totalPrice": 477.00,
  "status": "Confirmed",
  "createdAtUtc": "2026-08-27T17:44:09.14+00:00"
}
```

* **404** — reservation not found.

---

## CORS

Allowed origins come from `Cors:AllowedOrigins` in configuration (default
`http://localhost:3000` for the Stage 2 Next.js dev server).
