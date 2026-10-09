"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
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

// --- community classification ---
const optional = (formData: FormData, name: string) => {
  const v = String(formData.get(name) ?? "").trim();
  return v.length > 0 ? v : null;
};

export async function verifyClassificationAction(formData: FormData) {
  const id = String(formData.get("id"));
  const result = await adminSend("POST", `/api/admin/classification/documents/${id}/verify`, {
    specialtyId: optional(formData, "specialtyId"),
    departmentId: optional(formData, "departmentId"),
    documentType: optional(formData, "documentType"),
    academicYearId: optional(formData, "academicYearId"),
    sessionId: optional(formData, "sessionId"),
    moduleId: optional(formData, "moduleId"),
    note: optional(formData, "note"),
  });
  revalidatePath(`/admin/classification/${id}`);
  revalidatePath("/admin/classification");
  redirect(`/admin/classification/${id}?${result.ok ? "done=verified" : `error=${encodeURIComponent(result.errors[0] ?? result.message)}`}`);
}

export async function rejectClassificationAction(formData: FormData) {
  const id = String(formData.get("id"));
  const result = await adminSend("POST", `/api/admin/classification/documents/${id}/reject`, { note: optional(formData, "note") });
  revalidatePath("/admin/classification");
  redirect(`/admin/classification/${id}?${result.ok ? "done=rejected" : `error=${encodeURIComponent(result.message)}`}`);
}

export async function reopenClassificationAction(formData: FormData) {
  const id = String(formData.get("id"));
  const result = await adminSend("POST", `/api/admin/classification/documents/${id}/reopen`, { note: optional(formData, "note") });
  revalidatePath("/admin/classification");
  redirect(`/admin/classification/${id}?${result.ok ? "done=reopened" : `error=${encodeURIComponent(result.message)}`}`);
}

export type SettingsState = { ok?: boolean; message?: string };

const INT_FIELDS = [
  "documentsPerTask", "assignmentExpiryHours", "minSecondsBeforeVote", "requiredVoters", "agreementPercent",
  "nonEducationalPercent", "downloadsPerPrompt", "promptSnoozeMinutes", "freeDownloadsPerWindow", "quotaWindowDays",
  "bonusDownloadsPerContribution", "maxBonusPerWindow", "maxRewardedContributionsPerDay", "maxPendingProposalsPerUser",
] as const;
const BOOL_FIELDS = ["loginTriggerEnabled", "downloadTriggerEnabled", "quotaEnabled"] as const;

export async function saveClassificationSettingsAction(_prev: SettingsState, formData: FormData): Promise<SettingsState> {
  const body: Record<string, unknown> = {
    requiredFields: formData.getAll("requiredFields").map(String),
    nonEducationalPolicy: String(formData.get("nonEducationalPolicy") ?? "SendToReview"),
  };
  for (const f of INT_FIELDS) body[f] = Number(formData.get(f));
  for (const f of BOOL_FIELDS) body[f] = formData.get(f) === "on";

  const result = await adminSend("PUT", "/api/admin/classification/settings", body);
  revalidatePath("/admin/classification/settings");
  return result.ok
    ? { ok: true, message: "Paramètres enregistrés." }
    : { ok: false, message: result.errors.length ? result.errors.join(" · ") : result.message };
}

// --- taxonomy proposals ---
export async function proposalDecisionAction(formData: FormData) {
  const id = String(formData.get("id"));
  const decision = String(formData.get("decision"));
  const body: Record<string, unknown> = { note: optional(formData, "note") };
  if (decision === "approve") {
    body.name = optional(formData, "name");
    body.parentId = optional(formData, "parentId");
  } else if (decision === "rename") {
    body.name = String(formData.get("name") ?? "");
  } else if (decision === "merge") {
    body.targetId = optional(formData, "targetId");
    body.targetDocumentType = optional(formData, "targetDocumentType");
  }
  const result = await adminSend("POST", `/api/admin/taxonomy/proposals/${id}/${decision}`, body);
  revalidatePath("/admin/taxonomy");
  if (!result.ok) {
    redirect(`/admin/taxonomy?error=${encodeURIComponent(result.errors[0] ?? result.message)}`);
  }
}
