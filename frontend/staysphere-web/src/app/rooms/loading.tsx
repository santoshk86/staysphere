import { Spinner } from "@/components/Spinner";

export default function Loading() {
  return (
    <div className="py-12">
      <Spinner label="Loading rooms…" />
    </div>
  );
}
