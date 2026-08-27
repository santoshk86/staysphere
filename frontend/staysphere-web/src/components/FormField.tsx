import type { ReactNode } from "react";

interface FieldRenderArgs {
  /** Wire this onto the control's `aria-describedby`. */
  describedBy: string | undefined;
  /** Wire this onto the control's `aria-invalid`. */
  invalid: boolean;
}

interface FormFieldProps {
  id: string;
  label: string;
  error?: string;
  hint?: string;
  /** Renders the control (input / select / textarea) with a11y wiring provided. */
  children: (args: FieldRenderArgs) => ReactNode;
}

/**
 * Label + hint + error layout for a single form control. Provides the
 * `aria-describedby` / `aria-invalid` wiring so every control is consistent.
 */
export function FormField({ id, label, error, hint, children }: FormFieldProps) {
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(" ") || undefined;

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-medium text-foreground">
        {label}
      </label>
      {hint ? (
        <p id={hintId} className="text-xs text-muted">
          {hint}
        </p>
      ) : null}
      {children({ describedBy, invalid: Boolean(error) })}
      {error ? (
        <p id={errorId} role="alert" className="text-xs font-medium text-red-700">
          {error}
        </p>
      ) : null}
    </div>
  );
}

/** Shared input styling so text/date/number controls look identical. */
export const inputClassName =
  "w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm text-foreground " +
  "placeholder:text-muted/70 focus-visible:border-brand aria-invalid:border-red-400";
