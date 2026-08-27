import Link from "next/link";

/** App header with a home link. Rendered on every route via the root layout. */
export function SiteHeader() {
  return (
    <header className="border-b border-border bg-surface">
      <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-4 sm:px-6">
        <Link href="/" className="flex items-center gap-2 font-semibold text-foreground">
          <span
            aria-hidden="true"
            className="grid size-8 place-items-center rounded-lg bg-brand text-brand-foreground"
          >
            S
          </span>
          <span className="text-lg">StaySphere</span>
        </Link>
        <Link href="/rooms" className="text-sm font-medium text-brand hover:underline">
          Find a room
        </Link>
      </div>
    </header>
  );
}
