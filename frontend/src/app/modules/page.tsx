import type { Metadata } from "next";
import { ModuleCard } from "@/components/cards";
import { Container, EmptyState, Pagination } from "@/components/ui";
import { apiGetPaged } from "@/lib/api";
import type { Module } from "@/lib/types";

export const metadata: Metadata = {
  title: "Modules",
  description: "Tous les modules référencés, avec leurs cours, TD, TP et examens.",
  alternates: { canonical: "/modules" },
};

type Search = { params?: never; searchParams: Promise<{ page?: string; q?: string }> };

export default async function ModulesPage({ searchParams }: Search) {
  const sp = await searchParams;
  const page = Math.max(1, Number(sp.page ?? "1") || 1);

  const modules = await apiGetPaged<Module>("/api/modules", {
    query: { page, pageSize: 24, search: sp.q },
  }).catch(() => null);

  return (
    <Container className="py-10">
      <h1 className="text-2xl font-semibold">Modules</h1>

      {modules && modules.items.length > 0 ? (
        <>
          <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {modules.items.map((m) => (
              <ModuleCard key={m.id} module={m} />
            ))}
          </div>
          <Pagination
            page={modules.page}
            totalPages={modules.totalPages}
            hrefFor={(p) => `/modules?page=${p}${sp.q ? `&q=${encodeURIComponent(sp.q)}` : ""}`}
          />
        </>
      ) : (
        <div className="mt-6">
          <EmptyState>Aucun module trouvé.</EmptyState>
        </div>
      )}
    </Container>
  );
}
