import type { ReservationConfirmation, Room, SearchCriteria } from "@/lib/types";

/** A realistic room payload as returned by GET /api/rooms/search and /api/rooms/{id}. */
export function makeRoom(overrides: Partial<Room> = {}): Room {
  return {
    roomId: 4,
    roomNumber: "201",
    roomTypeId: 2,
    roomType: "Deluxe King",
    description: "A spacious room with a king bed, seating area and city views.",
    pricePerNight: 159,
    maxGuests: 2,
    amenities: ["Air conditioning", "City view", "Flat-screen TV", "Free Wi-Fi"],
    imageUrl: "/images/rooms/deluxe-king.svg",
    ...overrides,
  };
}

export const searchCriteria: SearchCriteria = {
  checkIn: "2999-01-10",
  checkOut: "2999-01-13",
  guests: 2,
};

/** A realistic confirmation payload as returned by POST/GET /api/reservations. */
export function makeConfirmation(
  overrides: Partial<ReservationConfirmation> = {},
): ReservationConfirmation {
  return {
    bookingReference: "STAY-MJXR4R8V",
    guestName: "Jordan Blake",
    guestEmail: "jordan.blake@example.com",
    specialRequests: "Late check-in around 11pm",
    roomId: 4,
    roomNumber: "201",
    roomType: "Deluxe King",
    description: "A spacious room with a king bed, seating area and city views.",
    amenities: ["Air conditioning", "City view", "Flat-screen TV", "Free Wi-Fi"],
    imageUrl: "/images/rooms/deluxe-king.svg",
    checkIn: "2999-01-10",
    checkOut: "2999-01-13",
    nights: 3,
    guestCount: 2,
    pricePerNight: 159,
    totalPrice: 477,
    status: "Confirmed",
    createdAtUtc: "2999-01-01T12:00:00+00:00",
    ...overrides,
  };
}
