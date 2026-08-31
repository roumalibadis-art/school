import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";
import { ACCESS_COOKIE } from "@/lib/session";

const API_URL = process.env.API_URL ?? "http://localhost:5178";

/**
 * Exchanges the session for a short-lived download ticket and streams the file back.
 * `?inline=1` serves it for in-browser viewing (PRD §30); default is an attachment.
 */
export async function GET(request: NextRequest, ctx: { params: Promise<{ slug: string }> }) {
  const { slug } = await ctx.params;
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) {
    const login = new URL(`/login?next=/documents/${slug}`, request.url);
    return NextResponse.redirect(login);
  }

  const ticketRes = await fetch(`${API_URL}/api/documents/${encodeURIComponent(slug)}/download`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: "no-store",
  });

  if (!ticketRes.ok) {
    const body = (await ticketRes.json().catch(() => ({}))) as { message?: string };
    return NextResponse.json({ message: body.message ?? "Téléchargement refusé" }, { status: ticketRes.status });
  }

  const { data } = (await ticketRes.json()) as { data: { url: string; fileName: string } };
  const target = data.url.startsWith("http") ? data.url : `${API_URL}${data.url}`;

  const fileRes = await fetch(target, { cache: "no-store" });
  if (!fileRes.ok || !fileRes.body) {
    return NextResponse.json({ message: "Fichier indisponible" }, { status: 502 });
  }

  const inline = request.nextUrl.searchParams.get("inline") === "1";
  const headers = new Headers();
  headers.set("Content-Type", fileRes.headers.get("Content-Type") ?? "application/octet-stream");
  headers.set(
    "Content-Disposition",
    `${inline ? "inline" : "attachment"}; filename="${data.fileName.replace(/"/g, "")}"`,
  );
  const length = fileRes.headers.get("Content-Length");
  if (length) headers.set("Content-Length", length);

  return new NextResponse(fileRes.body, { status: 200, headers });
}
