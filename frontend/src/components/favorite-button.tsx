"use client";

import { useEffect, useState, useTransition } from "react";
import { cx } from "@/components/ui";

export function FavoriteButton({
  kind,
  entityId,
  className,
}: {
  kind: "Module" | "Document";
  entityId: string;
  className?: string;
}) {
  const [state, setState] = useState<"loading" | "hidden" | "on" | "off">("loading");
  const [pending, startTransition] = useTransition();

  useEffect(() => {
    let cancelled = false;
    fetch(`/api/fav?kind=${kind}&entityId=${entityId}`, { cache: "no-store" })
      .then((r) => r.json())
      .then((body: { authenticated: boolean; favorited: boolean }) => {
        if (cancelled) return;
        setState(!body.authenticated ? "hidden" : body.favorited ? "on" : "off");
      })
      .catch(() => !cancelled && setState("hidden"));
    return () => {
      cancelled = true;
    };
  }, [kind, entityId]);

  if (state === "hidden" || state === "loading") return null;

  const active = state === "on";

  const toggle = () =>
    startTransition(async () => {
      const res = await fetch("/api/fav", {
        method: active ? "DELETE" : "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ kind, entityId }),
      });
      if (res.ok) setState(active ? "off" : "on");
    });

  return (
    <button
      type="button"
      onClick={toggle}
      disabled={pending}
      aria-pressed={active}
      className={cx(
        "inline-flex items-center gap-1.5 rounded-md border px-3 py-2 text-sm font-medium transition-colors",
        active
          ? "border-premium/30 bg-premium-soft text-premium"
          : "border-line bg-paper-raised text-ink hover:bg-paper-sunken",
        className,
      )}
    >
      <span aria-hidden>{active ? "★" : "☆"}</span>
      {active ? "Enregistré" : "Enregistrer"}
    </button>
  );
}
