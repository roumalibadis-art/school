"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { ACCESS_COOKIE } from "@/lib/session";

const API_URL = process.env.API_URL ?? "http://localhost:5178";

export async function subscribeAction(formData: FormData) {
  const planId = String(formData.get("planId") ?? "");
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) redirect(`/login?next=/pricing`);

  const res = await fetch(`${API_URL}/api/subscriptions`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
    body: JSON.stringify({ planId }),
    cache: "no-store",
  });

  if (res.status === 409) redirect("/subscribe?state=already-pending");
  if (!res.ok) redirect("/subscribe?state=error");

  redirect("/subscribe?state=created");
}
