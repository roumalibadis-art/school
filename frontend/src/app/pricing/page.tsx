import type { Metadata } from "next";
import { Card, Container, LinkButton } from "@/components/ui";

export const metadata: Metadata = {
  title: "Premium",
  description: "Passez Premium pour accéder à l'ensemble des ressources d'USTHB Study.",
  alternates: { canonical: "/pricing" },
};

export default function PricingPage() {
  return (
    <Container className="py-12">
      <div className="mx-auto max-w-2xl text-center">
        <h1 className="text-3xl font-semibold">Premium</h1>
        <p className="mt-3 text-ink-muted">
          La plupart des documents sont gratuits. L&apos;abonnement Premium débloque les ressources
          marquées Premium et soutient la plateforme.
        </p>
      </div>

      <div className="mx-auto mt-10 grid max-w-3xl gap-4 sm:grid-cols-2">
        <Card className="p-6">
          <h2 className="text-lg font-semibold">Gratuit</h2>
          <p className="mt-1 text-sm text-ink-muted">Pour commencer.</p>
          <ul className="mt-4 space-y-1 text-sm text-ink-muted">
            <li>Accès aux documents gratuits</li>
            <li>Recherche et navigation complètes</li>
            <li>Aperçu de chaque document</li>
          </ul>
          <LinkButton href="/register" variant="secondary" className="mt-6 w-full">
            Créer un compte
          </LinkButton>
        </Card>

        <Card className="border-brand/30 p-6">
          <h2 className="text-lg font-semibold">Premium</h2>
          <p className="mt-1 text-sm text-ink-muted">Les tarifs sont définis par l&apos;équipe.</p>
          <ul className="mt-4 space-y-1 text-sm text-ink-muted">
            <li>Tout le contenu gratuit</li>
            <li>Accès aux documents Premium</li>
            <li>Téléchargement des corrigés Premium</li>
          </ul>
          <LinkButton href="/login" variant="primary" className="mt-6 w-full">
            Voir les offres
          </LinkButton>
        </Card>
      </div>

      <p className="mx-auto mt-8 max-w-prose text-center text-xs text-ink-soft">
        Les offres et prix exacts sont gérés depuis le tableau de bord d&apos;administration et
        affichés au moment de l&apos;abonnement.
      </p>
    </Container>
  );
}
