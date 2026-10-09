"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Badge, Button, Card, LinkButton } from "@/components/ui";
import { documentTypeLabel, formatBytes } from "@/lib/format";
import type {
  ClassificationOptions, ClassificationTask, ClassifyPrompt, QuotaStatus, TaskItem, VoteResult,
} from "@/lib/types";
import { Combobox, type AddOutcome, type ComboOption } from "./combobox";

type Category = "Specialty" | "Department" | "DocumentType" | "AcademicYear" | "Session";

interface Defaults {
  departmentId: string | null;
  specialtyId: string | null;
}

interface ApiResult<T> {
  ok: boolean;
  status: number;
  data: T | null;
  message: string;
  errors: string[];
}

async function call<T>(path: string, method: "GET" | "POST" = "GET", body?: unknown): Promise<ApiResult<T>> {
  try {
    const res = await fetch(`/bff/classification/${path}`, {
      method,
      headers: { "Content-Type": "application/json", "x-requested-with": "usthb" },
      body: body === undefined ? undefined : JSON.stringify(body),
      cache: "no-store",
    });
    const json = (await res.json().catch(() => ({}))) as { data?: T; message?: string; errors?: string[] };
    return { ok: res.ok, status: res.status, data: json.data ?? null, message: json.message ?? "", errors: json.errors ?? [] };
  } catch {
    return { ok: false, status: 0, data: null, message: "Connexion impossible. Vérifiez votre réseau et réessayez.", errors: [] };
  }
}

/** Filename heuristics for a *suggested* document type — always shown as a suggestion the user can change. */
function guessType(fileName: string, title: string): string | null {
  const t = `${fileName} ${title}`.toLowerCase();
  const solution = /corrig|solution|correction/.test(t);
  if (solution && /exam/.test(t)) return "ExamSolution";
  if (solution && /(contr|test|\bds\b)/.test(t)) return "TestSolution";
  if (solution && /(td|exo|serie|série)/.test(t)) return "ExerciseSolution";
  if (/exam|final/.test(t)) return "Exam";
  if (/(contr[oô]le|test|\bds\b|\bcc\b)/.test(t)) return "Test";
  if (/\btp\b/.test(t)) return "TP";
  if (/(\btd\b|exo|serie|série)/.test(t)) return "TD";
  if (/(cours|chap|le[cç]on|polycop)/.test(t)) return "Course";
  if (/(r[eé]sum[eé]|synth[eè]se|fiche)/.test(t)) return "Summary";
  return null;
}

interface Form {
  departmentId: string | null;
  specialtyId: string | null;
  typeId: string | null;
  yearId: string | null;
  sessionId: string | null;
  typeSuggested: boolean;
  placeSuggested: boolean;
}

const emptyForm: Form = {
  departmentId: null, specialtyId: null, typeId: null, yearId: null, sessionId: null,
  typeSuggested: false, placeSuggested: false,
};

export function ClassifyWorkspace({ defaults, reason }: { defaults: Defaults; reason?: string }) {
  const [phase, setPhase] = useState<"loading" | "empty" | "working" | "done" | "error">("loading");
  const [task, setTask] = useState<ClassificationTask | null>(null);
  const [options, setOptions] = useState<ClassificationOptions | null>(null);
  const [form, setForm] = useState<Form>(emptyForm);
  const [extra, setExtra] = useState<Record<Category, ComboOption[]>>({
    Specialty: [], Department: [], DocumentType: [], AcademicYear: [], Session: [],
  });
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string>("");
  const [error, setError] = useState<string>("");
  const [tally, setTally] = useState({ answered: 0, skipped: 0, notEducational: 0, bonus: 0 });
  const [quota, setQuota] = useState<QuotaStatus | null>(null);
  const [more, setMore] = useState(false);
  const checked = useRef<Record<string, string>>({}); // "category|text" → text already checked for look-alikes

  const current: TaskItem | null = useMemo(
    () => task?.items.find((i) => i.status === "Assigned") ?? null,
    [task],
  );

  const loadTask = useCallback(async () => {
    setPhase("loading");
    setError("");
    const [optionsRes, taskRes] = await Promise.all([
      call<ClassificationOptions>("options"),
      call<ClassificationTask | null>("tasks/next", "POST"),
    ]);
    if (!optionsRes.ok || !optionsRes.data) {
      setError(optionsRes.message || "Impossible de charger les listes.");
      setPhase("error");
      return;
    }
    setOptions(optionsRes.data);
    if (!taskRes.ok) {
      setError(taskRes.message || "Impossible de charger les documents.");
      setPhase("error");
      return;
    }
    setTask(taskRes.data);
    setPhase(taskRes.data && taskRes.data.items.some((i) => i.status === "Assigned") ? "working" : "empty");
    const me = await call<{ quota: QuotaStatus }>("me");
    if (me.data) setQuota(me.data.quota);
  }, []);

  useEffect(() => { void loadTask(); }, [loadTask]);

  // Fresh form (with clearly-labelled suggestions) whenever the document changes.
  useEffect(() => {
    if (!current || !options) return;
    const dept = defaults.departmentId && options.departments.some((d) => d.id === defaults.departmentId) ? defaults.departmentId : null;
    const spec = defaults.specialtyId && options.specialties.some((s) => s.id === defaults.specialtyId) ? defaults.specialtyId : null;
    const type = guessType(current.fileName, current.title);
    setForm({
      departmentId: dept, specialtyId: spec, typeId: type,
      yearId: null, sessionId: null, typeSuggested: !!type, placeSuggested: !!(dept || spec),
    });
    setError("");
  }, [current?.assignmentId, options]); // eslint-disable-line react-hooks/exhaustive-deps

  // ---- option lists (approved + my pending + ones discovered while proposing) ----
  const pendingOf = useCallback((category: Category): ComboOption[] =>
    (options?.myPendingProposals ?? [])
      .filter((p) => p.category === category)
      .map((p) => ({ id: p.id, name: p.name, pending: true })), [options]);

  const merge = (...lists: ComboOption[][]): ComboOption[] => {
    const seen = new Set<string>();
    return lists.flat().filter((o) => (seen.has(o.id) ? false : (seen.add(o.id), true)));
  };

  const departmentOptions = useMemo(
    () => merge((options?.departments ?? []).map((d) => ({ id: d.id, name: d.name })), pendingOf("Department"), extra.Department),
    [options, extra, pendingOf],
  );
  const specialtyOptions = useMemo(() => {
    const real = (options?.specialties ?? [])
      .filter((s) => !form.departmentId || s.parentId === form.departmentId || departmentOptions.find((d) => d.id === form.departmentId)?.pending)
      .map((s) => ({ id: s.id, name: s.name }));
    return merge(real, pendingOf("Specialty"), extra.Specialty);
  }, [options, extra, form.departmentId, departmentOptions, pendingOf]);
  const typeOptions = useMemo(
    () => merge((options?.documentTypes ?? []).map((t) => ({ id: t.value, name: documentTypeLabel(t.value) })), pendingOf("DocumentType"), extra.DocumentType),
    [options, extra, pendingOf],
  );
  const yearOptions = useMemo(
    () => merge((options?.academicYears ?? []).map((y) => ({ id: y.id, name: y.name })), pendingOf("AcademicYear"), extra.AcademicYear),
    [options, extra, pendingOf],
  );
  const sessionOptions = useMemo(
    () => merge((options?.sessions ?? []).map((s) => ({ id: s.id, name: s.name })), pendingOf("Session"), extra.Session),
    [options, extra, pendingOf],
  );

  // ---- "Ajouter…" ----
  const propose = (category: Category, parentId: string | null) => async (text: string): Promise<AddOutcome> => {
    const key = `${category}|${text.trim().toLowerCase()}`;
    if (!checked.current[key]) {
      const qs = new URLSearchParams({ category, value: text });
      if (parentId) qs.set("parentId", parentId);
      const sim = await call<{ name: string; id: string | null; enumValue: string | null; pending: boolean }[]>(`proposals/similar?${qs.toString()}`);
      const near = (sim.data ?? []).filter((s) => (s.id ?? s.enumValue) && s.name.trim().toLowerCase() !== text.trim().toLowerCase());
      checked.current[key] = text;
      if (near.length > 0) {
        const suggestions = near.map((s) => ({ id: (s.id ?? s.enumValue)!, name: s.name, pending: s.pending }));
        setExtra((e) => ({ ...e, [category]: merge(e[category], suggestions) }));
        return { ok: false, error: "Des valeurs proches existent déjà. Choisissez-en une, ou cliquez à nouveau sur « Proposer » si la vôtre est différente.", similar: suggestions };
      }
    }

    const res = await call<{
      outcome: "Created" | "ExistingProposal" | "ExistingValue";
      proposal: { id: string; value: string } | null;
      existing: { name: string; id: string | null; enumValue: string | null } | null;
    }>("proposals", "POST", { category, value: text, parentId, documentId: current?.documentId });

    if (!res.ok || !res.data) {
      return { ok: false, error: res.errors[0]?.replace(/^value:\s*/, "") || res.message || "Proposition impossible." };
    }
    if (res.data.outcome === "ExistingValue" && res.data.existing) {
      const id = res.data.existing.id ?? res.data.existing.enumValue!;
      const option = { id, name: res.data.existing.name };
      setExtra((e) => ({ ...e, [category]: merge(e[category], [option]) }));
      return { ok: true, option, note: `« ${option.name} » existe déjà : sélectionné.` };
    }
    const option = { id: res.data.proposal!.id, name: res.data.proposal!.value, pending: true };
    setExtra((e) => ({ ...e, [category]: merge(e[category], [option]) }));
    return { ok: true, option, note: "Proposition envoyée : elle sera relue par un administrateur." };
  };

  // ---- actions ----
  const isPending = (list: ComboOption[], id: string | null) => !!id && !!list.find((o) => o.id === id)?.pending;

  function buildVote() {
    const body: Record<string, unknown> = { decision: "Classify" };
    if (form.departmentId) body[isPending(departmentOptions, form.departmentId) ? "departmentProposalId" : "departmentId"] = form.departmentId;
    if (form.specialtyId) body[isPending(specialtyOptions, form.specialtyId) ? "specialtyProposalId" : "specialtyId"] = form.specialtyId;
    if (form.typeId) body[isPending(typeOptions, form.typeId) ? "documentTypeProposalId" : "documentType"] = form.typeId;
    if (form.yearId) body[isPending(yearOptions, form.yearId) ? "academicYearProposalId" : "academicYearId"] = form.yearId;
    if (form.sessionId) body[isPending(sessionOptions, form.sessionId) ? "sessionProposalId" : "sessionId"] = form.sessionId;
    return body;
  }

  const hasAnyField = !!(form.departmentId || form.specialtyId || form.typeId || form.yearId || form.sessionId);

  function markResolved(assignmentId: string, status: TaskItem["status"]) {
    setTask((t) => t && {
      ...t,
      resolved: t.resolved + 1,
      items: t.items.map((i) => (i.assignmentId === assignmentId ? { ...i, status } : i)),
    });
  }

  async function finishIfDone(remaining: number) {
    if (remaining > 0) return;
    const me = await call<{ quota: QuotaStatus }>("me");
    if (me.data) setQuota(me.data.quota);
    const prompt = await call<ClassifyPrompt>("prompt");
    // "Continue" is only offered when more documents are actually waiting.
    setMore((prompt.data?.availableDocuments ?? 0) > 0);
    setPhase("done");
  }

  async function submit(kind: "classify" | "notEducational" | "skip") {
    if (!current || busy) return;
    setBusy(true);
    setError("");
    const id = current.assignmentId;
    const res = kind === "skip"
      ? await call<{ remaining: number }>(`assignments/${id}/skip`, "POST")
      : await call<VoteResult>(`assignments/${id}/vote`, "POST", kind === "classify" ? buildVote() : { decision: "NotEducational" });
    setBusy(false);

    if (res.status === 409 && kind !== "skip") {
      // Already answered / no longer open: move on rather than trap the user.
      markResolved(id, "Completed");
      setMessage(res.message || "Ce document n’est plus ouvert à la classification.");
      await finishIfDone((task?.items.filter((i) => i.status === "Assigned").length ?? 1) - 1);
      return;
    }
    if (!res.ok || !res.data) {
      setError(res.errors[0] || res.message || "Enregistrement impossible. Vos choix sont conservés : réessayez.");
      return;
    }

    if (kind === "skip") {
      markResolved(id, "Skipped");
      setTally((t) => ({ ...t, skipped: t.skipped + 1 }));
      setMessage("Document passé — aucun souci.");
      await finishIfDone((res.data as { remaining: number }).remaining);
      return;
    }

    const vote = res.data as VoteResult;
    markResolved(id, "Completed");
    setTally((t) => ({
      answered: t.answered + 1,
      skipped: t.skipped,
      notEducational: t.notEducational + (kind === "notEducational" ? 1 : 0),
      bonus: t.bonus + vote.bonusDownloadsGranted,
    }));
    setQuota(vote.quota);
    setMessage(vote.bonusDownloadsGranted > 0
      ? `Merci ! +${vote.bonusDownloadsGranted} téléchargements gagnés.`
      : "Merci ! Votre réponse est enregistrée.");
    await finishIfDone(vote.remaining);
  }

  // ---------------------------------------------------------------- render
  if (phase === "loading") {
    return <Card className="p-8 text-center text-sm text-ink-muted" aria-busy="true">Chargement des documents…</Card>;
  }
  if (phase === "error") {
    return (
      <Card className="p-8 text-center">
        <p role="alert" className="text-sm text-red-700">{error}</p>
        <Button className="mt-4" onClick={() => void loadTask()}>Réessayer</Button>
      </Card>
    );
  }
  if (phase === "empty") {
    return (
      <Card className="p-8 text-center">
        <h2 className="text-lg font-semibold">Rien à classer pour le moment 🎉</h2>
        <p className="mt-2 text-sm text-ink-muted">Tous les documents sont déjà pris en charge. Revenez plus tard — merci pour votre aide !</p>
        <QuotaLine quota={quota} />
        <LinkButton href="/dashboard" className="mt-4">Retour au tableau de bord</LinkButton>
      </Card>
    );
  }
  if (phase === "done") {
    return (
      <Card className="p-8 text-center" aria-live="polite">
        <h2 className="text-xl font-semibold">Merci pour votre contribution ! 🙌</h2>
        <p className="mt-2 text-sm text-ink-muted">
          {tally.answered} réponse{tally.answered > 1 ? "s" : ""} enregistrée{tally.answered > 1 ? "s" : ""}
          {tally.skipped ? ` · ${tally.skipped} document${tally.skipped > 1 ? "s" : ""} passé${tally.skipped > 1 ? "s" : ""}` : ""}
          {tally.bonus ? ` · +${tally.bonus} téléchargements gagnés` : ""}.
        </p>
        <QuotaLine quota={quota} />
        <div className="mt-5 flex flex-wrap justify-center gap-3">
          {more ? <Button onClick={() => { setTally({ answered: 0, skipped: 0, notEducational: 0, bonus: 0 }); void loadTask(); }}>Classer d’autres documents</Button> : null}
          <LinkButton href="/dashboard" variant={more ? "secondary" : "primary"}>Retour au tableau de bord</LinkButton>
        </div>
      </Card>
    );
  }

  const total = task?.total ?? 0;
  const position = (task?.items.filter((i) => i.status !== "Assigned").length ?? 0) + 1;

  return (
    <div className="space-y-4">
      {reason === "quota" ? (
        <Card className="border-brand/30 bg-brand-soft p-4 text-sm">
          <p className="font-medium">Vous avez utilisé vos téléchargements gratuits de la période.</p>
          <p className="mt-1 text-ink-muted">
            Classer quelques documents vous en fait gagner d’autres{quota?.bonusPerContribution ? ` (+${quota.bonusPerContribution} par document)` : ""}.
            Les contenus Premium restent réservés aux abonnés. <Link href="/pricing" className="link">Voir Premium</Link>
          </p>
        </Card>
      ) : null}

      <div>
        <div className="mb-1 flex items-center justify-between text-sm">
          <span className="font-medium">Document {Math.min(position, total)} sur {total}</span>
          <QuotaLine quota={quota} inline />
        </div>
        <progress className="h-2 w-full" max={total} value={total - (task?.items.filter((i) => i.status === "Assigned").length ?? 0)} aria-label="Progression" />
      </div>

      <p role="status" aria-live="polite" className="min-h-[1.25rem] text-sm text-brand-strong">{message}</p>

      {current ? (
        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)]">
          <Card className="overflow-hidden">
            {current.hasPreview ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={`/bff/classification/assignments/${current.assignmentId}/preview`}
                alt={`Aperçu de la première page de ${current.fileName}`}
                className="max-h-[32rem] w-full bg-paper-sunken object-contain"
              />
            ) : (
              <div className="flex h-48 items-center justify-center bg-paper-sunken text-sm text-ink-soft">Aperçu indisponible</div>
            )}
            <div className="space-y-1 p-4 text-sm">
              <h2 className="break-words text-base font-semibold">{current.title}</h2>
              <p className="break-all text-ink-muted">{current.fileName}</p>
              <p className="flex flex-wrap gap-2 pt-1">
                <Badge>{formatBytes(current.fileSize)}</Badge>
                {current.pageCount ? <Badge>{current.pageCount} page{current.pageCount > 1 ? "s" : ""}</Badge> : null}
                <Badge>{current.mimeType.replace("application/", "").toUpperCase()}</Badge>
              </p>
              {current.description ? <p className="pt-1 text-ink-muted">{current.description}</p> : null}
              {current.source ? <p className="text-xs text-ink-soft">Source : {current.source}</p> : null}
            </div>
          </Card>

          <Card className="p-4">
            <form
              className="space-y-4"
              onSubmit={(e) => { e.preventDefault(); void submit("classify"); }}
              aria-label="Classer ce document"
            >
              <p className="text-sm text-ink-muted">
                Remplissez ce que vous savez — un seul champ suffit. Laissez vide ce dont vous n’êtes pas sûr·e.
              </p>
              <Combobox
                label="Département" options={departmentOptions} value={form.departmentId}
                suggestion={form.placeSuggested}
                onChange={(id) => setForm((f) => ({
                  ...f, departmentId: id, placeSuggested: false,
                  specialtyId: id && f.specialtyId && !specialtyOptions.some((s) => s.id === f.specialtyId) ? null : f.specialtyId,
                }))}
                onAdd={propose("Department", null)} addLabel="Ajouter le département"
              />
              <Combobox
                label="Spécialité" options={specialtyOptions} value={form.specialtyId}
                suggestion={form.placeSuggested}
                onChange={(id) => setForm((f) => {
                  const real = options?.specialties.find((s) => s.id === id);
                  return { ...f, specialtyId: id, placeSuggested: false, departmentId: real?.parentId ?? f.departmentId };
                })}
                onAdd={propose("Specialty", form.departmentId && !isPending(departmentOptions, form.departmentId) ? form.departmentId : null)}
                addLabel="Ajouter la spécialité"
              />
              <Combobox
                label="Type de document" options={typeOptions} value={form.typeId}
                suggestion={form.typeSuggested}
                onChange={(id) => setForm((f) => ({ ...f, typeId: id, typeSuggested: false }))}
                onAdd={propose("DocumentType", null)} addLabel="Ajouter le type"
              />
              <div className="grid gap-4 sm:grid-cols-2">
                <Combobox
                  label="Année universitaire" options={yearOptions} value={form.yearId}
                  onChange={(id) => setForm((f) => ({ ...f, yearId: id }))}
                  onAdd={propose("AcademicYear", null)} addLabel="Ajouter l’année" placeholder="ex. 2024-2025"
                />
                <Combobox
                  label="Type d’examen / session" options={sessionOptions} value={form.sessionId}
                  onChange={(id) => setForm((f) => ({ ...f, sessionId: id }))}
                  onAdd={propose("Session", null)} addLabel="Ajouter la session"
                />
              </div>

              {error ? <p role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-800">{error}</p> : null}

              <div className="flex flex-col gap-2 pt-1 sm:flex-row sm:flex-wrap">
                <Button type="submit" disabled={busy || !hasAnyField} className="sm:flex-1">
                  {busy ? "Enregistrement…" : "Valider ma classification"}
                </Button>
                <Button type="button" variant="secondary" disabled={busy} onClick={() => void submit("notEducational")}>
                  Pas un support pédagogique
                </Button>
                <Button type="button" variant="ghost" disabled={busy} onClick={() => void submit("skip")}>
                  Je ne sais pas — passer
                </Button>
              </div>
              <p className="text-xs text-ink-soft">
                Passer un document n’a aucune conséquence : vous ne perdez rien. Chaque réponse validée vous rapproche de plus de téléchargements.
              </p>
            </form>
          </Card>
        </div>
      ) : null}
    </div>
  );
}

function QuotaLine({ quota, inline }: { quota: QuotaStatus | null; inline?: boolean }) {
  if (!quota || !quota.enabled || quota.exempt) return null;
  const text = `${quota.remaining} téléchargement${quota.remaining > 1 ? "s" : ""} restant${quota.remaining > 1 ? "s" : ""}`;
  return inline
    ? <span className="text-ink-muted">{text}</span>
    : <p className="mt-3 text-sm text-ink-muted">{text}{quota.resetsAt ? ` · renouvelé le ${new Date(quota.resetsAt).toLocaleDateString("fr-FR")}` : ""}</p>;
}
