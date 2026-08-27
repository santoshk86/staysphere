import Link from "next/link";
import { notFound } from "next/navigation";
import { Alert } from "@/components/Alert";
import { AmenityList } from "@/components/AmenityList";
import { PriceBreakdown } from "@/components/PriceBreakdown";
import { RoomImage } from "@/components/RoomImage";
import { ApiError, getReservation } from "@/lib/api";
import { formatDate } from "@/lib/format";

export const metadata = { title: "Booking confirmed" };

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-4 py-2">
      <dt className="text-muted">{label}</dt>
      <dd className="text-right font-medium text-foreground">{value}</dd>
    </div>
  );
}

export default async function ConfirmationPage(
  props: PageProps<"/booking/confirmation/[reference]">,
) {
  const { reference } = await props.params;

  let booking;
  try {
    booking = await getReservation(reference);
  } catch (error) {
    if (error instanceof ApiError && error.isNotFound) {
      notFound();
    }
    return (
      <div className="space-y-4">
        <Alert variant="error" title="Couldn't load this booking">
          {error instanceof ApiError ? error.message : "Please try again shortly."}
        </Alert>
        <Link href="/" className="text-sm font-semibold text-brand hover:underline">
          ← Back to home
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-6 text-center">
        <div
          aria-hidden="true"
          className="mx-auto grid size-12 place-items-center rounded-full bg-emerald-600 text-2xl text-white"
        >
          ✓
        </div>
        <h1 className="mt-3 text-2xl font-semibold tracking-tight text-emerald-900">
          Booking confirmed
        </h1>
        <p className="mt-1 text-sm text-emerald-800">
          A confirmation for {booking.guestEmail} is shown below.
        </p>
        <p className="mt-4 text-xs font-medium uppercase tracking-wider text-emerald-700">
          Booking reference
        </p>
        <p className="font-mono text-2xl font-semibold tracking-wider text-emerald-950">
          {booking.bookingReference}
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
        <div className="space-y-6 rounded-xl border border-border bg-surface p-5 shadow-sm sm:p-6">
          <section>
            <h2 className="text-sm font-semibold text-foreground">Guest</h2>
            <dl className="mt-2 divide-y divide-border text-sm">
              <Row label="Name" value={booking.guestName} />
              <Row label="Email" value={booking.guestEmail} />
              <Row label="Status" value={booking.status} />
            </dl>
          </section>

          <section>
            <h2 className="text-sm font-semibold text-foreground">Stay</h2>
            <dl className="mt-2 divide-y divide-border text-sm">
              <Row label="Check-in" value={formatDate(booking.checkIn)} />
              <Row label="Check-out" value={formatDate(booking.checkOut)} />
              <Row
                label="Nights"
                value={String(booking.nights)}
              />
              <Row
                label="Guests"
                value={`${booking.guestCount} ${booking.guestCount === 1 ? "guest" : "guests"}`}
              />
            </dl>
          </section>

          {booking.specialRequests ? (
            <section>
              <h2 className="text-sm font-semibold text-foreground">Special requests</h2>
              <p className="mt-2 whitespace-pre-line text-sm text-foreground/90">
                {booking.specialRequests}
              </p>
            </section>
          ) : null}
        </div>

        <aside className="h-fit space-y-4 rounded-xl border border-border bg-surface p-5 shadow-sm">
          <RoomImage roomType={booking.roomType} className="aspect-[3/2] w-full rounded-lg" />
          <div>
            <p className="font-semibold text-foreground">{booking.roomType}</p>
            <p className="text-sm text-muted">Room {booking.roomNumber}</p>
          </div>
          <AmenityList amenities={booking.amenities} />
          <div className="border-t border-border pt-4">
            <PriceBreakdown
              pricePerNight={booking.pricePerNight}
              nights={booking.nights}
              total={booking.totalPrice}
            />
          </div>
        </aside>
      </div>

      <div className="flex flex-wrap gap-3">
        <Link
          href="/rooms"
          className="inline-flex items-center justify-center rounded-lg bg-brand px-5 py-2.5 text-sm font-semibold text-brand-foreground hover:bg-teal-800"
        >
          Book another room
        </Link>
        <Link
          href="/"
          className="inline-flex items-center justify-center rounded-lg border border-border px-5 py-2.5 text-sm font-semibold text-foreground hover:bg-background"
        >
          Back to home
        </Link>
      </div>
    </div>
  );
}
