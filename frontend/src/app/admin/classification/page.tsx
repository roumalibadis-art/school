import Link from "next/link";
import { Badge, Card, EmptyState, Pagination } from "@/components/ui";
import { adminGetPaged } from "@/lib/admin";
import { formatDate, reasonLabels } from "@/lib/format";

interface Item {
  id: string; title: string; slug: string; documentStatus: string; classification: string; verification: string;
  reviewReason: string | null; round: number; votes: number; requiredVoters: number; createdAt: string;
}

const queues = [
  { key: "needs-review", label: "À vérifier" },
  { key: "conflicting", label: "Conflits" },
  { key: "awaiting-votes", label: "En attente de votes" },
  { key: "pending", label: "Sans vote" },
  { key: "rejected", label: "Rejetés" },
  { key: "verified", label: "Vérifiés" },
];


export default async function AdminClassificationPage({
  searchParams,
}: { searchParams: Promise<{ queue?: string; q?: string; page?: string }> }) {
  const { queue = "needs-review", q = "", page = "1" } = await searchParams;
  const data = await adminGetPaged<Item>(
    `/api/admin/classification/documents?queue=${encodeURIComponent(queue)}&search=${encodeURIComponent(q)}&page=${Number(page) || 1}&pageSize=20`,
  );

  const href = (p: number) => `/admin/classification?queue=${queue}&q=${encodeURIComponent(q)}&page=${p}`;

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Classification communautaire</h1>

      <nav className="flex flex-wrap gap-2" aria-label="Files de classification">
        {queues.map((t) => (
          <Link key={t.key} href={`/admin/classification?queue=${t.key}`}
            aria-current={queue === t.key ? "page" : undefined}
            className={`rounded-full border px-3 py-1 text-sm no-underline ${queue === t.key ? "border-brand bg-brand-soft text-brand-strong" : "border-line hover:bg-paper-sunken"}`}>
            {t.label}
          </Link>
        ))}
      </nav>

      <form className="flex gap-2" role="search">
        <input type="hidden" name="queue" value={queue} />
        <input name="q" defaultValue={q} placeholder="Rechercher un titre…" aria-label="Rechercher un titre"
          className="w-full max-w-sm rounded-md border border-line bg-paper-raised px-3 py-1.5 text-sm" />
        <button className="rounded-md border border-line px-3 py-1.5 text-sm hover:bg-paper-sunken">Filtrer</button>
      </form>

      <p className="text-sm text-ink-muted">{data.total} document(s)</p>

      {data.items.length === 0 ? (
        <EmptyState>Aucun document dans cette file.</EmptyState>
      ) : (
        <Card className="divide-y divide-line">
          {data.items.map((d) => (
            <Link key={d.id} href={`/admin/classification/${d.id}`}
              className="flex flex-wrap items-center justify-between gap-3 p-4 no-underline hover:bg-paper-sunken">
              <div className="min-w-0">
                <p className="truncate text-sm font-medium">{d.title}</p>
                <p className="text-xs text-ink-soft">
                  {formatDate(d.createdAt)} · tour {d.round}
                  {d.reviewReason ? ` · ${reasonLabels[d.reviewReason] ?? d.reviewReason}` : ""}
                </p>
              </div>
              <div className="flex items-center gap-2">
                <Badge>{d.votes}/{d.requiredVoters} votes</Badge>
                <Badge tone={d.verification === "Verified" ? "brand" : "neutral"}>{d.verification}</Badge>
                <Badge>{d.documentStatus}</Badge>
              </div>
            </Link>
          ))}
        </Card>
      )}
      <Pagination page={data.page} totalPages={data.totalPages} hrefFor={href} />
    </div>
  );
}
