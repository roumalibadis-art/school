import { Badge, Button, Card, EmptyState } from "@/components/ui";
import { adminGetPaged } from "@/lib/admin";
import { moderateContributionAction } from "@/lib/admin-actions";
import { documentTypeLabel, formatDate, formatBytes } from "@/lib/format";

interface Contribution {
  id: string; title: string; type: string; description: string | null;
  fileName: string; fileSize: number; status: string; createdAt: string;
}

export default async function AdminContributionsPage() {
  const contributions = await adminGetPaged<Contribution>("/api/admin/contributions?status=Pending&pageSize=50");

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Contributions en attente ({contributions.total})</h1>

      {contributions.items.length === 0 ? (
        <EmptyState>Rien à modérer.</EmptyState>
      ) : (
        contributions.items.map((c) => (
          <Card key={c.id} className="p-4">
            <div className="flex flex-wrap items-center gap-2">
              <Badge tone="brand">{documentTypeLabel(c.type)}</Badge>
              <span className="text-xs text-ink-soft">{formatDate(c.createdAt)} · {c.fileName} ({formatBytes(c.fileSize)})</span>
            </div>
            <h2 className="mt-1 font-semibold">{c.title}</h2>
            {c.description ? <p className="mt-1 text-sm text-ink-muted">{c.description}</p> : null}

            <form action={moderateContributionAction} className="mt-3 flex flex-wrap items-center gap-2">
              <input type="hidden" name="id" value={c.id} />
              <input
                name="note"
                placeholder="Note (optionnelle)…"
                className="min-w-[16rem] flex-1 rounded-md border border-line bg-paper-raised px-3 py-1.5 text-sm"
              />
              <label className="flex items-center gap-1.5 text-sm">
                <input type="checkbox" name="publishNow" value="true" defaultChecked />
                Publier directement
              </label>
              <Button type="submit" name="decision" value="approve">Approuver</Button>
              <Button type="submit" name="decision" value="reject" variant="secondary">Refuser</Button>
            </form>
          </Card>
        ))
      )}
    </div>
  );
}
