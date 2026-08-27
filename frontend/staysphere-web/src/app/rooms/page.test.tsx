import { render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { SearchCriteria } from "@/lib/types";
import { makeRoom } from "@/test/fixtures";
import RoomsPage, { RoomResults } from "./page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), prefetch: vi.fn(), refresh: vi.fn(), back: vi.fn(), forward: vi.fn() }),
}));

vi.mock("@/lib/api", async (importActual) => {
  const actual = await importActual<typeof import("@/lib/api")>();
  return { ...actual, searchRooms: vi.fn() };
});

import { ApiError, searchRooms } from "@/lib/api";

const searchRoomsMock = vi.mocked(searchRooms);

type Props = Parameters<typeof RoomsPage>[0];

const props = (searchParams: Record<string, string> = {}): Props =>
  ({ params: Promise.resolve({}), searchParams: Promise.resolve(searchParams) }) as Props;

const criteria: SearchCriteria = { checkIn: "2999-01-10", checkOut: "2999-01-13", guests: 2 };

beforeEach(() => {
  searchRoomsMock.mockReset();
});

// The page renders a client SearchForm plus a <Suspense> boundary around the
// async <RoomResults> data section. The synchronous branches are asserted here;
// the streamed data section is asserted directly against RoomResults below
// (React's client renderer cannot resume a nested async server component).
describe("Rooms search page (synchronous branches)", () => {
  it("prompts for a search and does not call the API when there is no query", async () => {
    render(await RoomsPage(props()));

    expect(screen.getByRole("heading", { name: /search for a room/i })).toBeInTheDocument();
    expect(screen.getByText(/enter your dates and guest count above/i)).toBeInTheDocument();
    expect(searchRoomsMock).not.toHaveBeenCalled();
  });

  it("shows the validation problem and does not call the API for an invalid query", async () => {
    render(await RoomsPage(props({ checkIn: "2999-01-13", checkOut: "2999-01-11", guests: "2" })));

    expect(screen.getByText(/check your search/i)).toBeInTheDocument();
    expect(screen.getByText(/check-out must be after check-in/i)).toBeInTheDocument();
    expect(searchRoomsMock).not.toHaveBeenCalled();
  });

  it("reflects the current query back into the heading and the search toolbar", async () => {
    render(await RoomsPage(props({ checkIn: "bad-date", checkOut: "2999-01-13", guests: "3" })));

    expect(screen.getByRole("heading", { name: /rooms for your stay/i })).toBeInTheDocument();
    expect(screen.getByLabelText("Guests")).toHaveValue(3);
    expect(screen.getByText(/check your search/i)).toBeInTheDocument();
  });
});

describe("RoomResults", () => {
  it("searches with the given criteria and renders the returned rooms", async () => {
    searchRoomsMock.mockResolvedValue([
      makeRoom({ roomId: 4, roomType: "Deluxe King", roomNumber: "201" }),
      makeRoom({ roomId: 6, roomType: "Family Suite", roomNumber: "301", pricePerNight: 249, maxGuests: 4 }),
    ]);

    render(await RoomResults({ criteria }));

    expect(searchRoomsMock).toHaveBeenCalledWith(criteria);
    const region = screen.getByRole("region", { name: /available rooms/i });
    expect(within(region).getByText(/2 rooms available for 2999-01-10 → 2999-01-13/i)).toBeInTheDocument();
    expect(within(region).getByRole("heading", { name: "Deluxe King" })).toBeInTheDocument();
    expect(within(region).getByRole("heading", { name: "Family Suite" })).toBeInTheDocument();
    expect(within(region).getAllByRole("link", { name: /view details/i })).toHaveLength(2);
  });

  it("shows an empty state when no rooms match", async () => {
    searchRoomsMock.mockResolvedValue([]);

    render(await RoomResults({ criteria }));

    expect(screen.getByText(/no rooms available/i)).toBeInTheDocument();
    expect(
      screen.getByText(/no rooms match 2999-01-10 → 2999-01-13 for 2 guests/i),
    ).toBeInTheDocument();
  });

  it("shows an error state when the search API fails", async () => {
    searchRoomsMock.mockRejectedValue(new ApiError(500, "ServerError", "An unexpected error occurred."));

    render(await RoomResults({ criteria }));

    expect(screen.getByText(/search failed/i)).toBeInTheDocument();
    expect(screen.getByText(/please adjust your search above and try again/i)).toBeInTheDocument();
  });

  it("surfaces the API error message when one is available", async () => {
    searchRoomsMock.mockRejectedValue(
      new ApiError(400, "ValidationFailed", "Check-out date must be after the check-in date."),
    );

    render(await RoomResults({ criteria }));

    expect(screen.getByText("Check-out date must be after the check-in date.")).toBeInTheDocument();
  });
});
