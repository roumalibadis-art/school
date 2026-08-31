import type { Metadata } from "next";
import { DocumentCard } from "@/components/cards";
import { Container, EmptyState, Pagination } from "@/components/ui";
import { apiGetPaged } from "@/lib/api";
import type { DocumentDto } from "@/lib/types";

export const metadata: Metadata = {
  title: "Examens",
  description: "Archive des sujets d'examen de l'USTHB, avec leurs corrigés quand ils existent.",
  alternates: { canonical: "/exams" },
};

type Search = { searchParams: Promise<{ page?: string }> };

export default async function ExamsPage({ searchParams }: Search) {
  const sp = await searchParams;
  const page = Math.max(1, Number(sp.page ?? "1") || 1);

  const exams = await apiGetPaged<DocumentDto>("/api/documents", {
    query: { type: "Exam", page, pageSize: 24 },
  }).catch(() => null);

  return (
    <Container className="py-10">
      <h1 className="text-2xl font-semibold">Examens</h1>
      <p className="mt-2 text-sm text-ink-muted">Sujets d&apos;examen classés par date de publication.</p>

      {exams && exams.items.length > 0 ? (
        <>
          <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {exams.items.map((d) => (
              <DocumentCard key={d.id} doc={d} />
            ))}
          </div>
          <Pagination page={exams.page} totalPages={exams.totalPages} hrefFor={(p) => `/exams?page=${p}`} />
        </>
      ) : (
        <div className="mt-6">
          <EmptyState>Aucun examen publié pour le moment.</EmptyState>
        </div>
      )}
    </Container>
  );
}
