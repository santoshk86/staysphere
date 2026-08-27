"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useId, useRef, useState } from "react";
import { Alert } from "@/components/Alert";
import { FormField, inputClassName } from "@/components/FormField";
import { Spinner } from "@/components/Spinner";
import { ApiError, createReservation, firstFieldErrors } from "@/lib/api";
import type { Room, SearchCriteria } from "@/lib/types";
import { validateBooking } from "@/lib/validation";

interface BookingFormProps {
  room: Room;
  criteria: SearchCriteria;
}

type Status = "idle" | "submitting" | "done";

const MAX_REQUESTS = 1000;

// Map API field-error keys onto this form's field names.
const FIELD_KEYS = new Set(["guestName", "guestEmail", "specialRequests"]);

export function BookingForm({ room, criteria }: BookingFormProps) {
  const router = useRouter();
  const prefix = useId();
  const fieldId = (name: string) => `${prefix}-${name}`;

  const [form, setForm] = useState({ guestName: "", guestEmail: "", specialRequests: "" });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [status, setStatus] = useState<Status>("idle");
  const [topError, setTopError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const topErrorRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (topError || conflict) topErrorRef.current?.focus();
  }, [topError, conflict]);

  function update(name: keyof typeof form, value: string) {
    setForm((prev) => ({ ...prev, [name]: value }));
    setErrors((prev) => {
      if (!prev[name]) return prev;
      const next = { ...prev };
      delete next[name];
      return next;
    });
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    // Guard against double submission / re-entry.
    if (status === "submitting" || status === "done") return;

    const result = validateBooking(form);
    if (!result.ok) {
      setErrors(result.errors);
      return;
    }

    setStatus("submitting");
    setTopError(null);
    setConflict(false);
    setErrors({});

    try {
      const confirmation = await createReservation({
        roomId: room.roomId,
        checkIn: criteria.checkIn,
        checkOut: criteria.checkOut,
        guestCount: criteria.guests,
        guestName: result.value.guestName,
        guestEmail: result.value.guestEmail,
        specialRequests: result.value.specialRequests,
      });
      // Keep the button disabled through the navigation.
      setStatus("done");
      router.push(
        `/booking/confirmation/${encodeURIComponent(confirmation.bookingReference)}`,
      );
    } catch (error) {
      setStatus("idle");

      if (error instanceof ApiError) {
        if (error.isConflict) {
          setConflict(true);
          return;
        }
        if (error.isValidation) {
          const flat = firstFieldErrors(error.fieldErrors);
          const fieldErrors: Record<string, string> = {};
          const other: string[] = [];
          for (const [key, message] of Object.entries(flat)) {
            if (FIELD_KEYS.has(key)) fieldErrors[key] = message;
            else other.push(message);
          }
          setErrors(fieldErrors);
          setTopError(
            other[0] ??
              (Object.keys(fieldErrors).length
                ? null
                : error.message) ??
              null,
          );
          return;
        }
        if (error.isNotFound) {
          setTopError("This room is no longer available. Please start a new search.");
          return;
        }
        setTopError(error.message);
        return;
      }

      setTopError("Something went wrong while booking. Please try again.");
    }
  }

  const searchHref = `/rooms?checkIn=${criteria.checkIn}&checkOut=${criteria.checkOut}&guests=${criteria.guests}`;
  const busy = status === "submitting" || status === "done";
  const requestsLength = form.specialRequests.length;

  return (
    <form onSubmit={handleSubmit} noValidate aria-busy={busy} className="space-y-5">
      <div
        ref={topErrorRef}
        tabIndex={-1}
        className="scroll-mt-24 outline-none"
        aria-live="assertive"
      >
        {conflict ? (
          <Alert variant="error" title="This room was just booked by someone else">
            <p>
              The room is no longer available for {criteria.checkIn} to {criteria.checkOut}.
              Your card has not been charged.
            </p>
            <Link
              href={searchHref}
              className="mt-2 inline-block font-semibold text-red-800 underline"
            >
              Search for another room
            </Link>
          </Alert>
        ) : topError ? (
          <Alert variant="error" title="Booking could not be completed">
            {topError}
          </Alert>
        ) : null}
      </div>

      <FormField
        id={fieldId("guestName")}
        label="Full name"
        error={errors.guestName}
      >
        {({ describedBy, invalid }) => (
          <input
            id={fieldId("guestName")}
            name="guestName"
            type="text"
            autoComplete="name"
            required
            value={form.guestName}
            onChange={(e) => update("guestName", e.target.value)}
            aria-invalid={invalid}
            aria-describedby={describedBy}
            className={inputClassName}
          />
        )}
      </FormField>

      <FormField
        id={fieldId("guestEmail")}
        label="Email address"
        hint="Your booking confirmation will be shown on screen."
        error={errors.guestEmail}
      >
        {({ describedBy, invalid }) => (
          <input
            id={fieldId("guestEmail")}
            name="guestEmail"
            type="email"
            autoComplete="email"
            required
            value={form.guestEmail}
            onChange={(e) => update("guestEmail", e.target.value)}
            aria-invalid={invalid}
            aria-describedby={describedBy}
            className={inputClassName}
          />
        )}
      </FormField>

      <FormField
        id={fieldId("specialRequests")}
        label="Special requests (optional)"
        hint={`${requestsLength}/${MAX_REQUESTS} characters`}
        error={errors.specialRequests}
      >
        {({ describedBy, invalid }) => (
          <textarea
            id={fieldId("specialRequests")}
            name="specialRequests"
            rows={4}
            maxLength={MAX_REQUESTS}
            value={form.specialRequests}
            onChange={(e) => update("specialRequests", e.target.value)}
            aria-invalid={invalid}
            aria-describedby={describedBy}
            className={inputClassName}
          />
        )}
      </FormField>

      <button
        type="submit"
        disabled={busy}
        className="inline-flex w-full items-center justify-center rounded-lg bg-brand px-5 py-3 text-sm font-semibold text-brand-foreground transition-colors hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-70 sm:w-auto"
      >
        {busy ? <Spinner label="Confirming your booking…" /> : "Confirm booking"}
      </button>
    </form>
  );
}
