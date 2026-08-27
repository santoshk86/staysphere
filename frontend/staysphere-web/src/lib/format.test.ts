import { describe, expect, it } from "vitest";
import {
  formatCurrency,
  formatDate,
  nightsBetween,
  parseCalendarDate,
  roomInitials,
} from "./format";

describe("formatCurrency", () => {
  it("formats whole and fractional dollar amounts as USD", () => {
    expect(formatCurrency(99)).toBe("$99.00");
    expect(formatCurrency(1234.5)).toBe("$1,234.50");
    expect(formatCurrency(0)).toBe("$0.00");
  });
});

describe("parseCalendarDate", () => {
  it("parses a yyyy-MM-dd string to a local date with no time shift", () => {
    const date = parseCalendarDate("2026-10-06");
    expect(date).not.toBeNull();
    expect(date!.getFullYear()).toBe(2026);
    expect(date!.getMonth()).toBe(9); // October (0-indexed)
    expect(date!.getDate()).toBe(6);
  });

  it("returns null when the shape is not yyyy-MM-dd", () => {
    expect(parseCalendarDate("not-a-date")).toBeNull();
    expect(parseCalendarDate("2026/10/06")).toBeNull();
    expect(parseCalendarDate("2026-1-1")).toBeNull();
    expect(parseCalendarDate("")).toBeNull();
  });

  it("normalizes out-of-range components the way the Date constructor does", () => {
    // Documents current behaviour: the parser validates shape, not calendar range,
    // so month 13 / day 40 roll forward rather than being rejected. Callers only
    // ever pass API- or DATE_RE-validated strings, so this is not user-facing.
    const rolled = parseCalendarDate("2026-13-40");
    expect(rolled).toBeInstanceOf(Date);
  });
});

describe("formatDate", () => {
  it("renders a friendly weekday/month/day/year label", () => {
    expect(formatDate("2026-10-06")).toBe("Tue, Oct 6, 2026");
  });

  it("falls back to the raw string when it cannot be parsed", () => {
    expect(formatDate("tomorrow")).toBe("tomorrow");
  });
});

describe("nightsBetween", () => {
  it("counts whole nights between two calendar dates", () => {
    expect(nightsBetween("2026-10-06", "2026-10-09")).toBe(3);
    expect(nightsBetween("2026-10-06", "2026-10-07")).toBe(1);
  });

  it("returns null when the range is zero-length, inverted, or invalid", () => {
    expect(nightsBetween("2026-10-06", "2026-10-06")).toBeNull();
    expect(nightsBetween("2026-10-09", "2026-10-06")).toBeNull();
    expect(nightsBetween("bad", "2026-10-09")).toBeNull();
  });
});

describe("roomInitials", () => {
  it("takes the first letter of the first two words, uppercased", () => {
    expect(roomInitials("Deluxe King")).toBe("DK");
    expect(roomInitials("standard queen room")).toBe("SQ");
    expect(roomInitials("Penthouse")).toBe("P");
  });
});
