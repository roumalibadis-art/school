import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { RegisterForm } from "@/components/auth-form";
import { Card, Container } from "@/components/ui";
import { registerAction } from "@/lib/auth-actions";
import { getCurrentUser } from "@/lib/session";

export const metadata: Metadata = { title: "Inscription", robots: { index: false } };

export default async function RegisterPage() {
  if (await getCurrentUser()) redirect("/dashboard");

  return (
    <Container className="py-16">
      <Card className="mx-auto max-w-md p-8">
        <h1 className="mb-1 text-center text-xl font-semibold">Créer un compte</h1>
        <p className="mb-6 text-center text-sm text-ink-muted">
          Vous choisirez votre spécialité et votre niveau juste après.
        </p>
        <RegisterForm action={registerAction} />
      </Card>
    </Container>
  );
}
