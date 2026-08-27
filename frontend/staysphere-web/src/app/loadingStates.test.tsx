import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import BookingLoading from "./booking/[roomId]/loading";
import ConfirmationLoading from "./booking/confirmation/[reference]/loading";
import RoomDetailsLoading from "./rooms/[roomId]/loading";
import RoomsLoading from "./rooms/loading";

// The route-level loading UIs Next.js shows during navigation. Each must give the
// user a labelled "we're working on it" signal rather than a blank screen.
describe("route loading states", () => {
  it.each([
    ["room search", RoomsLoading, /loading rooms/i],
    ["room details", RoomDetailsLoading, /loading room details/i],
    ["booking form", BookingLoading, /loading booking form/i],
    ["confirmation", ConfirmationLoading, /loading your confirmation/i],
  ])("%s shows an accessible loading message", (_name, Loading, message) => {
    render(<Loading />);

    expect(screen.getByText(message)).toBeInTheDocument();
  });
});
