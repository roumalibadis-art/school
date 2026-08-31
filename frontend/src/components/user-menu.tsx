"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { logoutAction } from "@/lib/auth-actions";

type Session =
  | { authenticated: false }
  | { authenticated: true; firstName: string; hasProfile: boolean };

export function UserMenu() {
  const [session, setSession] = useState<Session | null>(null);

  useEffect(() => {
    fetch("/api/session", { cache: "no-store" })
      .then((r) => r.json())
      .then(setSession)
      .catch(() => setSession({ authenticated: false }));
  }, []);

  if (!session) return <span className="w-16" aria-hidden />;

  if (!session.authenticated) {
    return (
      <Link
        href="/login"
        className="rounded-md border border-line px-3 py-1.5 no-underline hover:bg-paper-sunken"
      >
        Connexion
      </Link>
    );
  }

  return (
    <div className="flex items-center gap-3">
      <Link href="/dashboard" className="text-ink-muted no-underline hover:text-ink">
        {session.firstName}
      </Link>
      <form action={logoutAction}>
        <button type="submit" className="text-ink-soft hover:text-ink">
          Déconnexion
        </button>
      </form>
    </div>
  );
}
