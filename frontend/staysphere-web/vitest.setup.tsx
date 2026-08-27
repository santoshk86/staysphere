import "@testing-library/jest-dom/vitest";

import type { AnchorHTMLAttributes, ReactNode } from "react";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";

// React Testing Library does not auto-clean when Vitest globals are used with a
// custom setup file, so do it explicitly to keep tests isolated.
afterEach(() => {
  cleanup();
});

type MockLinkProps = {
  href: string | { pathname?: string };
  children?: ReactNode;
} & Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "href">;

// next/link renders an anchor in production; a plain <a> is the accurate,
// framework-free stand-in for component tests (keeps getByRole("link") working).
vi.mock("next/link", () => ({
  __esModule: true,
  default: ({ href, children, ...rest }: MockLinkProps) => (
    <a href={typeof href === "string" ? href : (href?.pathname ?? "#")} {...rest}>
      {children}
    </a>
  ),
}));
