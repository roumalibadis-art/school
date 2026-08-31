import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { LoginForm } from "@/components/auth-form";
import { Card, Container } from "@/components/ui";
import { loginAction } from "@/lib/auth-actions";
import { getCurrentUser } from "@/lib/session";

export const metadata: Metadata = { title: "Connexion", robots: { index: false } };

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ next?: string }> }) {
  if (await getCurrentUser()) redirect("/dashboard");
  const { next } = await searchParams;

  return (
    <Container className="py-16">
      <Card className="mx-auto max-w-md p-8">
        <h1 className="mb-6 text-center text-xl font-semibold">Connexion</h1>
        <LoginForm action={loginAction} next={next} />
      </Card>
    </Container>
  );
}
