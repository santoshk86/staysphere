import Link from "next/link";

export default function NotFound() {
  return (
    <div className="mx-auto max-w-md space-y-4 py-12 text-center">
      <h1 className="text-2xl font-semibold text-foreground">Page not found</h1>
      <p className="text-sm text-muted">
        The room or booking you&apos;re looking for doesn&apos;t exist or is no longer
        available.
      </p>
      <Link
        href="/rooms"
        className="inline-flex items-center justify-center rounded-lg bg-brand px-5 py-2.5 text-sm font-semibold text-brand-foreground hover:bg-teal-800"
      >
        Search for a room
      </Link>
    </div>
  );
}
