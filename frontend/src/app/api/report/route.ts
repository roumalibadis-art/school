import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";
import { ACCESS_COOKIE } from "@/lib/session";

const API_URL = process.env.API_URL ?? "http://localhost:5178";

export async function POST(request: NextRequest) {
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) return NextResponse.json({ message: "Connectez-vous pour signaler." }, { status: 401 });

  const { slug, reason, comment } = (await request.json()) as { slug: string; reason: string; comment?: string };

  const res = await fetch(`${API_URL}/api/documents/${encodeURIComponent(slug)}/report`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
    body: JSON.stringify({ reason, comment }),
    cache: "no-store",
  });

  return NextResponse.json(await res.json().catch(() => ({})), { status: res.status });
}
