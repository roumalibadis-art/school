import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { ACCESS_COOKIE } from "@/lib/session";

const API_URL = process.env.API_URL ?? "http://localhost:5178";

async function authed(path: string, init?: RequestInit) {
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) return null;
  return fetch(`${API_URL}${path}`, {
    ...init,
    headers: { ...init?.headers, Authorization: `Bearer ${token}` },
    cache: "no-store",
  });
}

export async function GET() {
  const res = await authed("/api/me/notifications?take=15");
  if (!res || !res.ok) return NextResponse.json({ authenticated: false, unreadCount: 0, items: [] });
  const body = (await res.json()) as { data: unknown[] };
  const items = (body.data ?? []) as { isRead: boolean }[];
  return NextResponse.json({
    authenticated: true,
    unreadCount: items.filter((n) => !n.isRead).length,
    items,
  });
}

export async function POST() {
  await authed("/api/me/notifications/read", { method: "POST" });
  return NextResponse.json({ ok: true });
}
