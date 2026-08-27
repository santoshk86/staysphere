/** Presentation helpers. Pure functions, no React, easy to unit-test later. */

const currencyFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
});

/** Format a number as USD. The API does not carry a currency code (see progress.md). */
export function formatCurrency(amount: number): string {
  return currencyFormatter.format(amount);
}

/** Parse a `yyyy-MM-dd` calendar date into a local `Date` (no timezone shift). */
export function parseCalendarDate(iso: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
  if (!match) return null;
  const [, y, m, d] = match;
  const date = new Date(Number(y), Number(m) - 1, Number(d));
  return Number.isNaN(date.getTime()) ? null : date;
}

/** e.g. "Mon, Oct 6, 2026". Falls back to the raw string if unparseable. */
export function formatDate(iso: string): string {
  const date = parseCalendarDate(iso);
  if (!date) return iso;
  return date.toLocaleDateString("en-US", {
    weekday: "short",
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

/** Whole nights between two `yyyy-MM-dd` dates, or null if either is invalid. */
export function nightsBetween(checkIn: string, checkOut: string): number | null {
  const start = parseCalendarDate(checkIn);
  const end = parseCalendarDate(checkOut);
  if (!start || !end) return null;
  const ms = end.getTime() - start.getTime();
  const nights = Math.round(ms / 86_400_000);
  return nights > 0 ? nights : null;
}

/** Initials for the image placeholder, e.g. "Deluxe King" -> "DK". */
export function roomInitials(roomType: string): string {
  return roomType
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0]?.toUpperCase() ?? "")
    .join("");
}
