import type { Metadata } from "next";
import { Container } from "@/components/ui";

export const metadata: Metadata = {
  title: "Conditions d'utilisation",
  alternates: { canonical: "/terms" },
};

export default function TermsPage() {
  return (
    <Container className="py-12">
      <div className="mx-auto max-w-prose">
        <h1 className="text-2xl font-semibold">Conditions d&apos;utilisation</h1>
        <p className="mt-4 text-sm text-ink-muted">
          En utilisant USTHB Study, vous acceptez d&apos;utiliser les ressources à des fins
          strictement pédagogiques. Les documents restent la propriété de leurs auteurs. Toute
          demande de retrait peut être adressée via la page contact.
        </p>
        <p className="mt-4 text-sm text-ink-soft">Version préliminaire — le texte complet sera publié ultérieurement.</p>
      </div>
    </Container>
  );
}
