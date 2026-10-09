import Link from "next/link";
import { notFound } from "next/navigation";
import { Badge, Button, Card } from "@/components/ui";
import { adminGet, adminGetPaged } from "@/lib/admin";
import {
  rejectClassificationAction, reopenClassificationAction, verifyClassificationAction,
} from "@/lib/admin-actions";
import { documentTypeLabel, formatDate, reasonLabels } from "@/lib/format";

interface Vote {
  id: string; userId: string; userEmail: string | null; round: number; decision: string; specialty: string | null;
  department: string | null; documentType: string | null; academicYear: string | null; session: string | null;
  pendingProposals: string[]; agreedWithOutcome: boolean | null; createdAt: string;
}
interface Detail {
  summary: {
    id: string; title: string; slug: string; documentStatus: string; classification: string; verification: string;
    reviewReason: string | null; round: number; votes: number; requiredVoters: number;
  };
  fileName: string; description: string | null; source: string | null; moduleId: string | null;
  specialtyId: string | null; departmentId: string | null; documentType: string | null;
  academicYearId: string | null; sessionId: string | null; hasPreview: boolean;
  votes: Vote[]; history: { action: string; actor: string | null; metadata: string | null; occurredAt: string }[];
}
interface Opt { id: string; name: string }

const DOC_TYPES = ["Course", "TD", "TP", "Exam", "ExamSolution", "Test", "TestSolution", "Exercise", "ExerciseSolution", "Summary", "Other"];

function Select({ name, label, options, value }: { name: string; label: string; options: Opt[]; value?: string | null }) {
  return (
    <label className="block text-sm">
      <span className="mb-1 block font-medium text-ink-muted">{label}</span>
      <select name={name} defaultValue={value ?? ""} className="w-full rounded-md border border-line bg-paper-raised px-3 py-2">
        <option value="">— (voix majoritaires)</option>
        {options.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
      </select>
    </label>
  );
}

export default async function ClassificationDetailPage({
  params, searchParams,
}: { params: Promise<{ id: string }>; searchParams: Promise<{ done?: string; error?: string }> }) {
  const { id } = await params;
  const { done, error } = await searchParams;
  const detail = await adminGet<Detail>(`/api/admin/classification/documents/${id}`).catch(() => null);
  if (!detail) notFound();

  const [specialties, departments, years, sessions, modules] = await Promise.all([
    adminGetPaged<Opt>("/api/specialties?pageSize=100"),
    adminGetPaged<Opt>("/api/departments?pageSize=100"),
    adminGetPaged<Opt>("/api/academic-years?pageSize=100"),
    adminGetPaged<Opt>("/api/sessions?pageSize=100"),
    adminGetPaged<Opt>("/api/modules?pageSize=100"),
  ]);
  const s = detail.summary;

  return (
    <div className="space-y-6">
      <p className="text-sm"><Link href="/admin/classification" className="link">← Retour aux files</Link></p>
      <header>
        <h1 className="text-2xl font-semibold">{s.title}</h1>
        <p className="mt-2 flex flex-wrap gap-2">
          <Badge tone={s.verification === "Verified" ? "brand" : "neutral"}>{s.verification}</Badge>
          <Badge>{s.classification}</Badge>
          <Badge>{s.documentStatus}</Badge>
          <Badge>tour {s.round}</Badge>
          <Badge>{s.votes}/{s.requiredVoters} votes</Badge>
          {s.reviewReason ? <Badge tone="premium">{reasonLabels[s.reviewReason] ?? s.reviewReason}</Badge> : null}
        </p>
      </header>

      {done ? <p role="status" className="rounded-md bg-brand-soft p-3 text-sm text-brand-strong">Action enregistrée ({done}).</p> : null}
      {error ? <p role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-800">{error}</p> : null}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
        <Card className="overflow-hidden">
          {detail.hasPreview ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={`/admin/preview/${id}`} alt={`Aperçu de ${detail.fileName}`} className="max-h-[28rem] w-full bg-paper-sunken object-contain" />
          ) : (
            <p className="p-10 text-center text-sm text-ink-soft">Aperçu indisponible</p>
          )}
          <div className="p-4 text-sm">
            <p className="break-all text-ink-muted">{detail.fileName}</p>
            {detail.description ? <p className="mt-2">{detail.description}</p> : null}
            {detail.source ? <p className="mt-1 text-xs text-ink-soft">Source : {detail.source}</p> : null}
            <p className="mt-2 text-xs text-ink-soft">Type actuel : {documentTypeLabel(detail.documentType ?? "Other")}</p>
          </div>
        </Card>

        <Card className="p-4">
          <h2 className="font-semibold">Décision</h2>
          <form action={verifyClassificationAction} className="mt-3 space-y-3">
            <input type="hidden" name="id" value={id} />
            <p className="text-xs text-ink-soft">
              Laissez les champs vides pour retenir les valeurs les plus votées, ou choisissez les valeurs correctes.
              Le module est requis pour publier le document.
            </p>
            <div className="grid gap-3 sm:grid-cols-2">
              <Select name="specialtyId" label="Spécialité" options={specialties.items} value={detail.specialtyId} />
              <Select name="departmentId" label="Département" options={departments.items} value={detail.departmentId} />
              <label className="block text-sm">
                <span className="mb-1 block font-medium text-ink-muted">Type</span>
                <select name="documentType" defaultValue="" className="w-full rounded-md border border-line bg-paper-raised px-3 py-2">
                  <option value="">— (voix majoritaires)</option>
                  {DOC_TYPES.map((t) => <option key={t} value={t}>{documentTypeLabel(t)}</option>)}
                </select>
              </label>
              <Select name="academicYearId" label="Année" options={years.items} value={detail.academicYearId} />
              <Select name="sessionId" label="Session" options={sessions.items} value={detail.sessionId} />
              <Select name="moduleId" label="Module" options={modules.items} value={detail.moduleId} />
            </div>
            <input name="note" placeholder="Note (journalisée)…" className="w-full rounded-md border border-line bg-paper-raised px-3 py-2 text-sm" />
            <Button type="submit">Valider / corriger</Button>
          </form>

          <div className="mt-5 flex flex-wrap gap-3 border-t border-line pt-4">
            <form action={reopenClassificationAction} className="flex gap-2">
              <input type="hidden" name="id" value={id} />
              <input name="note" placeholder="Raison…" className="rounded-md border border-line px-2 py-1 text-sm" />
              <Button type="submit" variant="secondary">Rouvrir le vote</Button>
            </form>
            <form action={rejectClassificationAction} className="flex gap-2">
              <input type="hidden" name="id" value={id} />
              <input name="note" placeholder="Raison du rejet…" className="rounded-md border border-line px-2 py-1 text-sm" />
              <Button type="submit" variant="secondary">Rejeter</Button>
            </form>
          </div>
        </Card>
      </div>

      <section>
        <h2 className="mb-2 font-semibold">Votes ({detail.votes.length})</h2>
        <div className="overflow-x-auto rounded-lg border border-line bg-paper-raised">
          <table className="w-full min-w-[40rem] text-left text-sm">
            <thead className="bg-paper-sunken text-xs text-ink-soft">
              <tr><th className="p-2">Tour</th><th className="p-2">Votant</th><th className="p-2">Décision</th><th className="p-2">Valeurs</th><th className="p-2">Accord</th><th className="p-2">Date</th></tr>
            </thead>
            <tbody className="divide-y divide-line">
              {detail.votes.map((v) => (
                <tr key={v.id}>
                  <td className="p-2">{v.round}</td>
                  <td className="p-2">{v.userEmail ?? v.userId}</td>
                  <td className="p-2">{v.decision === "NotEducational" ? "Non pédagogique" : "Classé"}</td>
                  <td className="p-2 text-xs">
                    {[v.department, v.specialty, v.documentType && documentTypeLabel(v.documentType), v.academicYear, v.session, ...v.pendingProposals]
                      .filter(Boolean).join(" · ") || "—"}
                  </td>
                  <td className="p-2">{v.agreedWithOutcome === null ? "—" : v.agreedWithOutcome ? "✔" : "✘"}</td>
                  <td className="p-2 text-xs">{formatDate(v.createdAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section>
        <h2 className="mb-2 font-semibold">Historique</h2>
        <ul className="space-y-1 text-sm">
          {detail.history.map((h, i) => (
            <li key={i} className="rounded-md border border-line bg-paper-raised p-2">
              <span className="font-medium">{h.action}</span> · {h.actor ?? "système"} · <span className="text-xs text-ink-soft">{formatDate(h.occurredAt)}</span>
              {h.metadata ? <pre className="mt-1 overflow-x-auto text-xs text-ink-soft">{h.metadata}</pre> : null}
            </li>
          ))}
          {detail.history.length === 0 ? <li className="text-ink-soft">Aucun événement.</li> : null}
        </ul>
      </section>
    </div>
  );
}
