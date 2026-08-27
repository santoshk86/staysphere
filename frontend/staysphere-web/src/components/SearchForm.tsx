"use client";

import { useRouter } from "next/navigation";
import { useId, useState } from "react";
import { FormField, inputClassName } from "@/components/FormField";
import { MAX_GUESTS, todayIso, validateSearch } from "@/lib/validation";

interface SearchFormProps {
  defaultCheckIn?: string;
  defaultCheckOut?: string;
  defaultGuests?: string;
  /** Compact layout for the results page toolbar. */
  compact?: boolean;
}

export function SearchForm({
  defaultCheckIn = "",
  defaultCheckOut = "",
  defaultGuests = "2",
  compact = false,
}: SearchFormProps) {
  const router = useRouter();
  const prefix = useId();
  const [values, setValues] = useState({
    checkIn: defaultCheckIn,
    checkOut: defaultCheckOut,
    guests: defaultGuests,
  });
  const [errors, setErrors] = useState<Record<string, string>>({});

  const fieldId = (name: string) => `${prefix}-${name}`;
  const today = todayIso();

  function update(name: keyof typeof values, value: string) {
    setValues((prev) => ({ ...prev, [name]: value }));
    setErrors((prev) => {
      if (!prev[name]) return prev;
      const next = { ...prev };
      delete next[name];
      return next;
    });
  }

  function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    const result = validateSearch(values);
    if (!result.ok) {
      setErrors(result.errors);
      return;
    }
    const { checkIn, checkOut, guests } = result.criteria;
    router.push(
      `/rooms?checkIn=${checkIn}&checkOut=${checkOut}&guests=${guests}`,
    );
  }

  return (
    <form
      onSubmit={handleSubmit}
      noValidate
      aria-label="Search for available rooms"
      className={`grid gap-4 ${compact ? "sm:grid-cols-[1fr_1fr_auto_auto] sm:items-start" : "sm:grid-cols-2 lg:grid-cols-[1fr_1fr_auto_auto] lg:items-start"}`}
    >
      <FormField id={fieldId("checkIn")} label="Check-in" error={errors.checkIn}>
        {({ describedBy, invalid }) => (
          <input
            id={fieldId("checkIn")}
            type="date"
            required
            min={today}
            value={values.checkIn}
            onChange={(e) => update("checkIn", e.target.value)}
            aria-invalid={invalid}
            aria-describedby={describedBy}
            className={inputClassName}
          />
        )}
      </FormField>

      <FormField id={fieldId("checkOut")} label="Check-out" error={errors.checkOut}>
        {({ describedBy, invalid }) => (
          <input
            id={fieldId("checkOut")}
            type="date"
            required
            min={values.checkIn || today}
            value={values.checkOut}
            onChange={(e) => update("checkOut", e.target.value)}
            aria-invalid={invalid}
            aria-describedby={describedBy}
            className={inputClassName}
          />
        )}
      </FormField>

      <FormField id={fieldId("guests")} label="Guests" error={errors.guests}>
        {({ describedBy, invalid }) => (
          <input
            id={fieldId("guests")}
            type="number"
            inputMode="numeric"
            required
            min={1}
            max={MAX_GUESTS}
            value={values.guests}
            onChange={(e) => update("guests", e.target.value)}
            aria-invalid={invalid}
            aria-describedby={describedBy}
            className={`${inputClassName} sm:w-24`}
          />
        )}
      </FormField>

      {/*
        Spacer label keeps the button aligned with the inputs (not the labels)
        once the grid is horizontal. The row is `items-start`, so a validation
        message under a field grows only its own cell and never shifts the
        other controls.
      */}
      <div className="flex flex-col gap-1.5">
        <span aria-hidden="true" className="hidden select-none text-sm font-medium sm:block">
          &nbsp;
        </span>
        <button
          type="submit"
          className="h-[38px] w-full rounded-lg bg-brand px-5 text-sm font-semibold text-brand-foreground transition-colors hover:bg-teal-800 sm:w-auto"
        >
          Search rooms
        </button>
      </div>
    </form>
  );
}
