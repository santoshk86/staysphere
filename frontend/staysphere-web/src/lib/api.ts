import { API_BASE_URL } from "./config";
import type {
  ApiErrorBody,
  CreateReservationInput,
  ReservationConfirmation,
  Room,
  SearchCriteria,
} from "./types";

/**
 * Single error type for every API failure. Carries the parsed error envelope
 * so callers can branch on `status` (e.g. 409 conflict) without re-parsing.
 */
export class ApiError extends Error {
  readonly status: number;
  /** Envelope `error` code, e.g. "BookingConflict", "NotFound", or "Network". */
  readonly code: string;
  /** Field-level validation messages, present only for 400 ValidationFailed. */
  readonly fieldErrors?: Record<string, string[]>;
  readonly traceId?: string;

  constructor(status: number, code: string, message: string, body?: ApiErrorBody) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
    this.fieldErrors = body?.errors;
    this.traceId = body?.traceId;
  }

  get isNotFound() {
    return this.status === 404;
  }

  get isConflict() {
    return this.status === 409;
  }

  get isValidation() {
    return this.status === 400;
  }

  get isNetwork() {
    return this.status === 0;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const url = `${API_BASE_URL}${path}`;

  let response: Response;
  try {
    response = await fetch(url, {
      // Availability and reservations are volatile: never serve a cached copy.
      cache: "no-store",
      headers: { Accept: "application/json", ...(init?.headers ?? {}) },
      ...init,
    });
  } catch {
    throw new ApiError(
      0,
      "Network",
      "Could not reach the StaySphere service. Check your connection and try again.",
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  const payload = text ? safeJsonParse(text) : undefined;

  if (!response.ok) {
    const body = (payload ?? undefined) as ApiErrorBody | undefined;
    throw new ApiError(
      response.status,
      body?.error ?? "ServerError",
      body?.message ?? `Request failed with status ${response.status}.`,
      body,
    );
  }

  return payload as T;
}

function safeJsonParse(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}

function toQuery(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "") {
      search.set(key, String(value));
    }
  }
  const qs = search.toString();
  return qs ? `?${qs}` : "";
}

/** `GET /api/rooms/search` — available physical rooms for the whole range. */
export function searchRooms(criteria: SearchCriteria): Promise<Room[]> {
  return request<Room[]>(
    `/api/rooms/search${toQuery({
      checkIn: criteria.checkIn,
      checkOut: criteria.checkOut,
      guests: criteria.guests,
    })}`,
  );
}

/** `GET /api/rooms/{roomId}` — full details for one room. */
export function getRoom(roomId: number | string): Promise<Room> {
  return request<Room>(`/api/rooms/${encodeURIComponent(String(roomId))}`);
}

/** `POST /api/reservations` — authoritative availability re-check happens here. */
export function createReservation(
  input: CreateReservationInput,
): Promise<ReservationConfirmation> {
  return request<ReservationConfirmation>("/api/reservations", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input),
  });
}

/** `GET /api/reservations/{reference}` — retrieve a confirmation. */
export function getReservation(
  reference: string,
): Promise<ReservationConfirmation> {
  return request<ReservationConfirmation>(
    `/api/reservations/${encodeURIComponent(reference)}`,
  );
}

/** Flatten the envelope's field errors to one message per field for form display. */
export function firstFieldErrors(
  fieldErrors: Record<string, string[]> | undefined,
): Record<string, string> {
  const result: Record<string, string> = {};
  if (!fieldErrors) return result;
  for (const [field, messages] of Object.entries(fieldErrors)) {
    if (messages?.length) {
      // API keys are already camelCase (e.g. "guestEmail"); lowercase the first
      // letter defensively in case model-binding returns "GuestEmail".
      result[field.charAt(0).toLowerCase() + field.slice(1)] = messages[0];
    }
  }
  return result;
}
