import Link from "next/link";
import { Card } from "@/components/ui";
import { adminGet } from "@/lib/admin";

interface Dashboard {
  stats: {
    totalUsers: number; activeUsers: number; premiumUsers: number; conversionRate: number;
    totalDocuments: number; publishedDocuments: number; pendingDocuments: number;
    totalViews: number; totalDownloads: number; revenue: number; currency: string;
    activeSubscriptions: number; pendingContributions: number; openReports: number;
  };
  signupsLast30Days: { date: string; value: number }[];
  revenueLast30Days: { date: string; value: number }[];
  downloadsLast30Days: { date: string; value: number }[];
  topModules: { id: string; label: string; slug: string; count: number }[];
  topDocuments: { id: string; label: string; slug: string; count: number }[];
  topSpecialties: { id: string; label: string; slug: string; count: number }[];
}

export default async function AdminDashboardPage() {
  const dash = await adminGet<Dashboard>("/api/admin/dashboard");
  const s = dash.stats;

  return (
    <div className="space-y-8">
      <h1 className="text-2xl font-semibold">Tableau de bord</h1>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Stat label="Utilisateurs" value={s.totalUsers} sub={`${s.activeUsers} actifs`} />
        <Stat label="Premium" value={s.premiumUsers} sub={`${s.conversionRate}% de conversion`} />
        <Stat label="Documents publiés" value={s.publishedDocuments} sub={`${s.pendingDocuments} en attente`} />
        <Stat label="Revenu" value={`${s.revenue.toLocaleString("fr-FR")} ${s.currency}`} sub={`${s.activeSubscriptions} abonnements actifs`} />
        <Stat label="Vues totales" value={s.totalViews} />
        <Stat label="Téléchargements" value={s.totalDownloads} />
        <Stat label="Contributions en attente" value={s.pendingContributions} highlight={s.pendingContributions > 0} />
        <Stat label="Signalements ouverts" value={s.openReports} highlight={s.openReports > 0} />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <Spark title="Inscriptions (30 j)" points={dash.signupsLast30Days} />
        <Spark title="Revenu (30 j)" points={dash.revenueLast30Days} />
        <Spark title="Téléchargements (30 j)" points={dash.downloadsLast30Days} />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <TopList title="Modules les plus consultés" items={dash.topModules} hrefBase="/modules" />
        <TopList title="Documents les plus consultés" items={dash.topDocuments} hrefBase="/documents" />
        <TopList title="Spécialités les plus actives" items={dash.topSpecialties} hrefBase="/specialties" />
      </div>
    </div>
  );
}

function Stat({ label, value, sub, highlight }: { label: string; value: number | string; sub?: string; highlight?: boolean }) {
  return (
    <Card className={`p-4 ${highlight ? "border-premium/40" : ""}`}>
      <p className="text-xs text-ink-soft">{label}</p>
      <p className="mt-1 text-2xl font-semibold">{typeof value === "number" ? value.toLocaleString("fr-FR") : value}</p>
      {sub ? <p className="mt-0.5 text-xs text-ink-muted">{sub}</p> : null}
    </Card>
  );
}

function Spark({ title, points }: { title: string; points: { date: string; value: number }[] }) {
  const max = Math.max(1, ...points.map((p) => p.value));
  const total = points.reduce((a, p) => a + p.value, 0);
  return (
    <Card className="p-4">
      <p className="text-xs text-ink-soft">{title}</p>
      <p className="mt-1 text-lg font-semibold">{total.toLocaleString("fr-FR")}</p>
      <div className="mt-3 flex h-12 items-end gap-px">
        {points.map((p) => (
          <div
            key={p.date}
            title={`${p.date}: ${p.value}`}
            className="flex-1 rounded-sm bg-brand/60"
            style={{ height: `${Math.max(2, (p.value / max) * 100)}%` }}
          />
        ))}
      </div>
    </Card>
  );
}

function TopList({
  title, items, hrefBase,
}: {
  title: string;
  items: { id: string; label: string; slug: string; count: number }[];
  hrefBase: string;
}) {
  return (
    <Card className="p-4">
      <p className="mb-2 text-sm font-semibold">{title}</p>
      {items.length === 0 ? (
        <p className="text-xs text-ink-soft">Aucune donnée.</p>
      ) : (
        <ol className="space-y-1 text-sm">
          {items.map((item) => (
            <li key={item.id} className="flex justify-between gap-3">
              <Link href={`${hrefBase}/${item.slug}`} className="truncate no-underline hover:text-brand-strong">
                {item.label}
              </Link>
              <span className="shrink-0 text-ink-soft">{item.count.toLocaleString("fr-FR")}</span>
            </li>
          ))}
        </ol>
      )}
    </Card>
  );
}
