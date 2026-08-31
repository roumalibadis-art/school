import type { Metadata } from "next";
import Link from "next/link";
import { Card, Container } from "@/components/ui";

export const metadata: Metadata = { title: "Inscription", robots: { index: false } };

export default function RegisterPage() {
  return (
    <Container className="py-16">
      <Card className="mx-auto max-w-md p-8 text-center">
        <h1 className="text-xl font-semibold">Inscription</h1>
        <p className="mt-3 text-sm text-ink-muted">
          La création de compte étudiant sera disponible prochainement.
        </p>
        <Link href="/" className="link mt-6 inline-block text-sm">
          Retour à l&apos;accueil
        </Link>
      </Card>
    </Container>
  );
}
