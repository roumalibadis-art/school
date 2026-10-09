import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { LoginForm } from "@/components/auth-form";
import { GoogleButton, googleErrors } from "@/components/google-button";
import { Card, Container } from "@/components/ui";
import { loginAction } from "@/lib/auth-actions";
import { googleEnabled } from "@/lib/providers";
import { getCurrentUser } from "@/lib/session";

export const metadata: Metadata = { title: "Connexion", robots: { index: false } };

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ next?: string; error?: string }> }) {
  if (await getCurrentUser()) redirect("/dashboard");
  const { next, error } = await searchParams;
  const google = await googleEnabled();
  const returnUrl = next && next.startsWith("/") && !next.startsWith("//") ? next : undefined;

  return (
    <Container className="py-16">
      <Card className="mx-auto max-w-md p-8">
        <h1 className="mb-6 text-center text-xl font-semibold">Connexion</h1>
        {error && googleErrors[error] ? (
          <p role="alert" className="mb-4 rounded-md bg-red-50 p-3 text-sm text-red-800">{googleErrors[error]}</p>
        ) : null}
        {google ? (
          <>
            <GoogleButton returnUrl={returnUrl} />
            <p className="my-4 text-center text-xs uppercase tracking-wide text-ink-soft">ou avec votre email</p>
          </>
        ) : null}
        <LoginForm action={loginAction} next={next} />
      </Card>
    </Container>
  );
}
