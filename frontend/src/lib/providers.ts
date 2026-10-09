const API_URL = process.env.API_URL ?? "http://localhost:5178";

/** Is "Sign in with Google" configured on the server? Fails closed so we never render a dead button. */
export async function googleEnabled(): Promise<boolean> {
  try {
    const res = await fetch(`${API_URL}/api/auth/providers`, { next: { revalidate: 60 } });
    if (!res.ok) return false;
    return ((await res.json()) as { data?: { google?: boolean } }).data?.google === true;
  } catch {
    return false;
  }
}
