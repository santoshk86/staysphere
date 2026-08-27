/**
 * Centralised runtime configuration. The API base URL is the only
 * environment-specific value the frontend needs; it is read once here so no
 * component ever references `process.env` directly.
 */
const rawBaseUrl =
  process.env.NEXT_PUBLIC_API_BASE_URL?.trim() || "http://localhost:7265";

/** API base URL with any trailing slash removed. */
export const API_BASE_URL = rawBaseUrl.replace(/\/+$/, "");
