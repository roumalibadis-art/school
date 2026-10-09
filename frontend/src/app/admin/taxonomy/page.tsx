import Link from "next/link";
import { Badge, Button, Card, EmptyState, Pagination } from "@/components/ui";
import { adminGetPaged } from "@/lib/admin";
import { proposalDecisionAction } from "@/lib/admin-actions";
import { documentTypeLabel, formatDate } from "@/lib/format";

interface Proposal {
  id: string; category: string; value: string; status: string; parentId: string | null; parentName: string | null;
  documentId: string | null; documentTitle: string | null; submittedByEmail: string | null; submittedAt: string;
  reviewedAt: string | null; adminNote: string | null; resolvedName: string | null; approvedName: string | null; voteCount: number;
}
interface Opt { id: string; name: string }

const categoryLabels: Record<string, string> = {
  Specialty: "Spécialité", Department: "Département", DocumentType: "Type de document",
  AcademicYear: "Année universitaire", Session: "Session",
};
const targetPath: Record<string, string> = {
  Specialty: "specialties", Department: "departments", AcademicYear: "academic-years", Session: "sessions",
};
const parentPath: Record<string, string> = { Specialty: "departments", Department: "faculties" };
const DOC_TYPES = ["Course", "TD", "TP", "Exam", "ExamSolution", "Test", "TestSolution", "Exercise", "ExerciseSolution", "Summary", "Other"];

const field = "w-full rounded-md border border-line bg-paper-raised px-2 py-1.5 text-sm";

export default async function TaxonomyPage({
  searchParams,
}: { searchParams: Promise<{ status?: string; category?: string; page?: string; error?: string }> }) {
  const { status = "Pending", category = "", page = "1", error } = await searchParams;
  const data = await adminGetPaged<Proposal>(
    `/api/admin/taxonomy/proposals?status=${status}&category=${category}&page=${Number(page) || 1}&pageSize=20`,
  );

  // Option lists are only fetched for the categories actually on screen.
  const cats = [...new Set(data.items.map((p) => p.category))];
  const targets: Record<string, Opt[]> = {};
  const parents: Record<string, Opt[]> = {};
  await Promise.all(cats.flatMap((c) => [
    targetPath[c] ? adminGetPaged<Opt>(`/api/${targetPath[c]}?pageSize=100`).then((r) => { targets[c] = r.items; }) : Promise.resolve(),
    parentPath[c] ? adminGetPaged<Opt>(`/api/${parentPath[c]}?pageSize=100`).then((r) => { parents[c] = r.items; }) : Promise.resolve(),
  ]));

  const href = (p: number) => `/admin/taxonomy?status=${status}&category=${category}&page=${p}`;

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Valeurs proposées par les utilisateurs</h1>
      <p className="max-w-prose text-sm text-ink-muted">
        Une valeur proposée reste invisible pour tous (y compris dans la recherche) tant qu’elle n’est pas approuvée.
      </p>

      {error ? <p role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-800">{error}</p> : null}

      <form className="flex flex-wrap gap-2" role="search">
        <select name="status" defaultValue={status} aria-label="Statut" className={`${field} w-auto`}>
          {["Pending", "Approved", "Merged", "Rejected", "All"].map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <select name="category" defaultValue={category} aria-label="Catégorie" className={`${field} w-auto`}>
          <option value="">Toutes catégories</option>
          {Object.entries(categoryLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
        <Button type="submit" variant="secondary">Filtrer</Button>
      </form>

      <p className="text-sm text-ink-muted">{data.total} proposition(s)</p>

      {data.items.length === 0 ? <EmptyState>Aucune proposition.</EmptyState> : data.items.map((p) => (
        <Card key={p.id} className="p-4">
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone="brand">{categoryLabels[p.category] ?? p.category}</Badge>
            <Badge tone={p.status === "Pending" ? "premium" : "neutral"}>{p.status}</Badge>
            <span className="text-xs text-ink-soft">
              {p.submittedByEmail ?? "?"} · {formatDate(p.submittedAt)} · {p.voteCount} vote(s)
            </span>
          </div>
          <h2 className="mt-1 text-lg font-semibold">« {p.approvedName ?? p.value} »</h2>
          {p.approvedName && p.approvedName !== p.value ? <p className="text-xs text-ink-soft">Saisi : {p.value}</p> : null}
          {p.parentName ? <p className="text-sm text-ink-muted">Rattaché à : {p.parentName}</p> : null}
          {p.documentId ? (
            <p className="text-sm">Document : <Link href={`/admin/classification/${p.documentId}`} className="link">{p.documentTitle}</Link></p>
          ) : null}
          {p.status !== "Pending" ? (
            <p className="mt-1 text-sm text-ink-muted">
              {p.resolvedName ? `→ ${p.resolvedName}. ` : ""}{p.adminNote ?? ""} {p.reviewedAt ? `(${formatDate(p.reviewedAt)})` : ""}
            </p>
          ) : (
            <div className="mt-3 grid gap-3 md:grid-cols-2">
              {p.category !== "DocumentType" ? (
                <form action={proposalDecisionAction} className="space-y-2 rounded-md border border-line p-3">
                  <input type="hidden" name="id" value={p.id} />
                  <input type="hidden" name="decision" value="approve" />
                  <p className="text-sm font-medium">Approuver (créer la valeur)</p>
                  <input name="name" defaultValue={p.approvedName ?? p.value} aria-label="Nom final" className={field} />
                  {parents[p.category] ? (
                    <select name="parentId" defaultValue={p.parentId ?? ""} aria-label={p.category === "Specialty" ? "Département" : "Faculté"} className={field}>
                      <option value="">{p.category === "Specialty" ? "— Département —" : "— Faculté —"}</option>
                      {parents[p.category].map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
                    </select>
                  ) : null}
                  <input name="note" placeholder="Note" className={field} />
                  <Button type="submit">Approuver</Button>
                </form>
              ) : (
                <p className="rounded-md border border-line p-3 text-sm text-ink-muted">
                  Les types de documents forment une liste fixe : fusionnez cette proposition dans un type existant ou rejetez-la.
                </p>
              )}

              <div className="space-y-3">
                <form action={proposalDecisionAction} className="space-y-2 rounded-md border border-line p-3">
                  <input type="hidden" name="id" value={p.id} />
                  <input type="hidden" name="decision" value="merge" />
                  <p className="text-sm font-medium">Fusionner dans une valeur existante</p>
                  {p.category === "DocumentType" ? (
                    <select name="targetDocumentType" aria-label="Type existant" className={field} required>
                      <option value="">— Type —</option>
                      {DOC_TYPES.map((t) => <option key={t} value={t}>{documentTypeLabel(t)}</option>)}
                    </select>
                  ) : (
                    <select name="targetId" aria-label="Valeur existante" className={field} required>
                      <option value="">— Valeur —</option>
                      {(targets[p.category] ?? []).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
                    </select>
                  )}
                  <Button type="submit" variant="secondary">Fusionner</Button>
                </form>

                <div className="flex flex-wrap gap-3">
                  <form action={proposalDecisionAction} className="flex gap-2">
                    <input type="hidden" name="id" value={p.id} />
                    <input type="hidden" name="decision" value="rename" />
                    <input name="name" defaultValue={p.approvedName ?? p.value} aria-label="Nouveau nom" className={field} />
                    <Button type="submit" variant="secondary">Renommer</Button>
                  </form>
                  <form action={proposalDecisionAction} className="flex gap-2">
                    <input type="hidden" name="id" value={p.id} />
                    <input type="hidden" name="decision" value="reject" />
                    <input name="note" placeholder="Raison" aria-label="Raison du rejet" className={field} />
                    <Button type="submit" variant="secondary">Rejeter</Button>
                  </form>
                </div>
              </div>
            </div>
          )}
        </Card>
      ))}
      <Pagination page={data.page} totalPages={data.totalPages} hrefFor={href} />
    </div>
  );
}
