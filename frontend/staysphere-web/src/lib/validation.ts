import type { SearchCriteria } from "./types";

/**
 * Client-side input validation. This checks *shape* only (required fields,
 * obvious format errors) so the UI can give fast feedback. The backend remains
 * authoritative for every business rule: availability, capacity, date conflicts,
 * and past-date rejection at booking time.
 */

export const MAX_GUESTS = 10;

// Pragmatic email shape check; the backend does the authoritative validation.
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const DATE_RE = /^\d{4}-\d{2}-\d{2}$/;

/** Local "today" as `yyyy-MM-dd`. */
export function todayIso(): string {
  const now = new Date();
  const y = now.getFullYear();
  const m = String(now.getMonth() + 1).padStart(2, "0");
  const d = String(now.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

export interface RawSearchInput {
  checkIn?: string | null;
  checkOut?: string | null;
  guests?: string | null;
}

export type SearchValidation =
  | { ok: true; criteria: SearchCriteria }
  | { ok: false; errors: Record<string, string> };

/**
 * Validate search inputs coming from a form or the URL query string.
 * `allowPastCheckIn` is true for URL parsing (don't hard-fail a shared link)
 * and false for the form (guide the guest to a bookable date).
 */
export function validateSearch(
  input: RawSearchInput,
  { allowPastCheckIn = false }: { allowPastCheckIn?: boolean } = {},
): SearchValidation {
  const errors: Record<string, string> = {};
  const checkIn = (input.checkIn ?? "").trim();
  const checkOut = (input.checkOut ?? "").trim();
  const guestsRaw = (input.guests ?? "").trim();

  if (!checkIn) {
    errors.checkIn = "Choose a check-in date.";
  } else if (!DATE_RE.test(checkIn)) {
    errors.checkIn = "Enter a valid date.";
  } else if (!allowPastCheckIn && checkIn < todayIso()) {
    errors.checkIn = "Check-in cannot be in the past.";
  }

  if (!checkOut) {
    errors.checkOut = "Choose a check-out date.";
  } else if (!DATE_RE.test(checkOut)) {
    errors.checkOut = "Enter a valid date.";
  }

  if (!errors.checkIn && !errors.checkOut && checkOut <= checkIn) {
    errors.checkOut = "Check-out must be after check-in.";
  }

  const guests = Number(guestsRaw);
  if (!guestsRaw) {
    errors.guests = "Enter the number of guests.";
  } else if (!Number.isInteger(guests) || guests < 1) {
    errors.guests = "At least one guest is required.";
  } else if (guests > MAX_GUESTS) {
    errors.guests = `Maximum ${MAX_GUESTS} guests.`;
  }

  if (Object.keys(errors).length > 0) {
    return { ok: false, errors };
  }
  return { ok: true, criteria: { checkIn, checkOut, guests } };
}

export interface RawBookingInput {
  guestName?: string;
  guestEmail?: string;
  specialRequests?: string;
}

export type BookingValidation =
  | { ok: true; value: { guestName: string; guestEmail: string; specialRequests?: string } }
  | { ok: false; errors: Record<string, string> };

/** Validate the booking form's guest-detail fields. */
export function validateBooking(input: RawBookingInput): BookingValidation {
  const errors: Record<string, string> = {};
  const guestName = (input.guestName ?? "").trim();
  const guestEmail = (input.guestEmail ?? "").trim();
  const specialRequests = (input.specialRequests ?? "").trim();

  if (!guestName) {
    errors.guestName = "Enter the guest name.";
  } else if (guestName.length < 2) {
    errors.guestName = "Name must be at least 2 characters.";
  }

  if (!guestEmail) {
    errors.guestEmail = "Enter an email address.";
  } else if (!EMAIL_RE.test(guestEmail)) {
    errors.guestEmail = "Enter a valid email address.";
  }

  if (specialRequests.length > 1000) {
    errors.specialRequests = "Special requests must be 1000 characters or fewer.";
  }

  if (Object.keys(errors).length > 0) {
    return { ok: false, errors };
  }
  return {
    ok: true,
    value: {
      guestName,
      guestEmail,
      specialRequests: specialRequests || undefined,
    },
  };
}
