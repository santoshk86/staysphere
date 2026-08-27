import Link from "next/link";
import { notFound } from "next/navigation";
import { Alert } from "@/components/Alert";
import { AmenityList } from "@/components/AmenityList";
import { RoomImage } from "@/components/RoomImage";
import { ApiError, getRoom } from "@/lib/api";
import { formatCurrency } from "@/lib/format";
import { validateSearch } from "@/lib/validation";

export const metadata = { title: "Room details" };

function firstValue(value: string | string[] | undefined): string {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

export default async function RoomDetailsPage(props: PageProps<"/rooms/[roomId]">) {
  const { roomId } = await props.params;
  const sp = await props.searchParams;
  const raw = {
    checkIn: firstValue(sp.checkIn),
    checkOut: firstValue(sp.checkOut),
    guests: firstValue(sp.guests),
  };
  const search = validateSearch(raw, { allowPastCheckIn: true });

  let room;
  try {
    room = await getRoom(roomId);
  } catch (error) {
    if (error instanceof ApiError && error.isNotFound) {
      notFound();
    }
    return (
      <div className="space-y-4">
        <Alert variant="error" title="Couldn't load this room">
          {error instanceof ApiError ? error.message : "Please try again shortly."}
        </Alert>
        <Link href="/rooms" className="text-sm font-semibold text-brand hover:underline">
          ← Back to search
        </Link>
      </div>
    );
  }

  const query = search.ok
    ? `?checkIn=${search.criteria.checkIn}&checkOut=${search.criteria.checkOut}&guests=${search.criteria.guests}`
    : "";
  const overCapacity = search.ok && search.criteria.guests > room.maxGuests;

  return (
    <div className="space-y-6">
      <Link
        href={`/rooms${query}`}
        className="inline-block text-sm font-semibold text-brand hover:underline"
      >
        ← Back to results
      </Link>

      <div className="grid gap-6 lg:grid-cols-[1.4fr_1fr]">
        <div className="space-y-5">
          <RoomImage
            roomType={room.roomType}
            className="aspect-[16/10] w-full rounded-xl"
          />
          <div>
            <h1 className="text-2xl font-semibold tracking-tight text-foreground">
              {room.roomType}
            </h1>
            <p className="mt-1 text-sm text-muted">
              Room {room.roomNumber} · Sleeps up to {room.maxGuests}{" "}
              {room.maxGuests === 1 ? "guest" : "guests"}
            </p>
          </div>
          <p className="text-foreground/90">{room.description}</p>
          <div className="space-y-2">
            <h2 className="text-sm font-semibold text-foreground">Amenities</h2>
            <AmenityList amenities={room.amenities} />
          </div>
        </div>

        <aside className="h-fit space-y-4 rounded-xl border border-border bg-surface p-5 shadow-sm lg:sticky lg:top-6">
          <p>
            <span className="text-2xl font-semibold text-foreground">
              {formatCurrency(room.pricePerNight)}
            </span>
            <span className="text-sm text-muted"> / night</span>
          </p>

          {search.ok ? (
            <dl className="space-y-1 text-sm text-muted">
              <div className="flex justify-between">
                <dt>Check-in</dt>
                <dd className="text-foreground">{search.criteria.checkIn}</dd>
              </div>
              <div className="flex justify-between">
                <dt>Check-out</dt>
                <dd className="text-foreground">{search.criteria.checkOut}</dd>
              </div>
              <div className="flex justify-between">
                <dt>Guests</dt>
                <dd className="text-foreground">{search.criteria.guests}</dd>
              </div>
            </dl>
          ) : null}

          {overCapacity ? (
            <Alert variant="error">
              This room sleeps {room.maxGuests}. Reduce the guest count to book it.
            </Alert>
          ) : null}

          {search.ok && !overCapacity ? (
            <Link
              href={`/booking/${room.roomId}${query}`}
              className="inline-flex w-full items-center justify-center rounded-lg bg-brand px-5 py-3 text-sm font-semibold text-brand-foreground transition-colors hover:bg-teal-800"
            >
              Book this room
            </Link>
          ) : !search.ok ? (
            <div className="space-y-2">
              <Alert variant="info">Add your dates to book this room.</Alert>
              <Link
                href="/rooms"
                className="inline-flex w-full items-center justify-center rounded-lg border border-brand px-5 py-3 text-sm font-semibold text-brand hover:bg-brand hover:text-brand-foreground"
              >
                Choose dates
              </Link>
            </div>
          ) : null}

          <p className="text-xs text-muted">
            Availability is confirmed when you complete the booking.
          </p>
        </aside>
      </div>
    </div>
  );
}
