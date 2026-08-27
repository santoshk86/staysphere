import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { makeRoom } from "@/test/fixtures";
import BookingPage from "./page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), prefetch: vi.fn(), refresh: vi.fn(), back: vi.fn(), forward: vi.fn() }),
  notFound: () => {
    throw new Error("NEXT_NOT_FOUND");
  },
}));

vi.mock("@/lib/api", async (importActual) => {
  const actual = await importActual<typeof import("@/lib/api")>();
  return { ...actual, getRoom: vi.fn(), createReservation: vi.fn() };
});

import { ApiError, getRoom } from "@/lib/api";

const getRoomMock = vi.mocked(getRoom);

type Props = Parameters<typeof BookingPage>[0];

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

describe("Booking page", () => {
  it("shows the booking form and a stay summary for a valid room and search", async () => {
    getRoomMock.mockResolvedValue(makeRoom());

    render(await BookingPage(props("4", validSearch)));

    expect(screen.getByRole("heading", { name: /complete your booking/i })).toBeInTheDocument();
    expect(screen.getByLabelText("Full name")).toBeInTheDocument();
    expect(screen.getByLabelText("Email address")).toBeInTheDocument();

    expect(screen.getByText("Deluxe King")).toBeInTheDocument();
    expect(screen.getByText("Thu, Jan 10, 2999")).toBeInTheDocument();
    expect(screen.getByText("Sun, Jan 13, 2999")).toBeInTheDocument();
    expect(screen.getByText("$159.00 × 3 nights")).toBeInTheDocument();
    expect(screen.getByText("Total").parentElement).toHaveTextContent("$477.00");
  });

  it("asks the user to start a search when the stay dates are missing or invalid", async () => {
    render(await BookingPage(props("4")));

    expect(screen.getByText(/missing stay details/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /start a search/i })).toBeInTheDocument();
    expect(getRoomMock).not.toHaveBeenCalled();
  });

  it("blocks booking and explains when the party exceeds the room capacity", async () => {
    getRoomMock.mockResolvedValue(makeRoom({ maxGuests: 2 }));

    render(await BookingPage(props("4", { ...validSearch, guests: "4" })));

    expect(screen.getByText(/too many guests for this room/i)).toBeInTheDocument();
    expect(screen.queryByLabelText("Full name")).not.toBeInTheDocument();
  });

  it("calls notFound() when the room does not exist", async () => {
    getRoomMock.mockRejectedValue(new ApiError(404, "NotFound", "Room 999 was not found."));

    await expect(BookingPage(props("999", validSearch))).rejects.toThrow("NEXT_NOT_FOUND");
  });

  it("shows an error message when the room cannot be loaded", async () => {
    getRoomMock.mockRejectedValue(new ApiError(500, "ServerError", "An unexpected error occurred."));

    render(await BookingPage(props("4", validSearch)));

    expect(screen.getByText(/couldn't load this room/i)).toBeInTheDocument();
  });
});
