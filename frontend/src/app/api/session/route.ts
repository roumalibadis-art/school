import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { ACCESS_COOKIE } from "@/lib/session";

const API_URL = process.env.API_URL ?? "http://localhost:5178";

export async function GET() {
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) return NextResponse.json({ authenticated: false });

  const res = await fetch(`${API_URL}/api/me`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (!res.ok) return NextResponse.json({ authenticated: false });

  const body = (await res.json()) as {
    data: { firstName: string; specialty: unknown; level: unknown; roles: string[] };
  };
  return NextResponse.json({
    authenticated: true,
    firstName: body.data.firstName,
    hasProfile: !!body.data.specialty && !!body.data.level,
    isStaff: (body.data.roles ?? []).some((r) => r === "Admin" || r === "Moderator"),
  });
}
