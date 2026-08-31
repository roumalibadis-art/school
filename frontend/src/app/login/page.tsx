import type { Metadata } from "next";
import Link from "next/link";
import { Card, Container } from "@/components/ui";

export const metadata: Metadata = { title: "Connexion", robots: { index: false } };

export default function LoginPage() {
  return (
    <Container className="py-16">
      <Card className="mx-auto max-w-md p-8 text-center">
        <h1 className="text-xl font-semibold">Connexion</h1>
        <p className="mt-3 text-sm text-ink-muted">
          L&apos;espace étudiant (inscription, tableau de bord, favoris, téléchargements) arrive
          bientôt. En attendant, vous pouvez parcourir et rechercher librement.
        </p>
        <Link href="/" className="link mt-6 inline-block text-sm">
          Retour à l&apos;accueil
        </Link>
      </Card>
    </Container>
  );
}
