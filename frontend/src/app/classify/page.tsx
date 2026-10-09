import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { ClassifyWorkspace } from "@/components/classification/classify-workspace";
import { Container } from "@/components/ui";
import { getCurrentUser } from "@/lib/session";

export const metadata: Metadata = { title: "Aider à classer des documents", robots: { index: false } };

export default async function ClassifyPage({ searchParams }: { searchParams: Promise<{ reason?: string }> }) {
  const user = await getCurrentUser();
  if (!user) redirect("/login?next=/classify");
  const { reason } = await searchParams;

  return (
    <Container className="py-8">
      <header className="mb-6 max-w-prose">
        <h1 className="text-2xl font-semibold">Aidez la communauté à classer les documents</h1>
        <p className="mt-2 text-sm text-ink-muted">
          Quelques secondes par document suffisent. Plusieurs étudiants examinent chaque document : c’est l’accord
          entre eux qui valide le classement. Vous gagnez des téléchargements en retour.
        </p>
      </header>
      <ClassifyWorkspace
        defaults={{ departmentId: user.department?.id ?? null, specialtyId: user.specialty?.id ?? null }}
        reason={reason}
      />
    </Container>
  );
}
