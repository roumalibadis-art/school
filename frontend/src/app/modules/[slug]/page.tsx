import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { DocumentCard } from "@/components/cards";
import { Badge, Container, EmptyState } from "@/components/ui";
import { apiGetOrNull, apiGetPaged } from "@/lib/api";
import { documentTypeLabel } from "@/lib/format";
import type { DocumentDto, Module } from "@/lib/types";

type Params = { params: Promise<{ slug: string }> };

const groupOrder: DocumentDto["type"][] = [
  "Course", "TD", "TP", "Exercise", "ExerciseSolution", "Exam", "ExamSolution",
  "Test", "TestSolution", "Summary", "Other",
];

export async function generateMetadata({ params }: Params): Promise<Metadata> {
  const { slug } = await params;
  const module = await apiGetOrNull<Module>(`/api/modules/slug/${encodeURIComponent(slug)}`);
  if (!module) return { title: "Module introuvable" };
  return {
    title: module.name,
    description: `Cours, TD, TP, examens et corrigés du module ${module.name} (coeff. ${module.coefficient}, ${module.credits} crédits).`,
    alternates: { canonical: `/modules/${module.slug}` },
  };
}

export default async function ModulePage({ params }: Params) {
  const { slug } = await params;
  const module = await apiGetOrNull<Module>(`/api/modules/slug/${encodeURIComponent(slug)}`);
  if (!module) notFound();

  const docs = await apiGetPaged<DocumentDto>("/api/documents", {
    query: { moduleId: module.id, pageSize: 200 },
  });

  const grouped = groupOrder
    .map((type) => ({ type, items: docs.items.filter((d) => d.type === type) }))
    .filter((g) => g.items.length > 0);

  return (
    <Container className="py-10">
      <h1 className="text-2xl font-semibold">{module.name}</h1>
      <p className="mt-2 flex flex-wrap gap-2 text-xs text-ink-muted">
        {module.code ? <Badge>{module.code}</Badge> : null}
        <Badge>Coeff. {module.coefficient}</Badge>
        <Badge>{module.credits} crédits</Badge>
      </p>
      {module.description ? (
        <p className="mt-4 max-w-prose text-sm text-ink-muted">{module.description}</p>
      ) : null}

      <div className="mt-8 space-y-8">
        {grouped.map((group) => (
          <section key={group.type}>
            <h2 className="text-lg font-semibold">{documentTypeLabel(group.type)}</h2>
            <div className="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {group.items.map((d) => (
                <DocumentCard key={d.id} doc={d} />
              ))}
            </div>
          </section>
        ))}
        {grouped.length === 0 ? (
          <EmptyState>Aucun document publié pour ce module pour le moment.</EmptyState>
        ) : null}
      </div>
    </Container>
  );
}
