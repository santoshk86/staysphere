import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import HomePage from "./page";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), prefetch: vi.fn(), refresh: vi.fn(), back: vi.fn(), forward: vi.fn() }),
}));

describe("Home page", () => {
  it("leads with the value proposition and the search form", () => {
    render(<HomePage />);

    expect(
      screen.getByRole("heading", { level: 1, name: /find your room at staysphere/i }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /search rooms/i })).toBeInTheDocument();
    expect(screen.getByLabelText("Check-in")).toBeInTheDocument();
  });

  it("explains the three-step flow", () => {
    render(<HomePage />);

    const steps = screen.getByRole("heading", { name: /how it works/i });
    expect(steps).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Search" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Choose" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Book" })).toBeInTheDocument();
  });
});
