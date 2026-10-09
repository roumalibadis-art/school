import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";
import { ACCESS_COOKIE } from "@/lib/session";

const API_URL = process.env.API_URL ?? "http://localhost:5178";

/**
 * Exchanges the session for a short-lived download ticket and streams the file back.
 * `?inline=1` serves it for in-browser viewing (PRD §30); default is an attachment.
 */
export async function GET(request: NextRequest, ctx: { params: Promise<{ slug: string }> }) {
  // A GET here has side effects (issues a ticket, counts a download, may consume the free allowance), so browser
  // or router prefetching must never reach the API.
  const purpose = `${request.headers.get("purpose") ?? ""} ${request.headers.get("sec-purpose") ?? ""}`.toLowerCase();
  if (request.headers.has("next-router-prefetch") || request.headers.has("rsc") || purpose.includes("prefetch")) {
    return new NextResponse(null, { status: 204, headers: { "Cache-Control": "no-store" } });
  }

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
    const body = (await ticketRes.json().catch(() => ({}))) as { message?: string; errors?: string[] };
    // Free allowance used up: explain it and show the way to earn more, instead of dumping JSON.
    if (ticketRes.status === 403 && body.errors?.includes("contribution_required")) {
      return NextResponse.redirect(new URL("/classify?reason=quota", request.url));
    }
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
