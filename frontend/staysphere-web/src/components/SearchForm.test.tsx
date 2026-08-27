import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SearchForm } from "./SearchForm";

const { push } = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace: vi.fn(), prefetch: vi.fn(), refresh: vi.fn(), back: vi.fn(), forward: vi.fn() }),
}));

/** type=date inputs are unreliable with user-event in jsdom; set the value directly. */
function setDate(label: string, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

beforeEach(() => {
  push.mockReset();
});

describe("SearchForm", () => {
  it("renders labelled date and guest inputs and a submit button", () => {
    render(<SearchForm />);

    expect(screen.getByLabelText("Check-in")).toBeInTheDocument();
    expect(screen.getByLabelText("Check-out")).toBeInTheDocument();
    expect(screen.getByLabelText("Guests")).toHaveValue(2);
    expect(screen.getByRole("button", { name: /search rooms/i })).toBeInTheDocument();
  });

  it("lets the user change the guest count", async () => {
    const user = userEvent.setup({ delay: null });
    render(<SearchForm />);

    const guests = screen.getByLabelText("Guests");
    await user.clear(guests);
    await user.type(guests, "3");

    expect(guests).toHaveValue(3);
  });

  it("shows validation errors and does not navigate when required fields are empty", async () => {
    const user = userEvent.setup({ delay: null });
    render(<SearchForm />);

    await user.click(screen.getByRole("button", { name: /search rooms/i }));

    expect(await screen.findByText("Choose a check-in date.")).toBeInTheDocument();
    expect(screen.getByText("Choose a check-out date.")).toBeInTheDocument();
    expect(push).not.toHaveBeenCalled();
  });

  it("rejects a check-out that is not after check-in", async () => {
    const user = userEvent.setup({ delay: null });
    render(<SearchForm />);

    setDate("Check-in", "2999-01-13");
    setDate("Check-out", "2999-01-11");
    await user.click(screen.getByRole("button", { name: /search rooms/i }));

    expect(await screen.findByText(/check-out must be after check-in/i)).toBeInTheDocument();
    expect(push).not.toHaveBeenCalled();
  });

  it("rejects a guest count below one", async () => {
    const user = userEvent.setup({ delay: null });
    render(<SearchForm />);

    setDate("Check-in", "2999-01-10");
    setDate("Check-out", "2999-01-13");
    const guests = screen.getByLabelText("Guests");
    await user.clear(guests);
    await user.type(guests, "0");
    await user.click(screen.getByRole("button", { name: /search rooms/i }));

    expect(await screen.findByText(/at least one guest is required/i)).toBeInTheDocument();
    expect(push).not.toHaveBeenCalled();
  });

  it("navigates to the results route with the query for a valid search", async () => {
    const user = userEvent.setup({ delay: null });
    render(<SearchForm />);

    setDate("Check-in", "2999-01-10");
    setDate("Check-out", "2999-01-13");
    await user.click(screen.getByRole("button", { name: /search rooms/i }));

    expect(push).toHaveBeenCalledWith("/rooms?checkIn=2999-01-10&checkOut=2999-01-13&guests=2");
  });

  it("clears a field's error once the user corrects it", async () => {
    const user = userEvent.setup({ delay: null });
    render(<SearchForm />);

    await user.click(screen.getByRole("button", { name: /search rooms/i }));
    expect(await screen.findByText("Choose a check-in date.")).toBeInTheDocument();

    setDate("Check-in", "2999-01-10");

    expect(screen.queryByText("Choose a check-in date.")).not.toBeInTheDocument();
  });

  it("pre-fills values from props (used to keep the results toolbar in sync)", () => {
    render(
      <SearchForm compact defaultCheckIn="2999-02-01" defaultCheckOut="2999-02-05" defaultGuests="4" />,
    );

    expect(screen.getByLabelText("Check-in")).toHaveValue("2999-02-01");
    expect(screen.getByLabelText("Check-out")).toHaveValue("2999-02-05");
    expect(screen.getByLabelText("Guests")).toHaveValue(4);
  });
});
