import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  ApiError,
  createReservation,
  firstFieldErrors,
  getReservation,
  getRoom,
  searchRooms,
} from "./api";

type Body = Record<string, unknown> | unknown[] | string | undefined;

function fakeResponse(status: number, body: Body) {
  const text =
    body === undefined ? "" : typeof body === "string" ? body : JSON.stringify(body);
  return {
    status,
    ok: status >= 200 && status < 300,
    text: () => Promise.resolve(text),
  } as Response;
}

const fetchMock = vi.fn();

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("request building", () => {
  it("searchRooms issues a no-store GET with the criteria as query params", async () => {
    fetchMock.mockResolvedValue(fakeResponse(200, []));

    await searchRooms({ checkIn: "2999-01-10", checkOut: "2999-01-13", guests: 2 });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain("/api/rooms/search?");
    expect(url).toContain("checkIn=2999-01-10");
    expect(url).toContain("checkOut=2999-01-13");
    expect(url).toContain("guests=2");
    expect(init).toMatchObject({ cache: "no-store" });
    expect(new Headers(init.headers).get("Accept")).toBe("application/json");
  });

  it("getRoom requests the room by id", async () => {
    fetchMock.mockResolvedValue(fakeResponse(200, { roomId: 1 }));

    await getRoom(1);

    expect(fetchMock.mock.calls[0][0]).toContain("/api/rooms/1");
  });

  it("createReservation POSTs the input as a JSON body", async () => {
    fetchMock.mockResolvedValue(fakeResponse(201, { bookingReference: "STAY-1" }));
    const input = {
      roomId: 1,
      checkIn: "2999-01-10",
      checkOut: "2999-01-13",
      guestCount: 2,
      guestName: "Jordan Blake",
      guestEmail: "jordan@example.com",
    };

    await createReservation(input);

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toContain("/api/reservations");
    expect(init.method).toBe("POST");
    expect(new Headers(init.headers).get("Content-Type")).toBe("application/json");
    expect(JSON.parse(init.body as string)).toEqual(input);
  });

  it("getReservation url-encodes the reference", async () => {
    fetchMock.mockResolvedValue(fakeResponse(200, { bookingReference: "STAY-A B" }));

    await getReservation("STAY-A B");

    expect(fetchMock.mock.calls[0][0]).toContain("/api/reservations/STAY-A%20B");
  });
});

describe("response handling", () => {
  it("returns the parsed JSON body on success", async () => {
    const rooms = [{ roomId: 1, roomType: "Standard Queen" }];
    fetchMock.mockResolvedValue(fakeResponse(200, rooms));

    await expect(searchRooms({ checkIn: "a", checkOut: "b", guests: 1 })).resolves.toEqual(rooms);
  });

  it("maps a 409 envelope to an ApiError flagged as a conflict", async () => {
    fetchMock.mockResolvedValue(
      fakeResponse(409, {
        status: 409,
        error: "BookingConflict",
        message: "The selected room is no longer available for the requested dates.",
      }),
    );

    const error = await createReservation({
      roomId: 1,
      checkIn: "x",
      checkOut: "y",
      guestCount: 1,
      guestName: "A B",
      guestEmail: "a@b.com",
    }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 409, code: "BookingConflict", isConflict: true });
    expect((error as ApiError).message).toMatch(/no longer available/i);
  });

  it("carries field errors from a 400 validation envelope", async () => {
    fetchMock.mockResolvedValue(
      fakeResponse(400, {
        status: 400,
        error: "ValidationFailed",
        message: "One or more validation errors occurred.",
        errors: { guestEmail: ["Guest email is not valid."] },
      }),
    );

    const error = (await getRoom(1).catch((e: unknown) => e)) as ApiError;

    expect(error.isValidation).toBe(true);
    expect(error.fieldErrors).toEqual({ guestEmail: ["Guest email is not valid."] });
  });

  it("flags a 404 response as not found", async () => {
    fetchMock.mockResolvedValue(fakeResponse(404, { error: "NotFound", message: "Room 9 was not found." }));

    const error = (await getRoom(9).catch((e: unknown) => e)) as ApiError;

    expect(error.isNotFound).toBe(true);
  });

  it("falls back to a generic ServerError when the error body is not JSON", async () => {
    fetchMock.mockResolvedValue(fakeResponse(500, "<html>oops</html>"));

    const error = (await getRoom(1).catch((e: unknown) => e)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.code).toBe("ServerError");
    expect(error.message).toMatch(/status 500/i);
  });

  it("maps a thrown fetch (offline) to a network ApiError", async () => {
    fetchMock.mockRejectedValue(new TypeError("Failed to fetch"));

    const error = (await searchRooms({ checkIn: "a", checkOut: "b", guests: 1 }).catch(
      (e: unknown) => e,
    )) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.isNetwork).toBe(true);
    expect(error.status).toBe(0);
    expect(error.message).toMatch(/could not reach/i);
  });
});

describe("firstFieldErrors", () => {
  it("flattens each field to its first message and normalizes the key casing", () => {
    expect(
      firstFieldErrors({
        GuestEmail: ["Guest email is not valid.", "second message"],
        guestName: ["Guest name is required."],
      }),
    ).toEqual({
      guestEmail: "Guest email is not valid.",
      guestName: "Guest name is required.",
    });
  });

  it("returns an empty object when there is nothing to map", () => {
    expect(firstFieldErrors(undefined)).toEqual({});
  });
});
