/** Plain link: the whole OAuth round-trip is server-side (state + PKCE cookie set by the API). */
export function GoogleButton({ returnUrl, label = "Continuer avec Google" }: { returnUrl?: string; label?: string }) {
  const href = `/api/auth/google/start${returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : ""}`;
  return (
    // A real navigation (not <Link>): the API answers with a redirect to accounts.google.com.
    // eslint-disable-next-line @next/next/no-html-link-for-pages
    <a
      href={href}
      className="flex w-full items-center justify-center gap-3 rounded-md border border-line bg-paper-raised px-4 py-2 text-sm font-medium no-underline hover:bg-paper-sunken focus-visible:ring-2 focus-visible:ring-brand"
    >
      <svg aria-hidden width="18" height="18" viewBox="0 0 48 48">
        <path fill="#EA4335" d="M24 9.5c3.5 0 6.6 1.2 9.1 3.6l6.8-6.8C35.8 2.4 30.3 0 24 0 14.6 0 6.5 5.4 2.6 13.2l7.9 6.1C12.4 13.6 17.7 9.5 24 9.5z" />
        <path fill="#4285F4" d="M46.5 24.5c0-1.6-.1-3.1-.4-4.5H24v9h12.7c-.6 3-2.3 5.5-4.8 7.2l7.5 5.8c4.4-4.1 7.1-10.1 7.1-17.5z" />
        <path fill="#FBBC05" d="M10.5 28.7a14.5 14.5 0 0 1 0-9.4l-7.9-6.1a24 24 0 0 0 0 21.6l7.9-6.1z" />
        <path fill="#34A853" d="M24 48c6.5 0 11.9-2.1 15.9-5.8l-7.5-5.8c-2.1 1.4-4.9 2.3-8.4 2.3-6.3 0-11.6-4.1-13.5-9.8l-7.9 6.1C6.5 42.6 14.6 48 24 48z" />
      </svg>
      {label}
    </a>
  );
}

export const googleErrors: Record<string, string> = {
  google_state: "La connexion Google a expiré ou a été interrompue. Réessayez.",
  google_cancelled: "Connexion Google annulée.",
  google_failed: "Impossible de terminer la connexion avec Google. Réessayez.",
  google_unverified: "Votre adresse Google n’est pas vérifiée : connexion refusée.",
  google_link_required:
    "Un compte existe déjà avec cette adresse. Connectez-vous avec votre mot de passe, puis liez Google depuis votre profil.",
  account_disabled: "Ce compte est suspendu.",
};

export const googleLinkMessages: Record<string, { ok: boolean; text: string }> = {
  linked: { ok: true, text: "Compte Google lié. Vous pouvez désormais vous connecter avec Google." },
  already_linked: { ok: true, text: "Ce compte Google est déjà lié à votre compte." },
  in_use: { ok: false, text: "Ce compte Google est déjà utilisé par un autre compte." },
  unverified: { ok: false, text: "L’adresse de ce compte Google n’est pas vérifiée." },
  cancelled: { ok: false, text: "Liaison annulée." },
  failed: { ok: false, text: "Impossible de lier le compte Google. Réessayez." },
  link_expired: { ok: false, text: "Le lien a expiré. Recommencez depuis votre profil." },
};
