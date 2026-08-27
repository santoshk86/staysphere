import { Suspense } from "react";
import { Alert } from "@/components/Alert";
import { RoomCard } from "@/components/RoomCard";
import { SearchForm } from "@/components/SearchForm";
import { Spinner } from "@/components/Spinner";
import { ApiError, searchRooms } from "@/lib/api";
import type { SearchCriteria } from "@/lib/types";
import { validateSearch } from "@/lib/validation";

export const metadata = { title: "Room search results" };

function firstValue(value: string | string[] | undefined): string {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

// Exported so the results / empty / error rendering can be unit-tested directly;
// Next.js ignores non-reserved named exports from a route file.
export async function RoomResults({ criteria }: { criteria: SearchCriteria }) {
  let rooms;
  try {
    rooms = await searchRooms(criteria);
  } catch (error) {
    const message =
      error instanceof ApiError
        ? error.message
        : "We couldn't load rooms right now.";
    return (
      <Alert variant="error" title="Search failed">
        <p>{message}</p>
        <p className="mt-1">Please adjust your search above and try again.</p>
      </Alert>
    );
  }

  if (rooms.length === 0) {
    return (
      <Alert variant="info" title="No rooms available">
        No rooms match {criteria.checkIn} → {criteria.checkOut} for {criteria.guests}{" "}
        {criteria.guests === 1 ? "guest" : "guests"}. Try different dates or fewer guests.
      </Alert>
    );
  }

  return (
    <section aria-label="Available rooms" className="space-y-4">
      <p className="text-sm text-muted">
        {rooms.length} {rooms.length === 1 ? "room" : "rooms"} available for{" "}
        {criteria.checkIn} → {criteria.checkOut}
      </p>
      <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
        {rooms.map((room) => (
          <RoomCard key={room.roomId} room={room} search={criteria} />
        ))}
      </div>
    </section>
  );
}

export default async function RoomsPage(props: PageProps<"/rooms">) {
  const sp = await props.searchParams;
  const raw = {
    checkIn: firstValue(sp.checkIn),
    checkOut: firstValue(sp.checkOut),
    guests: firstValue(sp.guests),
  };
  const hasQuery = Boolean(raw.checkIn || raw.checkOut || raw.guests);
  const validation = validateSearch(raw, { allowPastCheckIn: true });

  return (
    <div className="space-y-8">
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">
          {hasQuery ? "Rooms for your stay" : "Search for a room"}
        </h1>
        <p className="text-sm text-muted">
          Availability is checked live against the StaySphere API.
        </p>
      </div>

      <div className="rounded-xl border border-border bg-surface p-4 shadow-sm sm:p-6">
        <SearchForm
          compact
          defaultCheckIn={raw.checkIn}
          defaultCheckOut={raw.checkOut}
          defaultGuests={raw.guests || "2"}
        />
      </div>

      {!hasQuery ? (
        <Alert variant="info">
          Enter your dates and guest count above to see available rooms.
        </Alert>
      ) : !validation.ok ? (
        <Alert variant="error" title="Check your search">
          <ul className="list-disc pl-5">
            {Object.values(validation.errors).map((message) => (
              <li key={message}>{message}</li>
            ))}
          </ul>
        </Alert>
      ) : (
        <Suspense
          key={`${validation.criteria.checkIn}-${validation.criteria.checkOut}-${validation.criteria.guests}`}
          fallback={<Spinner label="Searching available rooms…" />}
        >
          <RoomResults criteria={validation.criteria} />
        </Suspense>
      )}
    </div>
  );
}
