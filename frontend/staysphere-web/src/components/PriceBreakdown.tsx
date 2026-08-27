import { formatCurrency } from "@/lib/format";

interface PriceBreakdownProps {
  pricePerNight: number;
  nights: number;
  /** Authoritative total from the API when available; otherwise computed. */
  total?: number;
}

/** Nightly rate × nights → total. Used on the booking and confirmation screens. */
export function PriceBreakdown({ pricePerNight, nights, total }: PriceBreakdownProps) {
  const computed = total ?? pricePerNight * nights;

  return (
    <dl className="space-y-2 text-sm">
      <div className="flex justify-between">
        <dt className="text-muted">
          {formatCurrency(pricePerNight)} × {nights} {nights === 1 ? "night" : "nights"}
        </dt>
        <dd className="text-foreground">{formatCurrency(pricePerNight * nights)}</dd>
      </div>
      <div className="flex justify-between border-t border-border pt-2 text-base font-semibold">
        <dt>Total</dt>
        <dd>{formatCurrency(computed)}</dd>
      </div>
    </dl>
  );
}
