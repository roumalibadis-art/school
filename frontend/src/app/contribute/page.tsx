import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { ContributeForm } from "@/components/contribute-form";
import { authedApiGet } from "@/lib/api";
import { Badge, Card, Container } from "@/components/ui";
import { documentTypeLabel, formatDate } from "@/lib/format";
import { getCurrentUser } from "@/lib/session";

export const metadata: Metadata = { title: "Contribuer", robots: { index: false } };

interface Contribution {
  id: string; title: string; type: string; status: string; reviewNote: string | null; createdAt: string;
}

export default async function ContributePage({ searchParams }: { searchParams: Promise<{ sent?: string }> }) {
  if (!(await getCurrentUser())) redirect("/login?next=/contribute");
  const { sent } = await searchParams;
  const mine = await authedApiGet<Contribution[]>("/api/me/contributions").catch(() => []);

  return (
    <Container className="grid gap-8 py-10 lg:grid-cols-[minmax(0,1fr)_20rem]">
      <div>
        <h1 className="text-2xl font-semibold">Proposer un document</h1>
        <p className="mt-2 max-w-prose text-sm text-ink-muted">
          Partagez un cours, un TD, un examen ou un corrigé. Un modérateur le vérifiera avant publication.
        </p>
        {sent ? (
          <p className="mt-4 rounded-md border border-brand/20 bg-brand-soft p-3 text-sm text-brand-strong">
            Merci ! Votre contribution est en attente de modération.
          </p>
        ) : null}
        <Card className="mt-6 p-6">
          <ContributeForm />
        </Card>
      </div>

      <aside>
        <h2 className="text-sm font-semibold">Mes contributions</h2>
        <ul className="mt-3 space-y-2 text-sm">
          {mine.length === 0 ? <li className="text-ink-soft">Aucune pour le moment.</li> : null}
          {mine.map((c) => (
            <li key={c.id}>
              <Card className="p-3">
                <p className="flex items-center gap-2">
                  <Badge>{documentTypeLabel(c.type)}</Badge>
                  <Badge tone={c.status === "Approved" ? "brand" : c.status === "Rejected" ? "neutral" : "premium"}>
                    {c.status}
                  </Badge>
                </p>
                <p className="mt-1 truncate font-medium">{c.title}</p>
                <p className="text-xs text-ink-soft">{formatDate(c.createdAt)}</p>
                {c.reviewNote ? <p className="mt-1 text-xs text-ink-muted">« {c.reviewNote} »</p> : null}
              </Card>
            </li>
          ))}
        </ul>
        <Link href="/dashboard" className="link mt-4 inline-block text-sm">← Tableau de bord</Link>
      </aside>
    </Container>
  );
}
