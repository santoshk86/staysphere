interface AmenityListProps {
  amenities: string[];
  className?: string;
}

/** Amenities rendered as a chip list. Renders nothing when the list is empty. */
export function AmenityList({ amenities, className = "" }: AmenityListProps) {
  if (!amenities.length) return null;

  return (
    <ul className={`flex flex-wrap gap-2 ${className}`}>
      {amenities.map((amenity) => (
        <li
          key={amenity}
          className="rounded-full border border-border bg-background px-3 py-1 text-xs font-medium text-muted"
        >
          {amenity}
        </li>
      ))}
    </ul>
  );
}
