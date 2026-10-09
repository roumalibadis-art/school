import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { RegisterForm } from "@/components/auth-form";
import { GoogleButton } from "@/components/google-button";
import { Card, Container } from "@/components/ui";
import { registerAction } from "@/lib/auth-actions";
import { googleEnabled } from "@/lib/providers";
import { getCurrentUser } from "@/lib/session";

export const metadata: Metadata = { title: "Inscription", robots: { index: false } };

export default async function RegisterPage() {
  if (await getCurrentUser()) redirect("/dashboard");
  const google = await googleEnabled();

  return (
    <Container className="py-16">
      <Card className="mx-auto max-w-md p-8">
        <h1 className="mb-1 text-center text-xl font-semibold">Créer un compte</h1>
        <p className="mb-6 text-center text-sm text-ink-muted">
          Vous choisirez votre spécialité et votre niveau juste après.
        </p>
        {google ? (
          <>
            <GoogleButton returnUrl="/profile?welcome=1" label="S’inscrire avec Google" />
            <p className="my-4 text-center text-xs uppercase tracking-wide text-ink-soft">ou avec votre email</p>
          </>
        ) : null}
        <RegisterForm action={registerAction} />
      </Card>
    </Container>
  );
}
