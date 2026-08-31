import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { FavoriteButton } from "@/components/favorite-button";
import { Badge, Card, Container, EmptyState } from "@/components/ui";
import { authedApiGet } from "@/lib/api";
import { formatDate } from "@/lib/format";
import { getCurrentUser } from "@/lib/session";

export const metadata: Metadata = { title: "Mes favoris", robots: { index: false } };

interface Favorite {
  id: string;
  kind: "Module" | "Document";
  entityId: string;
  title: string;
  slug: string;
  isPremium: boolean;
  createdAt: string;
}

export default async function FavoritesPage() {
  if (!(await getCurrentUser())) redirect("/login?next=/favorites");
  const favorites = await authedApiGet<Favorite[]>("/api/favorites");

  return (
    <Container className="py-10">
      <h1 className="text-2xl font-semibold">Mes favoris</h1>

      {favorites.length === 0 ? (
        <div className="mt-6">
          <EmptyState>Vous n&apos;avez rien enregistré. Cliquez sur ☆ sur un module ou un document.</EmptyState>
        </div>
      ) : (
        <ul className="mt-6 space-y-3">
          {favorites.map((f) => (
            <li key={f.id}>
              <Card className="flex items-center justify-between gap-4 p-4">
                <Link
                  href={f.kind === "Module" ? `/modules/${f.slug}` : `/documents/${f.slug}`}
                  className="min-w-0 flex-1 no-underline"
                >
                  <p className="flex items-center gap-2">
                    <Badge>{f.kind === "Module" ? "Module" : "Document"}</Badge>
                    {f.isPremium ? <Badge tone="premium">Premium</Badge> : null}
                  </p>
                  <p className="mt-1 truncate text-sm font-semibold">{f.title}</p>
                  <p className="text-xs text-ink-soft">Ajouté le {formatDate(f.createdAt)}</p>
                </Link>
                <FavoriteButton kind={f.kind} entityId={f.entityId} />
              </Card>
            </li>
          ))}
        </ul>
      )}
    </Container>
  );
}
