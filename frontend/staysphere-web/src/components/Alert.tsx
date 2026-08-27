import type { ReactNode } from "react";

type Variant = "error" | "info" | "success";

const styles: Record<Variant, string> = {
  error: "border-red-200 bg-red-50 text-red-800",
  info: "border-sky-200 bg-sky-50 text-sky-800",
  success: "border-emerald-200 bg-emerald-50 text-emerald-900",
};

interface AlertProps {
  variant?: Variant;
  title?: string;
  children?: ReactNode;
  className?: string;
}

/** Inline status message for loading/error/empty/conflict states. */
export function Alert({ variant = "info", title, children, className = "" }: AlertProps) {
  return (
    <div
      role={variant === "error" ? "alert" : "status"}
      className={`rounded-lg border px-4 py-3 text-sm ${styles[variant]} ${className}`}
    >
      {title ? <p className="font-semibold">{title}</p> : null}
      {children ? <div className={title ? "mt-1" : ""}>{children}</div> : null}
    </div>
  );
}
