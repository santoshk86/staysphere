/**
 * Centralised runtime configuration. The API base URL is the only
 * environment-specific value the frontend needs; it is read once here so no
 * component ever references `process.env` directly.
 *
 * Set `NEXT_PUBLIC_API_BASE_URL` in `.env.local` (see `.env.example`). The
 * fallback below is only used when that variable is missing.
 */
const rawBaseUrl =
  process.env.NEXT_PUBLIC_API_BASE_URL?.trim() || "http://localhost:8080";

/** API base URL with any trailing slash removed. */
export const API_BASE_URL = rawBaseUrl.replace(/\/+$/, "");
