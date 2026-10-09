import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";
import { ACCESS_COOKIE } from "@/lib/session";

const API_URL = process.env.API_URL ?? "http://localhost:5178";

/** Streams a document's preview image for reviewers; the API enforces Classification.Review. */
export async function GET(_request: NextRequest, ctx: { params: Promise<{ id: string }> }) {
  const { id } = await ctx.params;
  if (!/^[0-9a-f-]{36}$/i.test(id)) return new NextResponse(null, { status: 404 });
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) return new NextResponse(null, { status: 401 });

  const res = await fetch(`${API_URL}/api/admin/classification/documents/${id}/preview`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (!res.ok || !res.body) return new NextResponse(null, { status: res.status });
  return new NextResponse(res.body, {
    headers: { "Content-Type": res.headers.get("Content-Type") ?? "image/png", "Cache-Control": "private, no-store" },
  });
}
