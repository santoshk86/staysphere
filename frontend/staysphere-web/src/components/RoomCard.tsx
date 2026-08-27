import Link from "next/link";
import { AmenityList } from "@/components/AmenityList";
import { RoomImage } from "@/components/RoomImage";
import { formatCurrency } from "@/lib/format";
import type { Room, SearchCriteria } from "@/lib/types";

interface RoomCardProps {
  room: Room;
  search: SearchCriteria;
}

function query(search: SearchCriteria): string {
  return `checkIn=${search.checkIn}&checkOut=${search.checkOut}&guests=${search.guests}`;
}

/** One room in the search results grid. */
export function RoomCard({ room, search }: RoomCardProps) {
  const href = `/rooms/${room.roomId}?${query(search)}`;

  return (
    <article className="flex flex-col overflow-hidden rounded-xl border border-border bg-surface shadow-sm transition-shadow hover:shadow-md">
      <RoomImage roomType={room.roomType} className="aspect-[3/2] w-full" />

      <div className="flex flex-1 flex-col gap-3 p-4">
        <div className="flex items-start justify-between gap-3">
          <h3 className="text-lg font-semibold text-foreground">{room.roomType}</h3>
          <p className="shrink-0 text-right">
            <span className="text-lg font-semibold text-foreground">
              {formatCurrency(room.pricePerNight)}
            </span>
            <span className="block text-xs text-muted">per night</span>
          </p>
        </div>

        <p className="text-sm text-muted">
          Sleeps up to {room.maxGuests} {room.maxGuests === 1 ? "guest" : "guests"} · Room{" "}
          {room.roomNumber}
        </p>

        <p className="line-clamp-2 text-sm text-foreground/80">{room.description}</p>

        <AmenityList amenities={room.amenities.slice(0, 4)} className="mt-auto pt-2" />

        <Link
          href={href}
          className="mt-3 inline-flex items-center justify-center rounded-lg border border-brand px-4 py-2 text-sm font-semibold text-brand transition-colors hover:bg-brand hover:text-brand-foreground"
        >
          View details
        </Link>
      </div>
    </article>
  );
}
