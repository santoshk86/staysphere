import { describe, expect, it } from "vitest";
import { MAX_GUESTS, todayIso, validateBooking, validateSearch } from "./validation";

const FUTURE_IN = "2999-01-10";
const FUTURE_OUT = "2999-01-13";

describe("validateSearch", () => {
  it("accepts a valid future range and returns a numeric guest count", () => {
    const result = validateSearch({ checkIn: FUTURE_IN, checkOut: FUTURE_OUT, guests: "2" });

    expect(result).toEqual({
      ok: true,
      criteria: { checkIn: FUTURE_IN, checkOut: FUTURE_OUT, guests: 2 },
    });
  });

  it("requires check-in and check-out", () => {
    const result = validateSearch({ checkIn: "", checkOut: "", guests: "2" });

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.errors.checkIn).toMatch(/check-in/i);
    expect(result.errors.checkOut).toMatch(/check-out/i);
  });

  it("rejects a malformed date", () => {
    const result = validateSearch({ checkIn: "10-01-2999", checkOut: FUTURE_OUT, guests: "2" });

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.errors.checkIn).toMatch(/valid date/i);
  });

  it("rejects a past check-in by default", () => {
    const result = validateSearch({ checkIn: "2000-01-01", checkOut: FUTURE_OUT, guests: "2" });

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.errors.checkIn).toMatch(/past/i);
  });

  it("allows a past check-in when allowPastCheckIn is set (shared links)", () => {
    const result = validateSearch(
      { checkIn: "2000-01-01", checkOut: "2000-01-05", guests: "2" },
      { allowPastCheckIn: true },
    );

    expect(result.ok).toBe(true);
  });

  it("rejects check-out on or before check-in", () => {
    for (const checkOut of [FUTURE_IN, "2998-12-31"]) {
      const result = validateSearch({ checkIn: FUTURE_IN, checkOut, guests: "2" });
      expect(result.ok).toBe(false);
      if (result.ok) return;
      expect(result.errors.checkOut).toMatch(/after check-in/i);
    }
  });

  it("requires a guest count", () => {
    const result = validateSearch({ checkIn: FUTURE_IN, checkOut: FUTURE_OUT, guests: "" });

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.errors.guests).toMatch(/number of guests/i);
  });

  it.each(["0", "-2", "1.5"])("rejects an invalid guest count %j", (guests) => {
    const result = validateSearch({ checkIn: FUTURE_IN, checkOut: FUTURE_OUT, guests });

    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.errors.guests).toMatch(/at least one guest/i);
  });

  it("rejects a guest count above the maximum but accepts the maximum", () => {
    const tooMany = validateSearch({
      checkIn: FUTURE_IN,
      checkOut: FUTURE_OUT,
      guests: String(MAX_GUESTS + 1),
    });
    expect(tooMany.ok).toBe(false);
    if (!tooMany.ok) expect(tooMany.errors.guests).toMatch(/maximum/i);

    const atMax = validateSearch({
      checkIn: FUTURE_IN,
      checkOut: FUTURE_OUT,
      guests: String(MAX_GUESTS),
    });
    expect(atMax.ok).toBe(true);
  });

  it("trims surrounding whitespace from inputs", () => {
    const result = validateSearch({
      checkIn: `  ${FUTURE_IN}  `,
      checkOut: `  ${FUTURE_OUT}  `,
      guests: "  2  ",
    });

    expect(result).toEqual({
      ok: true,
      criteria: { checkIn: FUTURE_IN, checkOut: FUTURE_OUT, guests: 2 },
    });
  });

  it("exposes today's date as an ISO string", () => {
    expect(todayIso()).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });
});

describe("validateBooking", () => {
  it("accepts valid guest details and drops empty special requests", () => {
    const result = validateBooking({
      guestName: "  Jordan Blake  ",
      guestEmail: "  jordan@example.com  ",
      specialRequests: "   ",
    });

    expect(result).toEqual({
      ok: true,
      value: { guestName: "Jordan Blake", guestEmail: "jordan@example.com", specialRequests: undefined },
    });
  });

  it("keeps special requests when provided", () => {
    const result = validateBooking({
      guestName: "Jordan Blake",
      guestEmail: "jordan@example.com",
      specialRequests: "  Late check-in  ",
    });

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.value.specialRequests).toBe("Late check-in");
  });

  it("requires a guest name of at least two characters", () => {
    expect(validateBooking({ guestName: "", guestEmail: "a@b.com" })).toMatchObject({
      ok: false,
      errors: { guestName: expect.stringMatching(/enter the guest name/i) },
    });
    expect(validateBooking({ guestName: "A", guestEmail: "a@b.com" })).toMatchObject({
      ok: false,
      errors: { guestName: expect.stringMatching(/at least 2 characters/i) },
    });
  });

  it.each(["", "jordan", "jordan@example", "jordan @example.com"])(
    "rejects the invalid email %j",
    (guestEmail) => {
      const result = validateBooking({ guestName: "Jordan Blake", guestEmail });
      expect(result.ok).toBe(false);
      if (result.ok) return;
      expect(result.errors.guestEmail).toBeDefined();
    },
  );

  it("rejects special requests longer than 1000 characters but accepts exactly 1000", () => {
    const over = validateBooking({
      guestName: "Jordan Blake",
      guestEmail: "jordan@example.com",
      specialRequests: "x".repeat(1001),
    });
    expect(over.ok).toBe(false);
    if (!over.ok) expect(over.errors.specialRequests).toMatch(/1000 characters or fewer/i);

    const atLimit = validateBooking({
      guestName: "Jordan Blake",
      guestEmail: "jordan@example.com",
      specialRequests: "x".repeat(1000),
    });
    expect(atLimit.ok).toBe(true);
  });
});
