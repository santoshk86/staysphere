import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { makeRoom, searchCriteria } from "@/test/fixtures";
import { RoomCard } from "./RoomCard";

describe("RoomCard", () => {
  it("shows the room type, price, capacity, room number and description", () => {
    render(<RoomCard room={makeRoom()} search={searchCriteria} />);

    expect(screen.getByRole("heading", { name: "Deluxe King" })).toBeInTheDocument();
    expect(screen.getByText("$159.00")).toBeInTheDocument();
    expect(screen.getByText(/per night/i)).toBeInTheDocument();
    expect(screen.getByText(/sleeps up to 2 guests/i)).toBeInTheDocument();
    expect(screen.getByText(/room 201/i)).toBeInTheDocument();
    expect(screen.getByText(/spacious room with a king bed/i)).toBeInTheDocument();
  });

  it("renders an image placeholder rather than loading a photo", () => {
    render(<RoomCard room={makeRoom()} search={searchCriteria} />);

    expect(screen.getByRole("img", { name: /deluxe king room photo placeholder/i })).toBeInTheDocument();
    expect(screen.queryByRole("img")).not.toHaveAttribute("src");
  });

  it("lists up to four amenities", () => {
    render(
      <RoomCard
        room={makeRoom({ amenities: ["Wi-Fi", "AC", "TV", "Mini bar", "Safe", "Balcony"] })}
        search={searchCriteria}
      />,
    );

    const list = screen.getByRole("list");
    expect(within(list).getAllByRole("listitem")).toHaveLength(4);
  });

  it("links to the room details route and carries the search dates through", () => {
    render(<RoomCard room={makeRoom({ roomId: 7 })} search={searchCriteria} />);

    const link = screen.getByRole("link", { name: /view details/i });
    expect(link).toHaveAttribute(
      "href",
      "/rooms/7?checkIn=2999-01-10&checkOut=2999-01-13&guests=2",
    );
  });

  it("uses the singular noun for a single-guest room", () => {
    render(<RoomCard room={makeRoom({ maxGuests: 1 })} search={searchCriteria} />);

    expect(screen.getByText(/sleeps up to 1 guest\b/i)).toBeInTheDocument();
  });
});
