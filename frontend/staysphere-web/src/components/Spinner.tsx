interface SpinnerProps {
  /** Accessible label; announced to screen readers. */
  label?: string;
  className?: string;
}

/** Small loading indicator. Decorative SVG plus a screen-reader label. */
export function Spinner({ label = "Loading", className = "" }: SpinnerProps) {
  return (
    <span className={`inline-flex items-center gap-2 text-sm text-muted ${className}`}>
      <svg
        className="size-4 animate-spin text-brand"
        viewBox="0 0 24 24"
        fill="none"
        aria-hidden="true"
      >
        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 0 1 8-8v4a4 4 0 0 0-4 4H4z" />
      </svg>
      <span>{label}</span>
    </span>
  );
}
