import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";
import { ACCESS_COOKIE } from "@/lib/session";

const API_URL = process.env.API_URL ?? "http://localhost:5178";

async function forward(method: "POST" | "DELETE", request: NextRequest) {
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) return NextResponse.json({ message: "Non authentifié" }, { status: 401 });

  const { kind, entityId } = (await request.json()) as { kind: string; entityId: string };

  const url =
    method === "POST"
      ? `${API_URL}/api/favorites`
      : `${API_URL}/api/favorites?kind=${encodeURIComponent(kind)}&entityId=${encodeURIComponent(entityId)}`;

  const res = await fetch(url, {
    method,
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
    body: method === "POST" ? JSON.stringify({ kind, entityId }) : undefined,
    cache: "no-store",
  });

  return NextResponse.json(await res.json().catch(() => ({})), { status: res.status });
}

export const POST = (request: NextRequest) => forward("POST", request);
export const DELETE = (request: NextRequest) => forward("DELETE", request);

export async function GET(request: NextRequest) {
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) return NextResponse.json({ authenticated: false, favorited: false });

  const res = await fetch(`${API_URL}/api/favorites`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (!res.ok) return NextResponse.json({ authenticated: true, favorited: false });

  const body = (await res.json()) as { data: { kind: string; entityId: string }[] };
  const kind = request.nextUrl.searchParams.get("kind");
  const entityId = request.nextUrl.searchParams.get("entityId");
  const favorited = (body.data ?? []).some((f) => f.kind === kind && f.entityId === entityId);

  return NextResponse.json({ authenticated: true, favorited });
}
