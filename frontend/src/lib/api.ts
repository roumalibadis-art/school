import { cookies } from "next/headers";
import { ACCESS_COOKIE } from "@/lib/session";
import type { ApiEnvelope, Paged } from "@/lib/types";

// Server-side base URL for the .NET API. Never exposed to the browser (browser calls go through
// the /api rewrite in next.config.mjs).
const API_URL = process.env.API_URL ?? "http://localhost:5178";

type Query = Record<string, string | number | boolean | null | undefined>;

function toQueryString(query?: Query): string {
  if (!query) return "";
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== null && value !== undefined && value !== "") {
      params.set(key, String(value));
    }
  }
  const s = params.toString();
  return s ? `?${s}` : "";
}

export class ApiError extends Error {
  constructor(readonly status: number, message: string) {
    super(message);
  }
}

/** Fetches `data` from an API envelope. Returns null on 404. Revalidates every 60s by default. */
export async function apiGet<T>(
  path: string,
  opts: { query?: Query; revalidate?: number } = {},
): Promise<T> {
  const res = await fetch(`${API_URL}${path}${toQueryString(opts.query)}`, {
    headers: { Accept: "application/json" },
    next: { revalidate: opts.revalidate ?? 60 },
  });

  if (!res.ok) {
    let message = `${res.status} ${res.statusText}`;
    try {
      const body = (await res.json()) as { message?: string };
      if (body?.message) message = body.message;
    } catch {
      /* keep the status text */
    }
    throw new ApiError(res.status, message);
  }

  const body = (await res.json()) as ApiEnvelope<T>;
  return body.data;
}

export async function apiGetOrNull<T>(
  path: string,
  opts: { query?: Query; revalidate?: number } = {},
): Promise<T | null> {
  try {
    return await apiGet<T>(path, opts);
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) return null;
    throw err;
  }
}

/** Fetches a paged list, normalising the envelope's `data` + `pagination` into one object. */
export async function apiGetPaged<T>(
  path: string,
  opts: { query?: Query; revalidate?: number } = {},
): Promise<Paged<T>> {
  const res = await fetch(`${API_URL}${path}${toQueryString(opts.query)}`, {
    headers: { Accept: "application/json" },
    next: { revalidate: opts.revalidate ?? 60 },
  });

  if (!res.ok) throw new ApiError(res.status, `${res.status} ${res.statusText}`);

  const body = (await res.json()) as ApiEnvelope<T[]>;
  const p = body.pagination ?? { page: 1, pageSize: body.data.length, total: body.data.length, totalPages: 1 };
  return { items: body.data ?? [], page: p.page, pageSize: p.pageSize, total: p.total, totalPages: p.totalPages };
}

export const previewImageUrl = (slug: string) => `/api/documents/${encodeURIComponent(slug)}/preview`;

/** Server-side authenticated GET using the session cookie. Throws ApiError(401) when signed out. */
export async function authedApiGet<T>(path: string, opts: { query?: Query } = {}): Promise<T> {
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) throw new ApiError(401, "Not authenticated");

  const res = await fetch(`${API_URL}${path}${toQueryString(opts.query)}`, {
    headers: { Accept: "application/json", Authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (!res.ok) {
    let message = `${res.status}`;
    try {
      const body = (await res.json()) as { message?: string };
      if (body?.message) message = body.message;
    } catch {
      /* ignore */
    }
    throw new ApiError(res.status, message);
  }
  const body = (await res.json()) as ApiEnvelope<T>;
  return body.data;
}
