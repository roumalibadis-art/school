"use client";

import Link from "next/link";
import { useActionState } from "react";
import { Button } from "@/components/ui";

type Action = (prev: { error?: string }, formData: FormData) => Promise<{ error?: string }>;

function Field({
  label, name, type = "text", required = true, autoComplete,
}: {
  label: string; name: string; type?: string; required?: boolean; autoComplete?: string;
}) {
  return (
    <label className="block text-sm">
      <span className="mb-1 block font-medium text-ink-muted">{label}</span>
      <input
        name={name}
        type={type}
        required={required}
        autoComplete={autoComplete}
        className="w-full rounded-md border border-line bg-paper-raised px-3 py-2 focus-visible:ring-2 focus-visible:ring-brand"
      />
    </label>
  );
}

export function LoginForm({ action, next }: { action: Action; next?: string }) {
  const [state, formAction, pending] = useActionState(action, {});
  return (
    <form action={formAction} className="space-y-4">
      {next ? <input type="hidden" name="next" value={next} /> : null}
      <Field label="Email" name="email" type="email" autoComplete="email" />
      <Field label="Mot de passe" name="password" type="password" autoComplete="current-password" />
      {state.error ? <p className="text-sm text-red-700">{state.error}</p> : null}
      <Button type="submit" disabled={pending} className="w-full">
        {pending ? "Connexion…" : "Se connecter"}
      </Button>
      <p className="text-center text-sm text-ink-muted">
        Pas de compte ? <Link href="/register" className="link">Créer un compte</Link>
      </p>
    </form>
  );
}

export function RegisterForm({ action }: { action: Action }) {
  const [state, formAction, pending] = useActionState(action, {});
  return (
    <form action={formAction} className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Prénom" name="firstName" autoComplete="given-name" />
        <Field label="Nom" name="lastName" autoComplete="family-name" />
      </div>
      <Field label="Email" name="email" type="email" autoComplete="email" />
      <Field label="Mot de passe" name="password" type="password" autoComplete="new-password" />
      <Field label="Confirmer le mot de passe" name="confirmPassword" type="password" autoComplete="new-password" />
      {state.error ? <p className="text-sm text-red-700">{state.error}</p> : null}
      <Button type="submit" disabled={pending} className="w-full">
        {pending ? "Création…" : "Créer mon compte"}
      </Button>
      <p className="text-center text-sm text-ink-muted">
        Déjà inscrit ? <Link href="/login" className="link">Se connecter</Link>
      </p>
    </form>
  );
}
