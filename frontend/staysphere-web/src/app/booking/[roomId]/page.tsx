import Link from "next/link";
import { notFound } from "next/navigation";
import { Alert } from "@/components/Alert";
import { BookingForm } from "@/components/BookingForm";
import { PriceBreakdown } from "@/components/PriceBreakdown";
import { RoomImage } from "@/components/RoomImage";
import { ApiError, getRoom } from "@/lib/api";
import { formatDate, nightsBetween } from "@/lib/format";
import { validateSearch } from "@/lib/validation";

export const metadata = { title: "Book your stay" };

function firstValue(value: string | string[] | undefined): string {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

export default async function BookingPage(props: PageProps<"/booking/[roomId]">) {
  const { roomId } = await props.params;
  const sp = await props.searchParams;
  const search = validateSearch(
    {
      checkIn: firstValue(sp.checkIn),
      checkOut: firstValue(sp.checkOut),
      guests: firstValue(sp.guests),
    },
    { allowPastCheckIn: true },
  );

  if (!search.ok) {
    return (
      <div className="space-y-4">
        <Alert variant="error" title="Missing stay details">
          We need valid check-in and check-out dates and a guest count before you can book.
        </Alert>
        <Link href="/rooms" className="text-sm font-semibold text-brand hover:underline">
          Start a search
        </Link>
      </div>
    );
  }

  const criteria = search.criteria;

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

  const detailsHref = `/rooms/${room.roomId}?checkIn=${criteria.checkIn}&checkOut=${criteria.checkOut}&guests=${criteria.guests}`;

  if (criteria.guests > room.maxGuests) {
    return (
      <div className="space-y-4">
        <Alert variant="error" title="Too many guests for this room">
          The {room.roomType} sleeps up to {room.maxGuests}. Lower the guest count to continue.
        </Alert>
        <Link href={detailsHref} className="text-sm font-semibold text-brand hover:underline">
          ← Back to room details
        </Link>
      </div>
    );
  }

  const nights = nightsBetween(criteria.checkIn, criteria.checkOut) ?? 1;

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <Link href={detailsHref} className="text-sm font-semibold text-brand hover:underline">
          ← Back to room details
        </Link>
        <h1 className="text-2xl font-semibold tracking-tight text-foreground">
          Complete your booking
        </h1>
        <p className="text-sm text-muted">
          No account or payment required — you&apos;ll get a booking reference right away.
        </p>
      </div>

      <div className="grid gap-8 lg:grid-cols-[1fr_20rem]">
        <div className="rounded-xl border border-border bg-surface p-5 shadow-sm sm:p-6">
          <h2 className="mb-4 text-lg font-semibold text-foreground">Guest details</h2>
          <BookingForm room={room} criteria={criteria} />
        </div>

        <aside className="h-fit space-y-4 rounded-xl border border-border bg-surface p-5 shadow-sm">
          <h2 className="text-sm font-semibold text-foreground">Your stay</h2>
          <RoomImage roomType={room.roomType} className="aspect-[3/2] w-full rounded-lg" />
          <div>
            <p className="font-semibold text-foreground">{room.roomType}</p>
            <p className="text-sm text-muted">Room {room.roomNumber}</p>
          </div>
          <dl className="space-y-1 text-sm">
            <div className="flex justify-between gap-4">
              <dt className="text-muted">Check-in</dt>
              <dd className="text-right text-foreground">{formatDate(criteria.checkIn)}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-muted">Check-out</dt>
              <dd className="text-right text-foreground">{formatDate(criteria.checkOut)}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-muted">Guests</dt>
              <dd className="text-right text-foreground">{criteria.guests}</dd>
            </div>
          </dl>
          <div className="border-t border-border pt-4">
            <PriceBreakdown pricePerNight={room.pricePerNight} nights={nights} />
          </div>
        </aside>
      </div>
    </div>
  );
}
