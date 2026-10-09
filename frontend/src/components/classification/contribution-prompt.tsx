"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui";
import type { ClassifyPrompt } from "@/lib/types";

// Never interrupt these: sign-in / onboarding (profile), payment, uploading a document, the classification page
// itself, the admin console, legal pages.
const QUIET = [
  "/login", "/register", "/profile", "/subscribe", "/contribute", "/classify", "/admin", "/auth", "/privacy", "/terms",
];
const RECHECK_AFTER_DOWNLOAD_MS = 2500;

async function getPrompt(): Promise<ClassifyPrompt | null> {
  try {
    const res = await fetch("/bff/classification/prompt", { cache: "no-store" });
    if (!res.ok) return null;
    return ((await res.json()) as { data: ClassifyPrompt }).data;
  } catch {
    return null;
  }
}

/**
 * Invites signed-in students to classify a few documents when the admin-configured triggers fire (login and/or
 * every N downloads). The server decides *whether* to prompt and consumes the trigger on answer, so this stays
 * a thin, polite layer: one dialog at most, easy to dismiss, never over critical pages.
 */
export function ContributionPrompt() {
  const pathname = usePathname();
  const router = useRouter();
  const [prompt, setPrompt] = useState<ClassifyPrompt | null>(null);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const dialog = useRef<HTMLDivElement>(null);
  const quiet = QUIET.some((p) => pathname === p || pathname.startsWith(`${p}/`));

  const check = useCallback(async () => {
    if (quiet) return;
    const session = await fetch("/api/session", { cache: "no-store" }).then((r) => r.json()).catch(() => null);
    if (!session?.authenticated) return;
    const p = await getPrompt();
    if (!p) return;
    setPrompt(p);
    setOpen(p.shouldPrompt);
  }, [quiet]);

  useEffect(() => { void check(); }, [check, pathname]);

  // A download does not navigate away, so look again shortly after one starts.
  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | undefined;
    const onClick = (e: MouseEvent) => {
      const a = (e.target as HTMLElement | null)?.closest?.("a[href^='/dl/']");
      if (a) {
        clearTimeout(timer);
        timer = setTimeout(() => void check(), RECHECK_AFTER_DOWNLOAD_MS);
      }
    };
    document.addEventListener("click", onClick);
    return () => { document.removeEventListener("click", onClick); clearTimeout(timer); };
  }, [check]);

  useEffect(() => {
    if (!open) return;
    dialog.current?.querySelector<HTMLElement>("button, a")?.focus();
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") void answer("later"); };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  async function answer(action: "start" | "later") {
    if (busy) return;
    setBusy(true);
    await fetch("/bff/classification/prompt/ack", {
      method: "POST",
      headers: { "Content-Type": "application/json", "x-requested-with": "usthb" },
      body: JSON.stringify({ action }),
    }).catch(() => undefined);
    setBusy(false);
    setOpen(false);
    if (action === "start") router.push("/classify");
    else setPrompt((p) => (p ? { ...p, shouldPrompt: false } : p));
  }

  if (quiet || !prompt) return null;

  if (open && prompt.shouldPrompt) {
    const why = prompt.reason === "downloads"
      ? "Vous avez téléchargé plusieurs documents."
      : "Content de vous revoir !";
    return (
      <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/30 p-4 sm:items-center" role="presentation">
        <div ref={dialog} role="dialog" aria-modal="true" aria-labelledby="contrib-title"
          className="w-full max-w-md rounded-lg border border-line bg-paper-raised p-6 shadow-card">
          <h2 id="contrib-title" className="text-lg font-semibold">Un petit coup de main ? 🙌</h2>
          <p className="mt-2 text-sm text-ink-muted">
            {why} Aidez-nous à classer {Math.min(prompt.documentsPerTask, Math.max(prompt.availableDocuments, 1))} document
            {prompt.availableDocuments > 1 ? "s" : ""} (spécialité, type, année…). Cela prend une minute
            {prompt.quota.enabled && !prompt.quota.exempt
              ? ` et vous gagnez ${prompt.quota.bonusPerContribution} téléchargement${prompt.quota.bonusPerContribution > 1 ? "s" : ""} par document`
              : ""}.
          </p>
          <div className="mt-5 flex flex-col gap-2 sm:flex-row">
            <Button onClick={() => void answer("start")} disabled={busy} className="sm:flex-1">Je classe maintenant</Button>
            <Button variant="ghost" onClick={() => void answer("later")} disabled={busy}>Plus tard</Button>
          </div>
        </div>
      </div>
    );
  }

  if (prompt.hasOpenTask && prompt.openTaskRemaining > 0) {
    return (
      <div className="fixed bottom-4 right-4 z-40 max-w-xs rounded-lg border border-line bg-paper-raised p-3 text-sm shadow-card">
        <p>
          Il vous reste <strong>{prompt.openTaskRemaining}</strong> document{prompt.openTaskRemaining > 1 ? "s" : ""} à classer.{" "}
          <Link href="/classify" className="link">Continuer</Link>
        </p>
      </div>
    );
  }

  return null;
}
