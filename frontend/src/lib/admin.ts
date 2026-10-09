import { cookies } from "next/headers";
import { ACCESS_COOKIE, getCurrentUser } from "@/lib/session";

const API_URL = process.env.API_URL ?? "http://localhost:5178";

const STAFF_ROLES = ["Admin", "Moderator"];

export async function requireStaff() {
  const user = await getCurrentUser();
  if (!user || !user.roles.some((r) => STAFF_ROLES.includes(r))) {
    return null;
  }
  return user;
}

export function isAdmin(roles: string[]): boolean {
  return roles.includes("Admin");
}

async function token(): Promise<string | null> {
  return (await cookies()).get(ACCESS_COOKIE)?.value ?? null;
}

export async function adminGet<T>(path: string): Promise<T> {
  const t = await token();
  const res = await fetch(`${API_URL}${path}`, {
    headers: { Authorization: `Bearer ${t ?? ""}`, Accept: "application/json" },
    cache: "no-store",
  });
  if (!res.ok) throw new Error(`${path} → ${res.status}`);
  const body = (await res.json()) as { data: T };
  return body.data;
}

export async function adminGetPaged<T>(path: string): Promise<{ items: T[]; total: number; page: number; totalPages: number }> {
  const t = await token();
  const res = await fetch(`${API_URL}${path}`, {
    headers: { Authorization: `Bearer ${t ?? ""}`, Accept: "application/json" },
    cache: "no-store",
  });
  if (!res.ok) throw new Error(`${path} → ${res.status}`);
  const body = (await res.json()) as { data: T[]; pagination?: { total: number; page: number; totalPages: number } };
  return {
    items: body.data ?? [],
    total: body.pagination?.total ?? body.data.length,
    page: body.pagination?.page ?? 1,
    totalPages: body.pagination?.totalPages ?? 1,
  };
}

/** Server-side authed mutation. Returns { ok, message }. */
export async function adminSend(
  method: "POST" | "PUT" | "DELETE",
  path: string,
  body?: unknown,
): Promise<{ ok: boolean; message: string; errors: string[] }> {
  const t = await token();
  const res = await fetch(`${API_URL}${path}`, {
    method,
    headers: { Authorization: `Bearer ${t ?? ""}`, "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
    cache: "no-store",
  });
  const json = (await res.json().catch(() => ({}))) as { message?: string; errors?: string[] };
  return { ok: res.ok, message: json.message ?? `${res.status}`, errors: json.errors ?? [] };
}

export function canManageSettings(permissions: string[]): boolean {
  return permissions.includes("Classification.Settings");
}
