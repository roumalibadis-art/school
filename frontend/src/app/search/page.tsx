import type { Metadata } from "next";
import { SearchResultRow } from "@/components/cards";
import { SearchBox } from "@/components/search-box";
import { Container, EmptyState, Pagination } from "@/components/ui";
import { apiGetPaged } from "@/lib/api";
import type { SearchHit } from "@/lib/types";

export const metadata: Metadata = {
  title: "Recherche",
  description: "Recherchez un module, un cours ou un examen sur USTHB Study.",
  robots: { index: false },
};

type Search = { searchParams: Promise<{ q?: string; page?: string; type?: string }> };

export default async function SearchPage({ searchParams }: Search) {
  const sp = await searchParams;
  const q = (sp.q ?? "").trim();
  const page = Math.max(1, Number(sp.page ?? "1") || 1);

  const results =
    q.length > 0
      ? await apiGetPaged<SearchHit>("/api/search", {
          query: { q, type: sp.type, page, pageSize: 20 },
          revalidate: 0,
        }).catch(() => null)
      : null;

  return (
    <Container className="py-10">
      <h1 className="text-2xl font-semibold">Recherche</h1>
      <div className="mt-4 max-w-xl">
        <SearchBox autoFocus defaultValue={q} />
      </div>

      {q.length === 0 ? (
        <p className="mt-8 text-sm text-ink-muted">
          Essayez « algo examen 2025 », « bases de données L2 » ou « analyse rattrapage ».
        </p>
      ) : results && results.items.length > 0 ? (
        <>
          <p className="mt-6 text-sm text-ink-soft">
            {results.total} résultat{results.total > 1 ? "s" : ""} pour « {q} »
          </p>
          <div className="mt-3 space-y-3">
            {results.items.map((hit) => (
              <SearchResultRow key={hit.id} hit={hit} />
            ))}
          </div>
          <Pagination
            page={results.page}
            totalPages={results.totalPages}
            hrefFor={(p) => `/search?q=${encodeURIComponent(q)}&page=${p}`}
          />
        </>
      ) : (
        <div className="mt-6">
          <EmptyState>Aucun résultat pour « {q} ».</EmptyState>
        </div>
      )}
    </Container>
  );
}
