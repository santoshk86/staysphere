import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { makeRoom } from "@/test/fixtures";
import RoomDetailsPage from "./page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), prefetch: vi.fn() }),
  notFound: () => {
    throw new Error("NEXT_NOT_FOUND");
  },
}));

vi.mock("@/lib/api", async (importActual) => {
  const actual = await importActual<typeof import("@/lib/api")>();
  return { ...actual, getRoom: vi.fn() };
});

import { ApiError, getRoom } from "@/lib/api";

const getRoomMock = vi.mocked(getRoom);

type Props = Parameters<typeof RoomDetailsPage>[0];

function props(roomId: string, searchParams: Record<string, string> = {}): Props {
  return {
    params: Promise.resolve({ roomId }),
    searchParams: Promise.resolve(searchParams),
  } as Props;
}

const validSearch = { checkIn: "2999-01-10", checkOut: "2999-01-13", guests: "2" };

beforeEach(() => {
  getRoomMock.mockReset();
});

describe("Room details page", () => {
  it("shows the room type, description, price, capacity, amenities and image placeholder", async () => {
    getRoomMock.mockResolvedValue(makeRoom());

    render(await RoomDetailsPage(props("4", validSearch)));

    expect(screen.getByRole("heading", { name: "Deluxe King" })).toBeInTheDocument();
    expect(screen.getByText(/spacious room with a king bed/i)).toBeInTheDocument();
    expect(screen.getByText("$159.00")).toBeInTheDocument();
    expect(screen.getByText(/sleeps up to 2 guests/i)).toBeInTheDocument();
    expect(screen.getByText("City view")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: /deluxe king room photo placeholder/i })).toBeInTheDocument();
  });

  it("offers a booking CTA that carries the stay dates through to the booking route", async () => {
    getRoomMock.mockResolvedValue(makeRoom({ roomId: 4 }));

    render(await RoomDetailsPage(props("4", validSearch)));

    expect(screen.getByRole("link", { name: /book this room/i })).toHaveAttribute(
      "href",
      "/booking/4?checkIn=2999-01-10&checkOut=2999-01-13&guests=2",
    );
  });

  it("prompts for dates instead of a booking CTA when the search is incomplete", async () => {
    getRoomMock.mockResolvedValue(makeRoom());

    render(await RoomDetailsPage(props("4")));

    expect(screen.getByText(/add your dates to book this room/i)).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /book this room/i })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: /choose dates/i })).toBeInTheDocument();
  });

  it("warns and withholds the CTA when the party is larger than the room capacity", async () => {
    getRoomMock.mockResolvedValue(makeRoom({ maxGuests: 2 }));

    render(await RoomDetailsPage(props("4", { ...validSearch, guests: "4" })));

    expect(screen.getByText(/this room sleeps 2\. reduce the guest count to book it/i)).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /book this room/i })).not.toBeInTheDocument();
  });

  it("calls notFound() when the room does not exist", async () => {
    getRoomMock.mockRejectedValue(new ApiError(404, "NotFound", "Room 999 was not found."));

    await expect(RoomDetailsPage(props("999", validSearch))).rejects.toThrow("NEXT_NOT_FOUND");
  });

  it("shows an error message (not a 404) when the API call fails", async () => {
    getRoomMock.mockRejectedValue(new ApiError(500, "ServerError", "An unexpected error occurred."));

    render(await RoomDetailsPage(props("4", validSearch)));

    expect(screen.getByText(/couldn't load this room/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /back to search/i })).toBeInTheDocument();
  });
});
