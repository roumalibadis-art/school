"use server";

import { revalidatePath } from "next/cache";
import { adminSend } from "@/lib/admin";

async function run(method: "POST" | "PUT" | "DELETE", path: string, body: unknown, revalidate: string) {
  await adminSend(method, path, body);
  revalidatePath(revalidate);
}

// --- users ---
export async function setUserActiveAction(formData: FormData) {
  const id = String(formData.get("id"));
  const active = String(formData.get("active")) === "true";
  await run("POST", `/api/admin/users/${id}/${active ? "restore" : "suspend"}`, undefined, `/admin/users/${id}`);
}

export async function grantPremiumAction(formData: FormData) {
  const id = String(formData.get("id"));
  const months = Number(formData.get("months") || 1);
  await run("POST", `/api/admin/users/${id}/premium`, { months }, `/admin/users/${id}`);
}

export async function setRolesAction(formData: FormData) {
  const id = String(formData.get("id"));
  const roles = formData.getAll("roles").map(String);
  await run("PUT", `/api/admin/users/${id}/roles`, { roles }, `/admin/users/${id}`);
}

// --- contributions ---
export async function moderateContributionAction(formData: FormData) {
  const id = String(formData.get("id"));
  const decision = String(formData.get("decision"));
  const note = String(formData.get("note") || "");
  const publishNow = String(formData.get("publishNow")) === "true";
  await run("POST", `/api/admin/contributions/${id}/${decision}`, { note, publishNow }, "/admin/contributions");
}

// --- reports ---
export async function resolveReportAction(formData: FormData) {
  const id = String(formData.get("id"));
  const status = String(formData.get("status"));
  const note = String(formData.get("note") || "");
  await run("POST", `/api/admin/reports/${id}/resolve`, { status, note }, "/admin/reports");
}

// --- payments ---
export async function resolvePaymentAction(formData: FormData) {
  const id = String(formData.get("id"));
  const decision = String(formData.get("decision"));
  const note = String(formData.get("note") || "");
  await run("POST", `/api/admin/payments/${id}/${decision}`, { note }, "/admin/payments");
}

// --- plans ---
export async function savePlanAction(formData: FormData) {
  const id = String(formData.get("id") || "");
  const payload = {
    name: String(formData.get("name")),
    description: String(formData.get("description") || "") || null,
    durationDays: Number(formData.get("durationDays")),
    price: Number(formData.get("price")),
    currency: String(formData.get("currency") || "DZD"),
    features: String(formData.get("features") || "") || null,
    isActive: String(formData.get("isActive")) === "on",
    displayOrder: Number(formData.get("displayOrder") || 0),
  };
  if (id) {
    await run("PUT", `/api/admin/subscription-plans/${id}`, payload, "/admin/plans");
  } else {
    await run("POST", "/api/admin/subscription-plans", payload, "/admin/plans");
  }
}

export async function deletePlanAction(formData: FormData) {
  const id = String(formData.get("id"));
  await run("DELETE", `/api/admin/subscription-plans/${id}`, undefined, "/admin/plans");
}
