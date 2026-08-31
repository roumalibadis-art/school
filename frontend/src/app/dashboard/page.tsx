import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { DocumentCard } from "@/components/cards";
import { Badge, Card, Container, EmptyState, SectionHeading } from "@/components/ui";
import { authedApiGet } from "@/lib/api";
import { documentTypeLabel, formatDate } from "@/lib/format";
import { getCurrentUser } from "@/lib/session";
import type { DocumentDto } from "@/lib/types";

export const metadata: Metadata = { title: "Tableau de bord", robots: { index: false } };

interface Dashboard {
  firstName: string;
  specialty: { name: string; slug: string } | null;
  level: { name: string } | null;
  myModules: { id: string; name: string; slug: string; semester: string | null }[];
  recentResources: DashboardDoc[];
  popularExams: DashboardDoc[];
  favoritesCount: number;
  subscription: { isPremium: boolean; expiresAt: string | null; state: string };
}
interface DashboardDoc {
  id: string; title: string; slug: string; type: string; isPremium: boolean; hasPreview: boolean; viewCount: number;
}

function asDocumentDto(d: DashboardDoc): DocumentDto {
  return {
    id: d.id, title: d.title, slug: d.slug, type: d.type as DocumentDto["type"], status: "Published",
    moduleId: "", fileName: "", fileSize: 0, mimeType: "application/pdf", isPremium: d.isPremium,
    rightsStatus: "Unknown", hasPreview: d.hasPreview, viewCount: d.viewCount, downloadCount: 0,
    solutionDocumentIds: [], createdAt: new Date().toISOString(), publishedAt: new Date().toISOString(),
  };
}

export default async function DashboardPage() {
  const user = await getCurrentUser();
  if (!user) redirect("/login?next=/dashboard");
  if (!user.specialty || !user.level) redirect("/profile?welcome=1");

  const dash = await authedApiGet<Dashboard>("/api/me/dashboard");

  return (
    <Container className="py-10">
      <header className="mb-8">
        <h1 className="text-2xl font-semibold">Bonjour, {dash.firstName}</h1>
        <p className="mt-1 flex flex-wrap gap-2 text-sm text-ink-muted">
          {dash.specialty ? <Badge tone="brand">{dash.specialty.name}</Badge> : null}
          {dash.level ? <Badge>{dash.level.name}</Badge> : null}
          <SubscriptionBadge subscription={dash.subscription} />
        </p>
      </header>

      <section className="mb-10">
        <SectionHeading title="Mes modules" />
        {dash.myModules.length > 0 ? (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {dash.myModules.map((m) => (
              <Link key={m.id} href={`/modules/${m.slug}`} className="no-underline">
                <Card className="p-4 transition-shadow hover:shadow-md">
                  <h3 className="text-sm font-semibold">{m.name}</h3>
                  {m.semester ? <p className="mt-1 text-xs text-ink-soft">{m.semester}</p> : null}
                </Card>
              </Link>
            ))}
          </div>
        ) : (
          <EmptyState>
            Aucun module pour votre spécialité et niveau. <Link href="/profile" className="link">Vérifier mon profil</Link>
          </EmptyState>
        )}
      </section>

      <section className="mb-10">
        <SectionHeading title="Ressources récentes" action={{ href: `/specialties/${dash.specialty?.slug ?? ""}`, label: "Ma spécialité" }} />
        {dash.recentResources.length > 0 ? (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {dash.recentResources.map((d) => <DocumentCard key={d.id} doc={asDocumentDto(d)} />)}
          </div>
        ) : (
          <EmptyState>Pas encore de ressources pour votre spécialité.</EmptyState>
        )}
      </section>

      <section className="mb-10">
        <SectionHeading title="Examens populaires" />
        {dash.popularExams.length > 0 ? (
          <ul className="divide-y divide-line rounded-lg border border-line bg-paper-raised">
            {dash.popularExams.map((d) => (
              <li key={d.id}>
                <Link href={`/documents/${d.slug}`} className="flex items-center justify-between gap-4 p-4 no-underline hover:bg-paper-sunken">
                  <span className="truncate text-sm font-medium">{d.title}</span>
                  <span className="shrink-0 text-xs text-ink-soft">{documentTypeLabel(d.type)} · {d.viewCount} vues</span>
                </Link>
              </li>
            ))}
          </ul>
        ) : (
          <EmptyState>Aucun examen populaire pour l&apos;instant.</EmptyState>
        )}
      </section>

      <div className="grid gap-6 sm:grid-cols-2">
        <section>
          <SectionHeading title="Favoris" action={{ href: "/favorites", label: "Voir tout" }} />
          <p className="text-sm text-ink-muted">{dash.favoritesCount} élément(s) enregistré(s).</p>
        </section>
        <section>
          <SectionHeading title="Abonnement" action={{ href: "/subscribe", label: "Gérer" }} />
          <p className="text-sm text-ink-muted">
            {dash.subscription.state === "active"
              ? `Premium actif${dash.subscription.expiresAt ? ` jusqu'au ${formatDate(dash.subscription.expiresAt)}` : ""}.`
              : dash.subscription.state === "expired"
                ? "Votre abonnement Premium a expiré."
                : "Compte gratuit."}
          </p>
          {dash.subscription.state !== "active" ? (
            <Link href="/pricing" className="link mt-1 inline-block text-sm">Voir les offres Premium →</Link>
          ) : null}
        </section>
      </div>
    </Container>
  );
}

function SubscriptionBadge({ subscription }: { subscription: Dashboard["subscription"] }) {
  if (subscription.state === "active" && subscription.expiresAt) {
    return <Badge tone="premium">Premium jusqu&apos;au {formatDate(subscription.expiresAt)}</Badge>;
  }
  if (subscription.state === "expired") {
    return <Badge tone="neutral">Premium expiré</Badge>;
  }
  return (
    <Link href="/pricing" className="no-underline">
      <Badge tone="neutral">Compte gratuit — passer Premium</Badge>
    </Link>
  );
}
