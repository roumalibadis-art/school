import type { Metadata } from "next";
import Link from "next/link";
import { DocumentCard } from "@/components/cards";
import { Container, EmptyState, Pagination } from "@/components/ui";
import { apiGetPaged } from "@/lib/api";
import { documentTypeLabels } from "@/lib/format";
import type { DocumentDto, DocumentType } from "@/lib/types";

export const metadata: Metadata = {
  title: "Documents",
  description: "Tous les documents publiés : cours, TD, TP, examens et corrigés.",
  alternates: { canonical: "/documents" },
};

const filterTypes = Object.keys(documentTypeLabels) as DocumentType[];

type Search = { searchParams: Promise<{ page?: string; type?: string; q?: string }> };

export default async function DocumentsPage({ searchParams }: Search) {
  const sp = await searchParams;
  const page = Math.max(1, Number(sp.page ?? "1") || 1);
  const activeType = filterTypes.includes(sp.type as DocumentType) ? (sp.type as DocumentType) : undefined;

  const docs = await apiGetPaged<DocumentDto>("/api/documents", {
    query: { page, pageSize: 24, type: activeType, search: sp.q },
  }).catch(() => null);

  const hrefWith = (patch: Record<string, string | undefined>) => {
    const params = new URLSearchParams();
    const merged = { type: activeType, q: sp.q, ...patch };
    for (const [k, v] of Object.entries(merged)) if (v) params.set(k, v);
    const s = params.toString();
    return `/documents${s ? `?${s}` : ""}`;
  };

  return (
    <Container className="py-10">
      <h1 className="text-2xl font-semibold">Documents</h1>

      <div className="mt-4 flex flex-wrap gap-2">
        <Link
          href={hrefWith({ type: undefined, page: undefined })}
          className={`rounded-full border px-3 py-1 text-xs no-underline ${!activeType ? "border-brand bg-brand-soft text-brand-strong" : "border-line hover:bg-paper-sunken"}`}
        >
          Tous
        </Link>
        {filterTypes.map((t) => (
          <Link
            key={t}
            href={hrefWith({ type: t, page: undefined })}
            className={`rounded-full border px-3 py-1 text-xs no-underline ${activeType === t ? "border-brand bg-brand-soft text-brand-strong" : "border-line hover:bg-paper-sunken"}`}
          >
            {documentTypeLabels[t]}
          </Link>
        ))}
      </div>

      {docs && docs.items.length > 0 ? (
        <>
          <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {docs.items.map((d) => (
              <DocumentCard key={d.id} doc={d} />
            ))}
          </div>
          <Pagination page={docs.page} totalPages={docs.totalPages} hrefFor={(p) => hrefWith({ page: String(p) })} />
        </>
      ) : (
        <div className="mt-6">
          <EmptyState>Aucun document ne correspond à ces critères.</EmptyState>
        </div>
      )}
    </Container>
  );
}
