import type { Metadata } from "next";
import { Container } from "@/components/ui";

export const metadata: Metadata = {
  title: "Confidentialité",
  alternates: { canonical: "/privacy" },
};

export default function PrivacyPage() {
  return (
    <Container className="py-12">
      <div className="mx-auto max-w-prose">
        <h1 className="text-2xl font-semibold">Confidentialité</h1>
        <p className="mt-4 text-sm text-ink-muted">
          USTHB Study collecte le minimum de données nécessaires au fonctionnement du service
          (compte, profil académique, historique de consultation). Aucune donnée personnelle
          n&apos;est revendue.
        </p>
        <p className="mt-4 text-sm text-ink-soft">Version préliminaire.</p>
      </div>
    </Container>
  );
}
