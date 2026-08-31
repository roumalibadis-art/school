import { Card } from "@/components/ui";
import { adminGetPaged } from "@/lib/admin";

interface Entry {
  id: string; actorEmail: string | null; action: string; entityType: string;
  entityId: string | null; metadata: string | null; occurredAt: string;
}

export default async function AdminAuditPage({ searchParams }: { searchParams: Promise<{ page?: string; action?: string }> }) {
  const sp = await searchParams;
  const page = Math.max(1, Number(sp.page ?? "1") || 1);
  const q = new URLSearchParams({ page: String(page), pageSize: "50" });
  if (sp.action) q.set("action", sp.action);

  const entries = await adminGetPaged<Entry>(`/api/admin/audit?${q.toString()}`);

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Journal d&apos;audit</h1>
      <Card className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="border-b border-line text-left text-xs text-ink-soft">
            <tr><th className="p-3">Quand</th><th className="p-3">Acteur</th><th className="p-3">Action</th><th className="p-3">Cible</th><th className="p-3">Détails</th></tr>
          </thead>
          <tbody>
            {entries.items.map((e) => (
              <tr key={e.id} className="border-b border-line last:border-0 align-top">
                <td className="whitespace-nowrap p-3 text-ink-soft">{new Date(e.occurredAt).toLocaleString("fr-FR")}</td>
                <td className="p-3 text-ink-muted">{e.actorEmail ?? "système"}</td>
                <td className="p-3 font-mono text-xs">{e.action}</td>
                <td className="p-3 text-xs">{e.entityType} {e.entityId ? `· ${e.entityId.slice(0, 8)}` : ""}</td>
                <td className="max-w-xs truncate p-3 font-mono text-xs text-ink-soft">{e.metadata}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
      <div className="flex gap-2 text-sm">
        {page > 1 ? <a href={`/admin/audit?page=${page - 1}`} className="rounded border border-line px-2 py-1 no-underline">←</a> : null}
        <span className="text-ink-soft">Page {page}/{entries.totalPages}</span>
        {page < entries.totalPages ? <a href={`/admin/audit?page=${page + 1}`} className="rounded border border-line px-2 py-1 no-underline">→</a> : null}
      </div>
    </div>
  );
}
