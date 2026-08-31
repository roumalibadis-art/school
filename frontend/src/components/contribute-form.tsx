"use client";

import { useActionState, useEffect, useState } from "react";
import { Button } from "@/components/ui";
import { documentTypeLabels } from "@/lib/format";
import { submitContributionAction } from "@/lib/contribution-actions";
import type { DocumentType } from "@/lib/types";

interface Module {
  id: string;
  name: string;
}

export function ContributeForm() {
  const [state, formAction, pending] = useActionState(submitContributionAction, {});
  const [search, setSearch] = useState("");
  const [modules, setModules] = useState<Module[]>([]);
  const [moduleId, setModuleId] = useState("");
  const [moduleName, setModuleName] = useState("");

  useEffect(() => {
    if (search.trim().length < 2) {
      setModules([]);
      return;
    }
    const timer = setTimeout(() => {
      fetch(`/api/modules?search=${encodeURIComponent(search)}&pageSize=10`, { cache: "no-store" })
        .then((r) => r.json())
        .then((b: { data: Module[] }) => setModules(b.data ?? []))
        .catch(() => setModules([]));
    }, 250);
    return () => clearTimeout(timer);
  }, [search]);

  return (
    <form action={formAction} className="space-y-4">
      <input type="hidden" name="moduleId" value={moduleId} />

      <label className="block text-sm">
        <span className="mb-1 block font-medium text-ink-muted">Titre</span>
        <input name="title" required maxLength={300} className="w-full rounded-md border border-line bg-paper-raised px-3 py-2" />
      </label>

      <label className="block text-sm">
        <span className="mb-1 block font-medium text-ink-muted">Type</span>
        <select name="type" required className="w-full rounded-md border border-line bg-paper-raised px-3 py-2">
          {(Object.keys(documentTypeLabels) as DocumentType[]).map((t) => (
            <option key={t} value={t}>{documentTypeLabels[t]}</option>
          ))}
        </select>
      </label>

      <div className="text-sm">
        <span className="mb-1 block font-medium text-ink-muted">Module</span>
        {moduleId ? (
          <p className="flex items-center gap-2">
            <span className="rounded-md bg-brand-soft px-2 py-1 text-brand-strong">{moduleName}</span>
            <button type="button" onClick={() => { setModuleId(""); setModuleName(""); }} className="text-xs text-ink-soft hover:text-ink">
              changer
            </button>
          </p>
        ) : (
          <>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Rechercher un module…"
              className="w-full rounded-md border border-line bg-paper-raised px-3 py-2"
            />
            {modules.length > 0 ? (
              <ul className="mt-1 max-h-40 overflow-auto rounded-md border border-line bg-paper-raised text-sm">
                {modules.map((m) => (
                  <li key={m.id}>
                    <button
                      type="button"
                      onClick={() => { setModuleId(m.id); setModuleName(m.name); setSearch(""); }}
                      className="block w-full px-3 py-1.5 text-left hover:bg-paper-sunken"
                    >
                      {m.name}
                    </button>
                  </li>
                ))}
              </ul>
            ) : null}
          </>
        )}
      </div>

      <label className="block text-sm">
        <span className="mb-1 block font-medium text-ink-muted">Description (optionnelle)</span>
        <textarea name="description" rows={3} maxLength={2000} className="w-full rounded-md border border-line bg-paper-raised px-3 py-2" />
      </label>

      <label className="block text-sm">
        <span className="mb-1 block font-medium text-ink-muted">Fichier (PDF ou image)</span>
        <input name="file" type="file" accept=".pdf,.png,.jpg,.jpeg" required className="w-full text-sm" />
      </label>

      {state.error ? <p className="text-sm text-red-700">{state.error}</p> : null}
      <Button type="submit" disabled={pending || !moduleId} className="w-full">
        {pending ? "Envoi…" : "Soumettre pour modération"}
      </Button>
    </form>
  );
}
