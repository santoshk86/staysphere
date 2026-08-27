import { roomInitials } from "@/lib/format";

interface RoomImageProps {
  roomType: string;
  /** Aspect / size classes from the caller (e.g. "aspect-[4/3]"). */
  className?: string;
}

/**
 * Image placeholder. The API returns an `imageUrl` pointing at backend static
 * assets, but the brief calls for a placeholder here — this renders a consistent
 * decorative block rather than loading a real photo.
 */
export function RoomImage({ roomType, className = "" }: RoomImageProps) {
  return (
    <div
      role="img"
      aria-label={`${roomType} room photo placeholder`}
      className={`relative flex items-center justify-center overflow-hidden bg-gradient-to-br from-teal-600 to-slate-800 ${className}`}
    >
      <span className="text-3xl font-semibold tracking-wide text-white/90 sm:text-4xl">
        {roomInitials(roomType)}
      </span>
      <span className="absolute bottom-2 right-3 text-[10px] font-medium uppercase tracking-wider text-white/60">
        Photo coming soon
      </span>
    </div>
  );
}
