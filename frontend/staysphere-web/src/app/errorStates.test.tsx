import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import GlobalError from "./error";
import NotFound from "./not-found";

afterEach(() => {
  vi.restoreAllMocks();
});

describe("not-found route", () => {
  it("explains the page is missing and offers a way back to search", () => {
    render(<NotFound />);

    expect(screen.getByRole("heading", { name: /page not found/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /search for a room/i })).toHaveAttribute("href", "/rooms");
  });
});

describe("error boundary", () => {
  it("shows a recovery message and retries when the user clicks Try again", async () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    const reset = vi.fn();
    const user = userEvent.setup({ delay: null });

    render(<GlobalError error={new Error("boom") as Error & { digest?: string }} reset={reset} />);

    expect(screen.getByRole("heading", { name: /something went wrong/i })).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /try again/i }));
    expect(reset).toHaveBeenCalledTimes(1);
  });
});
