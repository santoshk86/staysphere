import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { PriceBreakdown } from "./PriceBreakdown";

describe("PriceBreakdown", () => {
  it("shows the nightly rate line and computes the total when none is given", () => {
    render(<PriceBreakdown pricePerNight={159} nights={3} />);

    expect(screen.getByText("$159.00 × 3 nights")).toBeInTheDocument();
    expect(screen.getByText("Total").parentElement).toHaveTextContent("$477.00");
  });

  it("prefers an authoritative total from the API over the computed one", () => {
    render(<PriceBreakdown pricePerNight={159} nights={3} total={450} />);

    expect(screen.getByText("Total").parentElement).toHaveTextContent("$450.00");
  });

  it("uses the singular noun for a one-night stay", () => {
    render(<PriceBreakdown pricePerNight={99} nights={1} />);

    expect(screen.getByText("$99.00 × 1 night")).toBeInTheDocument();
  });
});
