"use client";

import { useState } from "react";
import { cx } from "@/components/ui";

const reasons = [
  ["WrongModule", "Mauvais module"],
  ["WrongYear", "Mauvaise année"],
  ["Unreadable", "Illisible"],
  ["Duplicate", "Doublon"],
  ["IncorrectInformation", "Information incorrecte"],
  ["Copyright", "Problème de droits d'auteur"],
  ["Other", "Autre"],
] as const;

export function ReportButton({ slug }: { slug: string }) {
  const [open, setOpen] = useState(false);
  const [status, setStatus] = useState<"idle" | "sending" | "done" | "error">("idle");
  const [reason, setReason] = useState<string>("WrongModule");
  const [comment, setComment] = useState("");

  const submit = async () => {
    setStatus("sending");
    const res = await fetch("/api/report", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ slug, reason, comment }),
    });
    setStatus(res.ok ? "done" : "error");
  };

  if (!open) {
    return (
      <button onClick={() => setOpen(true)} className="text-xs text-ink-soft underline hover:text-ink">
        Signaler ce document
      </button>
    );
  }

  return (
    <div className="rounded-md border border-line bg-paper-raised p-3 text-sm">
      {status === "done" ? (
        <p className="text-brand-strong">Merci, votre signalement a été transmis.</p>
      ) : (
        <>
          <select
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            className="w-full rounded-md border border-line bg-paper px-2 py-1.5"
          >
            {reasons.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
          </select>
          <textarea
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            rows={2}
            placeholder="Précisions (optionnel)…"
            className="mt-2 w-full rounded-md border border-line bg-paper px-2 py-1.5"
          />
          {status === "error" ? <p className="mt-1 text-xs text-red-700">Connectez-vous pour signaler.</p> : null}
          <div className="mt-2 flex gap-2">
            <button
              onClick={submit}
              disabled={status === "sending"}
              className={cx("rounded-md bg-brand px-3 py-1.5 text-xs font-medium text-white", status === "sending" && "opacity-50")}
            >
              Envoyer
            </button>
            <button onClick={() => setOpen(false)} className="text-xs text-ink-soft hover:text-ink">Annuler</button>
          </div>
        </>
      )}
    </div>
  );
}
