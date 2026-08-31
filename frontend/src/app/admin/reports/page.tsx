import Link from "next/link";
import { Badge, Button, Card, EmptyState } from "@/components/ui";
import { adminGetPaged } from "@/lib/admin";
import { resolveReportAction } from "@/lib/admin-actions";
import { formatDate } from "@/lib/format";

interface Report {
  id: string; documentTitle: string; documentSlug: string;
  reason: string; comment: string | null; status: string; createdAt: string;
}

const reasonLabels: Record<string, string> = {
  WrongModule: "Mauvais module", WrongYear: "Mauvaise année", Unreadable: "Illisible",
  Duplicate: "Doublon", IncorrectInformation: "Info incorrecte", Copyright: "Droits d'auteur", Other: "Autre",
};

export default async function AdminReportsPage() {
  const reports = await adminGetPaged<Report>("/api/admin/reports?status=Open&pageSize=50");

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Signalements ouverts ({reports.total})</h1>

      {reports.items.length === 0 ? (
        <EmptyState>Aucun signalement en attente.</EmptyState>
      ) : (
        reports.items.map((r) => (
          <Card key={r.id} className="p-4">
            <div className="flex flex-wrap items-center gap-2">
              <Badge>{reasonLabels[r.reason] ?? r.reason}</Badge>
              <span className="text-xs text-ink-soft">{formatDate(r.createdAt)}</span>
            </div>
            <p className="mt-1 text-sm">
              Document :{" "}
              <Link href={`/documents/${r.documentSlug}`} className="link">{r.documentTitle}</Link>
            </p>
            {r.comment ? <p className="mt-1 text-sm text-ink-muted">« {r.comment} »</p> : null}

            <form action={resolveReportAction} className="mt-3 flex flex-wrap items-center gap-2">
              <input type="hidden" name="id" value={r.id} />
              <input
                name="note"
                placeholder="Note de résolution…"
                className="min-w-[16rem] flex-1 rounded-md border border-line bg-paper-raised px-3 py-1.5 text-sm"
              />
              <Button type="submit" name="status" value="Resolved">Résolu</Button>
              <Button type="submit" name="status" value="Dismissed" variant="secondary">Rejeter</Button>
            </form>
          </Card>
        ))
      )}
    </div>
  );
}
