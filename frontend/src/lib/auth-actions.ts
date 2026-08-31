"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { ACCESS_COOKIE, REFRESH_COOKIE } from "@/lib/session";

const API_URL = process.env.API_URL ?? "http://localhost:5178";

type FormState = { error?: string };

interface AuthResult {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAtUtc: string;
}

async function setSessionCookies(auth: AuthResult) {
  const jar = await cookies();
  const secure = process.env.NODE_ENV === "production";
  jar.set(ACCESS_COOKIE, auth.accessToken, {
    httpOnly: true,
    sameSite: "lax",
    secure,
    path: "/",
    expires: new Date(auth.accessTokenExpiresAtUtc),
  });
  jar.set(REFRESH_COOKIE, auth.refreshToken, {
    httpOnly: true,
    sameSite: "lax",
    secure,
    path: "/",
    maxAge: 60 * 60 * 24 * 14,
  });
}

async function post(path: string, body: unknown): Promise<{ ok: boolean; message: string; data?: AuthResult }> {
  const res = await fetch(`${API_URL}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    cache: "no-store",
  });
  const json = (await res.json().catch(() => ({}))) as { message?: string; data?: AuthResult };
  return { ok: res.ok, message: json.message ?? `${res.status}`, data: json.data };
}

export async function loginAction(_prev: FormState, formData: FormData): Promise<FormState> {
  const result = await post("/api/auth/login", {
    email: String(formData.get("email") ?? ""),
    password: String(formData.get("password") ?? ""),
  });
  if (!result.ok || !result.data) return { error: result.message || "Identifiants invalides." };

  await setSessionCookies(result.data);
  redirect(String(formData.get("next") || "/dashboard"));
}

export async function registerAction(_prev: FormState, formData: FormData): Promise<FormState> {
  const password = String(formData.get("password") ?? "");
  const result = await post("/api/auth/register", {
    email: String(formData.get("email") ?? ""),
    password,
    confirmPassword: String(formData.get("confirmPassword") ?? ""),
    firstName: String(formData.get("firstName") ?? ""),
    lastName: String(formData.get("lastName") ?? ""),
  });
  if (!result.ok || !result.data) return { error: result.message || "Inscription impossible." };

  await setSessionCookies(result.data);
  redirect("/profile?welcome=1");
}

export async function logoutAction() {
  const jar = await cookies();
  const refresh = jar.get(REFRESH_COOKIE)?.value;
  if (refresh) {
    await fetch(`${API_URL}/api/auth/logout`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${jar.get(ACCESS_COOKIE)?.value ?? ""}`,
      },
      body: JSON.stringify({ refreshToken: refresh }),
    }).catch(() => undefined);
  }
  jar.delete(ACCESS_COOKIE);
  jar.delete(REFRESH_COOKIE);
  redirect("/");
}

export async function updateProfileAction(_prev: FormState, formData: FormData): Promise<FormState> {
  const jar = await cookies();
  const token = jar.get(ACCESS_COOKIE)?.value;
  if (!token) return { error: "Session expirée." };

  const optional = (name: string) => {
    const v = String(formData.get(name) ?? "").trim();
    return v.length > 0 ? v : null;
  };

  const res = await fetch(`${API_URL}/api/me`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
    body: JSON.stringify({
      firstName: String(formData.get("firstName") ?? ""),
      lastName: String(formData.get("lastName") ?? ""),
      studentId: optional("studentId"),
      universityId: optional("universityId"),
      facultyId: optional("facultyId"),
      departmentId: optional("departmentId"),
      specialtyId: optional("specialtyId"),
      levelId: optional("levelId"),
    }),
    cache: "no-store",
  });
  if (!res.ok) {
    const json = (await res.json().catch(() => ({}))) as { message?: string };
    return { error: json.message ?? "Enregistrement impossible." };
  }

  redirect("/dashboard");
}
