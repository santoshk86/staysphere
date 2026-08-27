import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Alert } from "./Alert";

describe("Alert", () => {
  it("announces errors assertively via role=alert", () => {
    render(
      <Alert variant="error" title="Booking could not be completed">
        Something went wrong.
      </Alert>,
    );

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("Booking could not be completed");
    expect(alert).toHaveTextContent("Something went wrong.");
  });

  it("uses the polite role=status for informational messages", () => {
    render(<Alert variant="info">No rooms available.</Alert>);

    expect(screen.getByRole("status")).toHaveTextContent("No rooms available.");
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
