import { cookies } from "next/headers";

export const ACCESS_COOKIE = "usthb_access";
export const REFRESH_COOKIE = "usthb_refresh";

const API_URL = process.env.API_URL ?? "http://localhost:5178";

export interface SessionUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  isPremium: boolean;
  premiumExpiresAt: string | null;
  roles: string[];
  permissions: string[];
  department?: { id: string; name: string; slug: string } | null;
  specialty: { id: string; name: string; slug: string } | null;
  level: { id: string; name: string; slug: string } | null;
}

export async function getAccessToken(): Promise<string | null> {
  return (await cookies()).get(ACCESS_COOKIE)?.value ?? null;
}

/** Fetches the current user from the API using the session cookie. Returns null when unauthenticated. */
export async function getCurrentUser(): Promise<SessionUser | null> {
  const token = await getAccessToken();
  if (!token) return null;

  const res = await fetch(`${API_URL}/api/me`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (!res.ok) return null;

  const body = (await res.json()) as { data: SessionUser };
  return body.data;
}

export function hasProfile(user: SessionUser | null): boolean {
  return !!user?.specialty && !!user?.level;
}
