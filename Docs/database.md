# StaySphere Database

* Engine: **SQLite** via `Microsoft.EntityFrameworkCore.Sqlite` 10.
* Context: `StaySphereDbContext` (Infrastructure).
* Migrations: `Backend/StaySphere.Infrastructure/Persistence/Migrations`
  (`InitialCreate`).
* Connection string key: `ConnectionStrings:StaySphere`
  (default `Data Source=staysphere.db`, overridable via
  `ConnectionStrings__StaySphere`).
* On API startup `DatabaseInitializer` runs `Migrate()`, switches the database to
  **WAL** journal mode, and seeds sample reservations if none exist.

## Schema

### RoomTypes
| Column | Type | Notes |
|--------|------|-------|
| Id | INTEGER PK | autoincrement |
| Name | TEXT | required, unique, ≤ 120 |
| Description | TEXT | required, ≤ 2000 |
| PricePerNightCents | INTEGER | money stored as integer cents (see decisions.md) |
| MaxGuests | INTEGER | required |
| ImageUrl | TEXT | required, ≤ 500 |

### Rooms — physical inventory units
| Column | Type | Notes |
|--------|------|-------|
| Id | INTEGER PK | autoincrement |
| RoomNumber | TEXT | required, unique, ≤ 20 |
| RoomTypeId | INTEGER FK → RoomTypes.Id | `ON DELETE RESTRICT` |

### Amenities
| Column | Type | Notes |
|--------|------|-------|
| Id | INTEGER PK | autoincrement |
| Name | TEXT | required, unique, ≤ 100 |

### RoomTypeAmenities — join table
| Column | Type | Notes |
|--------|------|-------|
| RoomTypeId | INTEGER FK → RoomTypes.Id | composite PK, `ON DELETE CASCADE` |
| AmenityId | INTEGER FK → Amenities.Id | composite PK, `ON DELETE CASCADE` |

### Reservations
| Column | Type | Notes |
|--------|------|-------|
| Id | INTEGER PK | autoincrement (internal only) |
| BookingReference | TEXT | required, **unique**, public identifier |
| RoomId | INTEGER FK → Rooms.Id | `ON DELETE RESTRICT` |
| CheckIn | TEXT (date) | owned `DateRange.Start`, inclusive |
| CheckOut | TEXT (date) | owned `DateRange.End`, exclusive |
| GuestCount | INTEGER | required |
| GuestName | TEXT | required, ≤ 200 |
| GuestEmail | TEXT | required, ≤ 320 |
| SpecialRequests | TEXT | nullable, ≤ 1000 |
| Status | TEXT | `Confirmed` / `Cancelled` (enum stored as string) |
| TotalPriceCents | INTEGER | price snapshot at booking time |
| CreatedAtUtc | TEXT | `DateTimeOffset` |

## Indexes

| Index | Columns | Purpose |
|-------|---------|---------|
| `IX_RoomTypes_Name` (unique) | Name | catalog integrity |
| `IX_Amenities_Name` (unique) | Name | catalog integrity |
| `IX_Rooms_RoomNumber` (unique) | RoomNumber | inventory integrity |
| `IX_Rooms_RoomTypeId` | RoomTypeId | FK navigation |
| `IX_Reservations_BookingReference` (unique) | BookingReference | confirmation lookup + uniqueness |
| `IX_Reservations_RoomId_Status` | RoomId, Status | availability query |
| `IX_Reservations_CheckIn_CheckOut` | CheckIn, CheckOut | availability query date filter |

## Seed data

Seeded by migration (`HasData`):

* **4 room types**: Standard Queen (2p, $99), Deluxe King (2p, $159),
  Family Suite (4p, $249), Executive Suite (3p, $399).
* **8 rooms**: 101/102/103 (Standard), 201/202 (Deluxe), 301/302 (Family), 401 (Executive).
* **10 amenities** mapped to the room types.

Seeded at runtime by `DatabaseInitializer` (date-relative, only if no reservations exist):

* One confirmed reservation on the lowest-numbered room for `today+3 .. today+6`.
* One confirmed reservation on the first room of another type for `today+10 .. today+12`.

## Adding rooms from JSON at startup

`JsonRoomCatalogSeeder` runs on every startup, right after `Migrate()` and before
the sample reservations. It reads one or more JSON files listed in
`Seeding:RoomsFiles` (default `["Data/room-seed.json"]`, resolved relative to the
app's base directory; absolute paths are used as-is).

Each file may contain `amenities`, `roomTypes` and `rooms` arrays. Every record
carries an explicit `id`. **A record is inserted only when no row with that `id`
already exists** — existing rows are never updated or duplicated, so the file is
safe to keep across restarts and to extend over time. Records are also skipped
(with a warning) when the unique `Name` / `RoomNumber` is already taken by a
different id, or when a room references a missing room type.

This is intentionally *not* part of an EF migration: migrations must be
deterministic and must not depend on external files.

Example (`StaySphere.Api/Data/room-seed.json`):

```json
{
  "amenities": [ { "id": 11, "name": "Private pool" } ],
  "roomTypes": [
    { "id": 5, "name": "Penthouse Suite", "description": "…",
      "pricePerNight": 899.00, "maxGuests": 4,
      "imageUrl": "/images/rooms/penthouse-suite.svg",
      "amenityIds": [1, 2, 7, 11] }
  ],
  "rooms": [
    { "id": 9,  "roomNumber": "104", "roomTypeId": 1 },
    { "id": 12, "roomNumber": "601", "roomTypeId": 5 }
  ]
}
```

## Availability query

A room is **available** for `[checkIn, checkOut)` when it has no confirmed
reservation `r` such that `r.CheckIn < checkOut AND checkIn < r.CheckOut`.
This is expressed once (`DateRange.OverlapsWith`) and as a single EF `Where`
that translates to a SQL `NOT EXISTS`; rooms and reservations are never loaded
into memory for conflict detection.
