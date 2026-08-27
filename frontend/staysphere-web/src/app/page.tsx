import { SearchForm } from "@/components/SearchForm";

const steps = [
  { title: "Search", body: "Pick your dates and party size to see rooms that are actually free." },
  { title: "Choose", body: "Compare room types, prices, and amenities, then open the details." },
  { title: "Book", body: "Enter your details and get an instant booking reference." },
];

export default function HomePage() {
  return (
    <div className="space-y-12">
      <section className="space-y-4">
        <h1 className="text-3xl font-semibold tracking-tight text-foreground sm:text-4xl">
          Find your room at StaySphere
        </h1>
        <p className="max-w-2xl text-muted">
          Real-time availability for every date range. Search now and book in under a
          minute — no account needed.
        </p>

        <div className="rounded-xl border border-border bg-surface p-4 shadow-sm sm:p-6">
          <SearchForm />
        </div>
      </section>

      <section aria-labelledby="how-it-works" className="space-y-4">
        <h2 id="how-it-works" className="text-lg font-semibold text-foreground">
          How it works
        </h2>
        <ol className="grid gap-4 sm:grid-cols-3">
          {steps.map((step, index) => (
            <li key={step.title} className="rounded-xl border border-border bg-surface p-4">
              <span className="grid size-7 place-items-center rounded-full bg-brand text-sm font-semibold text-brand-foreground">
                {index + 1}
              </span>
              <h3 className="mt-3 font-semibold text-foreground">{step.title}</h3>
              <p className="mt-1 text-sm text-muted">{step.body}</p>
            </li>
          ))}
        </ol>
      </section>
    </div>
  );
}
