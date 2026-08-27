import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { AmenityList } from "./AmenityList";

describe("AmenityList", () => {
  it("renders one list item per amenity", () => {
    render(<AmenityList amenities={["Free Wi-Fi", "Air conditioning", "City view"]} />);

    const items = screen.getAllByRole("listitem").map((li) => li.textContent);
    expect(items).toEqual(["Free Wi-Fi", "Air conditioning", "City view"]);
  });

  it("renders nothing when there are no amenities", () => {
    const { container } = render(<AmenityList amenities={[]} />);

    expect(container).toBeEmptyDOMElement();
  });
});
