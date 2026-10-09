"use client";

import { useEffect, useId, useMemo, useRef, useState } from "react";
import { Button } from "@/components/ui";

export interface ComboOption {
  id: string;
  name: string;
  pending?: boolean;
}

export type AddOutcome =
  | { ok: true; option: ComboOption; note?: string; similar?: ComboOption[] }
  | { ok: false; error: string; similar?: ComboOption[] };

const fold = (s: string) => s.normalize("NFD").replace(/\p{M}/gu, "").toLowerCase();

/**
 * Searchable single-select with an inline "Ajouter…" path (no modal, no page change), so proposing a missing
 * value never loses the rest of the form. WAI-ARIA combobox pattern: arrow keys, Enter, Escape.
 */
export function Combobox({
  label, hint, options, value, onChange, onAdd, placeholder = "Rechercher…", disabled, addLabel = "Ajouter", suggestion,
}: {
  label: string;
  hint?: string;
  options: ComboOption[];
  value: string | null;
  onChange: (id: string | null) => void;
  onAdd?: (text: string) => Promise<AddOutcome>;
  placeholder?: string;
  disabled?: boolean;
  addLabel?: string;
  suggestion?: boolean;
}) {
  const uid = useId();
  const root = useRef<HTMLDivElement>(null);
  const selected = options.find((o) => o.id === value) ?? null;

  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const [adding, setAdding] = useState(false);
  const [addText, setAddText] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [similar, setSimilar] = useState<ComboOption[]>([]);
  const [note, setNote] = useState<string | null>(null);

  const matches = useMemo(() => {
    const q = fold(query.trim());
    const list = q ? options.filter((o) => fold(o.name).includes(q)) : options;
    return list.slice(0, 60);
  }, [options, query]);

  const canAdd = !!onAdd && query.trim().length >= 2 && !options.some((o) => fold(o.name) === fold(query.trim()));
  const rows = canAdd ? matches.length + 1 : matches.length;

  useEffect(() => setActive(0), [query, open]);

  const close = () => { setOpen(false); setQuery(""); };

  function pick(id: string | null) {
    onChange(id);
    close();
    setNote(null);
  }

  function startAdd() {
    setAdding(true);
    setAddText(query.trim());
    setError(null);
    setSimilar([]);
    setOpen(false);
  }

  async function submitAdd() {
    if (!onAdd) return;
    setBusy(true);
    setError(null);
    const outcome = await onAdd(addText);
    setBusy(false);
    if (outcome.ok) {
      onChange(outcome.option.id);
      setNote(outcome.note ?? null);
      setAdding(false);
      setSimilar([]);
      setQuery("");
    } else {
      setError(outcome.error);
      setSimilar(outcome.similar ?? []);
    }
  }

  function onKeyDown(e: React.KeyboardEvent) {
    if (e.key === "ArrowDown") { e.preventDefault(); setOpen(true); setActive((a) => Math.min(a + 1, rows - 1)); }
    else if (e.key === "ArrowUp") { e.preventDefault(); setActive((a) => Math.max(a - 1, 0)); }
    else if (e.key === "Escape") { close(); }
    else if (e.key === "Enter" && open) {
      e.preventDefault();
      if (active < matches.length) pick(matches[active].id);
      else if (canAdd) startAdd();
    }
  }

  const listId = `${uid}-list`;
  return (
    <div
      ref={root}
      className="text-sm"
      onBlur={(e) => { if (!root.current?.contains(e.relatedTarget as Node | null)) close(); }}
    >
      <label htmlFor={`${uid}-input`} className="mb-1 flex items-center gap-2 font-medium text-ink-muted">
        {label}
        {suggestion && selected ? <span className="rounded bg-paper-sunken px-1.5 text-xs font-normal text-ink-soft">suggestion</span> : null}
      </label>

      <div className="relative">
        <input
          id={`${uid}-input`}
          role="combobox"
          aria-expanded={open}
          aria-controls={listId}
          aria-autocomplete="list"
          aria-activedescendant={open ? `${uid}-opt-${active}` : undefined}
          disabled={disabled}
          autoComplete="off"
          value={open ? query : selected?.name ?? ""}
          placeholder={selected ? selected.name : placeholder}
          onChange={(e) => { setQuery(e.target.value); setOpen(true); }}
          onFocus={() => setOpen(true)}
          onKeyDown={onKeyDown}
          className="w-full rounded-md border border-line bg-paper-raised px-3 py-2 pr-9 disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-brand"
        />
        {selected && !disabled ? (
          <button type="button" aria-label={`Effacer ${label}`} onClick={() => pick(null)}
            className="absolute inset-y-0 right-2 px-1 text-ink-soft hover:text-ink">×</button>
        ) : null}

        {open ? (
          <ul id={listId} role="listbox" aria-label={label}
            className="absolute z-30 mt-1 max-h-60 w-full overflow-auto rounded-md border border-line bg-paper-raised shadow-card">
            {matches.map((o, i) => (
              <li key={o.id} id={`${uid}-opt-${i}`} role="option" aria-selected={o.id === value}
                onMouseDown={(e) => e.preventDefault()} onClick={() => pick(o.id)}
                className={`cursor-pointer px-3 py-2 ${i === active ? "bg-brand-soft" : ""} ${o.id === value ? "font-medium" : ""}`}>
                {o.name}{o.pending ? <span className="ml-2 text-xs text-ink-soft">(en attente de validation)</span> : null}
              </li>
            ))}
            {matches.length === 0 && !canAdd ? <li className="px-3 py-2 text-ink-soft">Aucun résultat</li> : null}
            {canAdd ? (
              <li id={`${uid}-opt-${matches.length}`} role="option" aria-selected={false}
                onMouseDown={(e) => e.preventDefault()} onClick={startAdd}
                className={`cursor-pointer border-t border-line px-3 py-2 font-medium text-brand-strong ${active === matches.length ? "bg-brand-soft" : ""}`}>
                ＋ {addLabel} « {query.trim()} »…
              </li>
            ) : null}
          </ul>
        ) : null}
      </div>

      {hint && !adding ? <p className="mt-1 text-xs text-ink-soft">{hint}</p> : null}
      {note ? <p role="status" className="mt-1 text-xs text-ink-muted">{note}</p> : null}

      {adding ? (
        <div role="group" aria-label={`${addLabel} — ${label}`} className="mt-2 rounded-md border border-line bg-paper-sunken p-3">
          <label className="block text-xs font-medium text-ink-muted" htmlFor={`${uid}-add`}>Nouvelle valeur proposée</label>
          <input id={`${uid}-add`} value={addText} maxLength={120} autoFocus
            onChange={(e) => setAddText(e.target.value)}
            onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); void submitAdd(); } }}
            className="mt-1 w-full rounded-md border border-line bg-paper-raised px-3 py-2" />
          <p className="mt-1 text-xs text-ink-soft">
            Elle sera relue par un administrateur avant d’apparaître pour tout le monde. Vous pouvez continuer à classer en attendant.
          </p>
          {error ? <p role="alert" className="mt-2 text-xs text-red-700">{error}</p> : null}
          {similar.length > 0 ? (
            <div className="mt-2 text-xs">
              <p className="text-ink-muted">Vouliez-vous dire :</p>
              <div className="mt-1 flex flex-wrap gap-2">
                {similar.map((s) => (
                  <button key={s.id} type="button" onClick={() => { onChange(s.id); setAdding(false); setSimilar([]); }}
                    className="rounded-full border border-line bg-paper-raised px-2.5 py-1 hover:bg-brand-soft">{s.name}</button>
                ))}
              </div>
            </div>
          ) : null}
          <div className="mt-3 flex gap-2">
            <Button type="button" onClick={() => void submitAdd()} disabled={busy || addText.trim().length < 2}>
              {busy ? "Envoi…" : "Proposer"}
            </Button>
            <Button type="button" variant="ghost" onClick={() => { setAdding(false); setError(null); }}>Annuler</Button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
