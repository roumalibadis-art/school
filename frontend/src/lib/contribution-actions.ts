"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { ACCESS_COOKIE } from "@/lib/session";

const API_URL = process.env.API_URL ?? "http://localhost:5178";

export async function submitContributionAction(_prev: { error?: string }, formData: FormData): Promise<{ error?: string }> {
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) redirect("/login?next=/contribute");

  const forward = new FormData();
  forward.set("Title", String(formData.get("title") ?? ""));
  forward.set("Type", String(formData.get("type") ?? ""));
  forward.set("ModuleId", String(formData.get("moduleId") ?? ""));
  forward.set("Description", String(formData.get("description") ?? ""));
  const file = formData.get("file");
  if (file instanceof File) forward.set("File", file);

  const res = await fetch(`${API_URL}/api/contributions`, {
    method: "POST",
    headers: { Authorization: `Bearer ${token}` },
    body: forward,
  });

  if (!res.ok) {
    const json = (await res.json().catch(() => ({}))) as { message?: string; errors?: string[] };
    return { error: json.errors?.[0] ?? json.message ?? "Envoi impossible." };
  }

  redirect("/contribute?sent=1");
}
