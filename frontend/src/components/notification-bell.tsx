"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";

interface Notif {
  id: string;
  title: string;
  body: string;
  link: string | null;
  isRead: boolean;
  createdAt: string;
}

export function NotificationBell() {
  const [state, setState] = useState<{ authenticated: boolean; unreadCount: number; items: Notif[] } | null>(null);
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  const load = () =>
    fetch("/api/notif", { cache: "no-store" })
      .then((r) => r.json())
      .then(setState)
      .catch(() => setState({ authenticated: false, unreadCount: 0, items: [] }));

  useEffect(() => {
    void load();
    const timer = setInterval(load, 60_000);
    return () => clearInterval(timer);
  }, []);

  useEffect(() => {
    const onClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("click", onClick);
    return () => document.removeEventListener("click", onClick);
  }, []);

  if (!state?.authenticated) return null;

  const toggle = () => {
    setOpen((v) => !v);
    if (!open && state.unreadCount > 0) {
      fetch("/api/notif", { method: "POST" }).then(() =>
        setState((s) => (s ? { ...s, unreadCount: 0, items: s.items.map((n) => ({ ...n, isRead: true })) } : s)),
      );
    }
  };

  return (
    <div ref={ref} className="relative">
      <button onClick={toggle} aria-label="Notifications" className="relative text-ink-muted hover:text-ink">
        <span aria-hidden>🔔</span>
        {state.unreadCount > 0 ? (
          <span className="absolute -right-1.5 -top-1.5 grid h-4 min-w-4 place-items-center rounded-full bg-brand px-1 text-[10px] font-semibold text-white">
            {state.unreadCount}
          </span>
        ) : null}
      </button>

      {open ? (
        <div className="absolute right-0 mt-2 w-80 rounded-lg border border-line bg-paper-raised p-2 shadow-card">
          {state.items.length === 0 ? (
            <p className="p-3 text-sm text-ink-soft">Aucune notification.</p>
          ) : (
            <ul className="max-h-96 divide-y divide-line overflow-auto text-sm">
              {state.items.map((n) => (
                <li key={n.id} className="p-2">
                  <Wrap link={n.link}>
                    <p className="font-medium">{n.title}</p>
                    <p className="text-xs text-ink-muted">{n.body}</p>
                  </Wrap>
                </li>
              ))}
            </ul>
          )}
        </div>
      ) : null}
    </div>
  );
}

function Wrap({ link, children }: { link: string | null; children: React.ReactNode }) {
  return link ? (
    <Link href={link} className="block no-underline hover:text-brand-strong">{children}</Link>
  ) : (
    <div>{children}</div>
  );
}
