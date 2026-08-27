/**
 * Types mirroring the StaySphere API contracts (see `Docs/api.md`).
 * Field names match the API's camelCase JSON exactly.
 */

/** A room as returned by `GET /api/rooms/search` and `GET /api/rooms/{roomId}`. */
export interface Room {
  roomId: number;
  roomNumber: string;
  roomTypeId: number;
  roomType: string;
  description: string;
  pricePerNight: number;
  maxGuests: number;
  amenities: string[];
  imageUrl: string;
}

/** Payload for `POST /api/reservations`. */
export interface CreateReservationInput {
  roomId: number;
  checkIn: string;
  checkOut: string;
  guestCount: number;
  guestName: string;
  guestEmail: string;
  specialRequests?: string;
}

/** Response of `GET /api/reservations/{reference}` and `POST /api/reservations`. */
export interface ReservationConfirmation {
  bookingReference: string;
  guestName: string;
  guestEmail: string;
  specialRequests: string | null;
  roomId: number;
  roomNumber: string;
  roomType: string;
  description: string;
  amenities: string[];
  imageUrl: string;
  checkIn: string;
  checkOut: string;
  nights: number;
  guestCount: number;
  pricePerNight: number;
  totalPrice: number;
  status: string;
  createdAtUtc: string;
}

/** The API's error envelope (every non-2xx response). */
export interface ApiErrorBody {
  status: number;
  error: string;
  message: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}

/** Validated inputs for a room search. */
export interface SearchCriteria {
  checkIn: string;
  checkOut: string;
  guests: number;
}
