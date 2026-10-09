import type { FullConfig } from "@playwright/test";

const API = `http://localhost:${process.env.API_PORT ?? 5188}`;
export const STUDENT_PASSWORD = "E2e#Passw0rd";
export const studentEmail = (n: number) => `e2e-student-${n}@example.test`;
export const IDP = `http://localhost:${process.env.IDP_PORT ?? 5190}`;

/** A minimal, well-formed one-page PDF (valid xref) so the API accepts and previews it. */
export function samplePdf(text: string): Uint8Array {
  const content = `BT /F1 24 Tf 72 700 Td (${text.replace(/[()\\]/g, "")}) Tj ET`;
  const objects = [
    "<< /Type /Catalog /Pages 2 0 R >>",
    "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
    `<< /Length ${content.length} >>\nstream\n${content}\nendstream`,
    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
  ];
  let out = "%PDF-1.4\n";
  const offsets: number[] = [];
  objects.forEach((o, i) => { offsets.push(out.length); out += `${i + 1} 0 obj\n${o}\nendobj\n`; });
  const xref = out.length;
  out += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  offsets.forEach((o) => { out += `${String(o).padStart(10, "0")} 00000 n \n`; });
  out += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
  return new TextEncoder().encode(out);
}

async function json<T>(res: Response): Promise<T> {
  const body = (await res.json().catch(() => ({}))) as { data?: T; message?: string };
  if (!res.ok) throw new Error(`${res.url} → ${res.status} ${body.message ?? ""}`);
  return body.data as T;
}

async function login(email: string, password: string): Promise<string> {
  const data = await json<{ accessToken: string }>(await fetch(`${API}/api/auth/login`, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email, password }),
  }));
  return data.accessToken;
}

async function upload(token: string, title: string, fields: Record<string, string>) {
  const form = new FormData();
  form.set("File", new Blob([samplePdf(title)], { type: "application/pdf" }), `${title.replace(/\s+/g, "-")}.pdf`);
  form.set("Title", title);
  for (const [k, v] of Object.entries(fields)) form.set(k, v);
  const data = await json<{ document: { id: string; slug: string } }>(await fetch(`${API}/api/documents`, {
    method: "POST", headers: { Authorization: `Bearer ${token}` }, body: form,
  }));
  return data.document;
}

export default async function globalSetup(_config: FullConfig) {
  const admin = await login("admin@example.local", "Admin#2026!");
  const auth = { Authorization: `Bearer ${admin}`, "Content-Type": "application/json" };

  // Classification-friendly settings: no artificial delay, login prompt only, quota off until a test enables it.
  const settings = await json<Record<string, unknown>>(await fetch(`${API}/api/admin/classification/settings`, { headers: auth }));
  await json(await fetch(`${API}/api/admin/classification/settings`, {
    method: "PUT", headers: auth,
    body: JSON.stringify({ ...settings, minSecondsBeforeVote: 0, downloadTriggerEnabled: false, loginTriggerEnabled: true, quotaEnabled: false }),
  }));

  // A second specialty to choose between.
  const departments = await json<{ id: string }[]>(await fetch(`${API}/api/departments?pageSize=5`, { headers: auth }));
  await fetch(`${API}/api/specialties`, {
    method: "POST", headers: auth,
    body: JSON.stringify({ name: "Mathématiques Appliquées", departmentId: departments[0].id, isActive: true }),
  });

  // Four documents with no module → community queue.
  for (const title of ["Algorithmique examen 2024", "Réseaux TD 3", "Bases de données cours", "Compilation corrigé examen"]) {
    await upload(admin, title, { Type: "Other", IsPremium: "false" });
  }

  // One normal published free document for the download/quota scenario.
  const modules = await json<{ id: string }[]>(await fetch(`${API}/api/modules?pageSize=1`, { headers: auth }));
  const published = await upload(admin, "E2E document gratuit", { Type: "Course", ModuleId: modules[0].id, IsPremium: "false" });
  await json(await fetch(`${API}/api/documents/${published.id}/status`, {
    method: "POST", headers: auth, body: JSON.stringify({ status: "Published" }),
  }));
  process.env.E2E_PUBLISHED_SLUG = published.slug;

  // Students (registered through the public endpoint, exactly like real users).
  for (let n = 1; n <= 6; n++) {
    await fetch(`${API}/api/auth/register`, {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        email: studentEmail(n), password: STUDENT_PASSWORD, confirmPassword: STUDENT_PASSWORD,
        firstName: `Etu${n}`, lastName: "E2E",
      }),
    });
  }

  // Remember the slug for specs (workers re-import modules, so use a file).
  const { writeFileSync } = await import("node:fs");
  writeFileSync("e2e/.state.json", JSON.stringify({ publishedSlug: published.slug }));
}
