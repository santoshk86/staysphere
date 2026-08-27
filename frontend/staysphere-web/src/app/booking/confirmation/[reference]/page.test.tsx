import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { makeConfirmation } from "@/test/fixtures";
import ConfirmationPage from "./page";

vi.mock("next/navigation", () => ({
  notFound: () => {
    throw new Error("NEXT_NOT_FOUND");
  },
}));

vi.mock("@/lib/api", async (importActual) => {
  const actual = await importActual<typeof import("@/lib/api")>();
  return { ...actual, getReservation: vi.fn() };
});

import { ApiError, getReservation } from "@/lib/api";

const getReservationMock = vi.mocked(getReservation);

type Props = Parameters<typeof ConfirmationPage>[0];

const props = (reference: string): Props =>
  ({ params: Promise.resolve({ reference }), searchParams: Promise.resolve({}) }) as Props;

beforeEach(() => {
  getReservationMock.mockReset();
});

describe("Booking confirmation page", () => {
  it("displays the reference, guest details, room, dates, guest count and price summary", async () => {
    getReservationMock.mockResolvedValue(makeConfirmation());

    render(await ConfirmationPage(props("STAY-MJXR4R8V")));

    expect(screen.getByRole("heading", { name: /booking confirmed/i })).toBeInTheDocument();
    expect(screen.getByText("STAY-MJXR4R8V")).toBeInTheDocument();

    expect(screen.getByText("Jordan Blake")).toBeInTheDocument();
    expect(screen.getByText("jordan.blake@example.com")).toBeInTheDocument();

    expect(screen.getByText("Deluxe King")).toBeInTheDocument();
    expect(screen.getByText(/room 201/i)).toBeInTheDocument();

    expect(screen.getByText("Thu, Jan 10, 2999")).toBeInTheDocument(); // check-in, formatted
    expect(screen.getByText("Sun, Jan 13, 2999")).toBeInTheDocument(); // check-out, formatted

    expect(screen.getByText("Nights").parentElement).toHaveTextContent("3");
    expect(screen.getByText("Guests").parentElement).toHaveTextContent("2 guests");

    expect(screen.getByText("Total").parentElement).toHaveTextContent("$477.00");
    expect(screen.getByText(/late check-in around 11pm/i)).toBeInTheDocument();
  });

  it("omits the special-requests section when there were none", async () => {
    getReservationMock.mockResolvedValue(makeConfirmation({ specialRequests: null }));

    render(await ConfirmationPage(props("STAY-NOEXTRAS")));

    expect(screen.queryByRole("heading", { name: /special requests/i })).not.toBeInTheDocument();
  });

  it("calls notFound() for an unknown reference", async () => {
    getReservationMock.mockRejectedValue(new ApiError(404, "NotFound", "Reservation 'STAY-NOPE' was not found."));

    await expect(ConfirmationPage(props("STAY-NOPE"))).rejects.toThrow("NEXT_NOT_FOUND");
  });

  it("shows an error message when the confirmation cannot be loaded", async () => {
    getReservationMock.mockRejectedValue(new ApiError(500, "ServerError", "An unexpected error occurred."));

    render(await ConfirmationPage(props("STAY-MJXR4R8V")));

    expect(screen.getByText(/couldn't load this booking/i)).toBeInTheDocument();
  });
});
