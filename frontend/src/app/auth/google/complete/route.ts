import { NextResponse, type NextRequest } from "next/server";
import { ACCESS_COOKIE, REFRESH_COOKIE } from "@/lib/session";

const API_URL = process.env.API_URL ?? "http://localhost:5178";

/** Only same-site absolute paths — never a URL — so this page cannot be used as an open redirect. */
function safePath(path: string | null): string {
  if (!path || path.length > 200 || !path.startsWith("/") || path.startsWith("//") || path.includes("\\")
    || /[\u0000-\u001f]/.test(path)) {
    return "/dashboard";
  }
  return path;
}

/**
 * Landing point after the API's Google callback. Trades the single-use ticket for the normal session cookies
 * (server-to-server, so the tokens never touch a URL) and sends the user on.
 */
export async function GET(request: NextRequest) {
  const ticket = request.nextUrl.searchParams.get("ticket");
  const failure = NextResponse.redirect(new URL("/login?error=google_failed", request.url));
  if (!ticket) return failure;

  const res = await fetch(`${API_URL}/api/auth/google/exchange`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ ticket }),
    cache: "no-store",
  }).catch(() => null);
  if (!res?.ok) {
    const code = res?.status === 403 ? "account_disabled" : "google_failed";
    return NextResponse.redirect(new URL(`/login?error=${code}`, request.url));
  }

  const { data } = (await res.json()) as {
    data: { accessToken: string; refreshToken: string; accessTokenExpiresAtUtc: string };
  };

  const response = NextResponse.redirect(new URL(safePath(request.nextUrl.searchParams.get("returnUrl")), request.url));
  const secure = process.env.NODE_ENV === "production";
  response.cookies.set(ACCESS_COOKIE, data.accessToken, {
    httpOnly: true, sameSite: "lax", secure, path: "/", expires: new Date(data.accessTokenExpiresAtUtc),
  });
  response.cookies.set(REFRESH_COOKIE, data.refreshToken, {
    httpOnly: true, sameSite: "lax", secure, path: "/", maxAge: 60 * 60 * 24 * 14,
  });
  return response;
}
