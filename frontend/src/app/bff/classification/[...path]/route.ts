import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";
import { ACCESS_COOKIE } from "@/lib/session";

const API_URL = process.env.API_URL ?? "http://localhost:5178";

/**
 * Same-origin gateway for the classification UI. The browser never sees the access token (it lives in an
 * HttpOnly cookie); this handler adds it. Only the routes below are reachable — never a free-form proxy.
 */
const ALLOWED: Array<{ method: string; pattern: RegExp }> = [
  { method: "GET", pattern: /^prompt$/ },
  { method: "POST", pattern: /^prompt\/ack$/ },
  { method: "POST", pattern: /^tasks\/next$/ },
  { method: "POST", pattern: /^assignments\/[0-9a-f-]{36}\/(vote|skip)$/ },
  { method: "GET", pattern: /^assignments\/[0-9a-f-]{36}\/preview$/ },
  { method: "GET", pattern: /^options$/ },
  { method: "GET", pattern: /^me$/ },
  { method: "POST", pattern: /^proposals$/ },
  { method: "GET", pattern: /^proposals\/similar$/ },
];

async function handle(method: "GET" | "POST", request: NextRequest, ctx: { params: Promise<{ path: string[] }> }) {
  const { path } = await ctx.params;
  const joined = path.join("/");
  if (!ALLOWED.some((a) => a.method === method && a.pattern.test(joined))) {
    return NextResponse.json({ success: false, message: "Not found" }, { status: 404 });
  }

  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) return NextResponse.json({ success: false, message: "Session expirée." }, { status: 401 });

  // CSRF guard on top of SameSite=Lax cookies: state-changing calls must carry a header that a cross-site
  // form or <img> cannot set.
  if (method === "POST" && request.headers.get("x-requested-with") !== "usthb") {
    return NextResponse.json({ success: false, message: "Requête refusée." }, { status: 403 });
  }

  const res = await fetch(`${API_URL}/api/classification/${joined}${request.nextUrl.search}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(method === "POST" ? { "Content-Type": "application/json" } : {}),
    },
    body: method === "POST" ? await request.text() : undefined,
    cache: "no-store",
  });

  if (joined.endsWith("/preview")) {
    if (!res.ok || !res.body) return new NextResponse(null, { status: res.status });
    return new NextResponse(res.body, {
      status: 200,
      headers: {
        "Content-Type": res.headers.get("Content-Type") ?? "image/png",
        "Cache-Control": "private, no-store",
      },
    });
  }

  return new NextResponse(await res.text(), {
    status: res.status,
    headers: { "Content-Type": "application/json", "Cache-Control": "no-store" },
  });
}

export const GET = (request: NextRequest, ctx: { params: Promise<{ path: string[] }> }) => handle("GET", request, ctx);
export const POST = (request: NextRequest, ctx: { params: Promise<{ path: string[] }> }) => handle("POST", request, ctx);
