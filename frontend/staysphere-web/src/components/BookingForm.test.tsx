import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ReservationConfirmation } from "@/lib/types";
import { makeConfirmation, makeRoom, searchCriteria } from "@/test/fixtures";
import { BookingForm } from "./BookingForm";

const { push } = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace: vi.fn(), prefetch: vi.fn(), refresh: vi.fn(), back: vi.fn(), forward: vi.fn() }),
}));

vi.mock("@/lib/api", async (importActual) => {
  const actual = await importActual<typeof import("@/lib/api")>();
  return { ...actual, createReservation: vi.fn() };
});

// Import after the mock so we get the mocked function and the real ApiError class.
import { ApiError, createReservation } from "@/lib/api";

const createReservationMock = vi.mocked(createReservation);

function renderForm() {
  return render(<BookingForm room={makeRoom()} criteria={searchCriteria} />);
}

async function fillGuestDetails(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText("Full name"), "Jordan Blake");
  await user.type(screen.getByLabelText("Email address"), "jordan@example.com");
}

const submitButton = () => screen.getByRole("button");

beforeEach(() => {
  push.mockReset();
  createReservationMock.mockReset();
});

describe("BookingForm", () => {
  it("renders the guest name, email and special-requests fields", () => {
    renderForm();

    expect(screen.getByLabelText("Full name")).toBeInTheDocument();
    expect(screen.getByLabelText("Email address")).toBeInTheDocument();
    expect(screen.getByLabelText(/special requests/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /confirm booking/i })).toBeInTheDocument();
  });

  it("blocks submission and shows field errors when required details are missing", async () => {
    const user = userEvent.setup({ delay: null });
    renderForm();

    await user.click(screen.getByRole("button", { name: /confirm booking/i }));

    expect(await screen.findByText("Enter the guest name.")).toBeInTheDocument();
    expect(screen.getByText("Enter an email address.")).toBeInTheDocument();
    expect(createReservationMock).not.toHaveBeenCalled();
  });

  it("rejects an invalid email address", async () => {
    const user = userEvent.setup({ delay: null });
    renderForm();

    await user.type(screen.getByLabelText("Full name"), "Jordan Blake");
    await user.type(screen.getByLabelText("Email address"), "not-an-email");
    await user.click(screen.getByRole("button", { name: /confirm booking/i }));

    expect(await screen.findByText(/enter a valid email address/i)).toBeInTheDocument();
    expect(createReservationMock).not.toHaveBeenCalled();
  });

  it("sends the room + stay + guest details and navigates to the confirmation on success", async () => {
    const user = userEvent.setup({ delay: null });
    createReservationMock.mockResolvedValue(makeConfirmation({ bookingReference: "STAY-XYZ123" }));
    renderForm();

    await fillGuestDetails(user);
    await user.click(screen.getByRole("button", { name: /confirm booking/i }));

    await waitFor(() =>
      expect(push).toHaveBeenCalledWith("/booking/confirmation/STAY-XYZ123"),
    );
    expect(createReservationMock).toHaveBeenCalledWith({
      roomId: 4,
      checkIn: "2999-01-10",
      checkOut: "2999-01-13",
      guestCount: 2,
      guestName: "Jordan Blake",
      guestEmail: "jordan@example.com",
      specialRequests: undefined,
    });
  });

  it("shows a submitting state and disables the button while the request is in flight", async () => {
    const user = userEvent.setup({ delay: null });
    let resolve: (value: ReservationConfirmation) => void = () => {};
    createReservationMock.mockReturnValue(
      new Promise<ReservationConfirmation>((res) => {
        resolve = res;
      }),
    );
    renderForm();

    await fillGuestDetails(user);
    await user.click(screen.getByRole("button", { name: /confirm booking/i }));

    expect(submitButton()).toBeDisabled();
    expect(screen.getByText(/confirming your booking/i)).toBeInTheDocument();

    resolve(makeConfirmation({ bookingReference: "STAY-DONE" }));
    await waitFor(() => expect(push).toHaveBeenCalledWith("/booking/confirmation/STAY-DONE"));
  });

  it("ignores repeated submits while a request is pending", async () => {
    const user = userEvent.setup({ delay: null });
    createReservationMock.mockReturnValue(new Promise<ReservationConfirmation>(() => {}));
    renderForm();

    await fillGuestDetails(user);
    const button = screen.getByRole("button", { name: /confirm booking/i });
    await user.click(button);
    await user.click(button);
    await user.click(button);

    expect(createReservationMock).toHaveBeenCalledTimes(1);
  });

  it("shows a dedicated 'just booked' message with a recovery link on a 409 conflict", async () => {
    const user = userEvent.setup({ delay: null });
    createReservationMock.mockRejectedValue(
      new ApiError(409, "BookingConflict", "The selected room is no longer available for the requested dates."),
    );
    renderForm();

    await fillGuestDetails(user);
    await user.click(screen.getByRole("button", { name: /confirm booking/i }));

    expect(await screen.findByText(/just booked by someone else/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /search for another room/i })).toHaveAttribute(
      "href",
      "/rooms?checkIn=2999-01-10&checkOut=2999-01-13&guests=2",
    );
    expect(submitButton()).toBeEnabled();
    expect(push).not.toHaveBeenCalled();
  });

  it("maps backend 400 field errors back onto the matching input", async () => {
    const user = userEvent.setup({ delay: null });
    createReservationMock.mockRejectedValue(
      new ApiError(400, "ValidationFailed", "One or more validation errors occurred.", {
        status: 400,
        error: "ValidationFailed",
        message: "One or more validation errors occurred.",
        errors: { guestEmail: ["That email address was rejected by the server."] },
      }),
    );
    renderForm();

    await fillGuestDetails(user);
    await user.click(screen.getByRole("button", { name: /confirm booking/i }));

    expect(
      await screen.findByText("That email address was rejected by the server."),
    ).toBeInTheDocument();
  });

  it("surfaces a non-field 400 error in the summary alert", async () => {
    const user = userEvent.setup({ delay: null });
    createReservationMock.mockRejectedValue(
      new ApiError(400, "ValidationFailed", "One or more validation errors occurred.", {
        status: 400,
        error: "ValidationFailed",
        message: "One or more validation errors occurred.",
        errors: { checkIn: ["Check-in date cannot be in the past."] },
      }),
    );
    renderForm();

    await fillGuestDetails(user);
    await user.click(screen.getByRole("button", { name: /confirm booking/i }));

    expect(await screen.findByText("Check-in date cannot be in the past.")).toBeInTheDocument();
  });

  it("shows the server message on a 500 error and lets the user retry", async () => {
    const user = userEvent.setup({ delay: null });
    createReservationMock.mockRejectedValue(
      new ApiError(500, "ServerError", "An unexpected error occurred."),
    );
    renderForm();

    await fillGuestDetails(user);
    await user.click(screen.getByRole("button", { name: /confirm booking/i }));

    expect(await screen.findByText("An unexpected error occurred.")).toBeInTheDocument();
    expect(submitButton()).toBeEnabled();
  });

  it("shows a connectivity message on a network failure", async () => {
    const user = userEvent.setup({ delay: null });
    createReservationMock.mockRejectedValue(
      new ApiError(0, "Network", "Could not reach the StaySphere service. Check your connection and try again."),
    );
    renderForm();

    await fillGuestDetails(user);
    await user.click(screen.getByRole("button", { name: /confirm booking/i }));

    expect(await screen.findByText(/could not reach the staysphere service/i)).toBeInTheDocument();
  });

  it("treats a 404 as the room having disappeared", async () => {
    const user = userEvent.setup({ delay: null });
    createReservationMock.mockRejectedValue(new ApiError(404, "NotFound", "Room 4 was not found."));
    renderForm();

    await fillGuestDetails(user);
    await user.click(screen.getByRole("button", { name: /confirm booking/i }));

    expect(await screen.findByText(/no longer available\. please start a new search/i)).toBeInTheDocument();
  });
});
