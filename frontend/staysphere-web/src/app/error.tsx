"use client";

import { useEffect } from "react";

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div className="mx-auto max-w-md space-y-4 py-12 text-center">
      <h1 className="text-2xl font-semibold text-foreground">Something went wrong</h1>
      <p className="text-sm text-muted">
        An unexpected error occurred. You can try again, or start a new search.
      </p>
      <button
        type="button"
        onClick={reset}
        className="inline-flex items-center justify-center rounded-lg bg-brand px-5 py-2.5 text-sm font-semibold text-brand-foreground hover:bg-teal-800"
      >
        Try again
      </button>
    </div>
  );
}
