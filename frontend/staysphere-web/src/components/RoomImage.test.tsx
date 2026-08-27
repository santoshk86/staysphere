import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { RoomImage } from "./RoomImage";

describe("RoomImage", () => {
  it("exposes an accessible placeholder labelled with the room type", () => {
    render(<RoomImage roomType="Deluxe King" />);

    const placeholder = screen.getByRole("img", { name: "Deluxe King room photo placeholder" });
    expect(placeholder).toBeInTheDocument();
    expect(placeholder).toHaveTextContent("DK");
    expect(placeholder).toHaveTextContent(/photo coming soon/i);
  });

  it("is a decorative element, not a real <img> with a source", () => {
    render(<RoomImage roomType="Family Suite" />);

    expect(screen.getByRole("img").tagName).toBe("DIV");
  });
});
