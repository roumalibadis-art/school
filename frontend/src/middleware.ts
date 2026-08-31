import { NextResponse, type NextRequest } from "next/server";
import { ACCESS_COOKIE, REFRESH_COOKIE } from "@/lib/session";

const API_URL = process.env.API_URL ?? "http://localhost:5178";
const PROTECTED = ["/dashboard", "/favorites", "/profile", "/subscribe"];

function isExpiringSoon(jwt: string): boolean {
  try {
    const payload = JSON.parse(atob(jwt.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
    return typeof payload.exp === "number" && payload.exp * 1000 - Date.now() < 90_000;
  } catch {
    return true;
  }
}

export async function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const needsAuth = PROTECTED.some((p) => pathname === p || pathname.startsWith(`${p}/`));

  const access = request.cookies.get(ACCESS_COOKIE)?.value;
  const refresh = request.cookies.get(REFRESH_COOKIE)?.value;

  // Refresh a near-expired access token so server components see a fresh one.
  if (access && refresh && isExpiringSoon(access)) {
    const res = await fetch(`${API_URL}/api/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken: refresh }),
    }).catch(() => null);

    if (res?.ok) {
      const body = (await res.json()) as { data: { accessToken: string; refreshToken: string; accessTokenExpiresAtUtc: string } };
      const next = NextResponse.next();
      const secure = process.env.NODE_ENV === "production";
      next.cookies.set(ACCESS_COOKIE, body.data.accessToken, {
        httpOnly: true, sameSite: "lax", secure, path: "/", expires: new Date(body.data.accessTokenExpiresAtUtc),
      });
      next.cookies.set(REFRESH_COOKIE, body.data.refreshToken, {
        httpOnly: true, sameSite: "lax", secure, path: "/", maxAge: 60 * 60 * 24 * 14,
      });
      return next;
    }

    if (needsAuth) {
      const login = new URL("/login", request.url);
      login.searchParams.set("next", pathname);
      const redirectResponse = NextResponse.redirect(login);
      redirectResponse.cookies.delete(ACCESS_COOKIE);
      redirectResponse.cookies.delete(REFRESH_COOKIE);
      return redirectResponse;
    }
  }

  if (needsAuth && !access && !refresh) {
    const login = new URL("/login", request.url);
    login.searchParams.set("next", pathname);
    return NextResponse.redirect(login);
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/dashboard/:path*", "/favorites/:path*", "/profile/:path*", "/subscribe/:path*"],
};
