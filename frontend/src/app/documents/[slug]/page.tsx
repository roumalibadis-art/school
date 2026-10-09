import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { FavoriteButton } from "@/components/favorite-button";
import { ReportButton } from "@/components/report-button";
import { Badge, Card, Container, LinkButton } from "@/components/ui";
import { apiGetOrNull, authedApiGet } from "@/lib/api";
import { documentTypeLabel, formatBytes, formatDate } from "@/lib/format";
import { getCurrentUser } from "@/lib/session";
import type { DocumentDto, QuotaStatus } from "@/lib/types";

type Params = { params: Promise<{ slug: string }> };

export async function generateMetadata({ params }: Params): Promise<Metadata> {
  const { slug } = await params;
  const doc = await apiGetOrNull<DocumentDto>(`/api/documents/${encodeURIComponent(slug)}`);
  if (!doc) return { title: "Document introuvable" };
  return {
    title: doc.title,
    description: doc.description ?? `${documentTypeLabel(doc.type)} — ${doc.title}.`,
    alternates: { canonical: `/documents/${doc.slug}` },
    openGraph: {
      title: doc.title,
      type: "article",
      images: doc.hasPreview ? [`/api/documents/${encodeURIComponent(doc.slug)}/preview`] : undefined,
    },
  };
}

export default async function DocumentPage({ params }: Params) {
  const { slug } = await params;
  const [doc, user] = await Promise.all([
    apiGetOrNull<DocumentDto>(`/api/documents/${encodeURIComponent(slug)}`),
    getCurrentUser(),
  ]);
  if (!doc) notFound();

  // Tell the reader about any download allowance *before* it can block them.
  const quota = user
    ? (await authedApiGet<{ quota: QuotaStatus }>("/api/classification/me").catch(() => null))?.quota ?? null
    : null;
  const showQuota = !!quota && quota.enabled && !quota.exempt && !doc.isPremium;

  return (
    <Container className="grid gap-8 py-10 lg:grid-cols-[minmax(0,1fr)_20rem]">
      <article>
        <div className="flex flex-wrap items-center gap-2">
          <Badge tone="brand">{documentTypeLabel(doc.type)}</Badge>
          {doc.isPremium ? <Badge tone="premium">Premium</Badge> : <Badge>Gratuit</Badge>}
        </div>
        <h1 className="mt-3 text-2xl font-semibold">{doc.title}</h1>
        {doc.description ? <p className="mt-3 max-w-prose text-sm text-ink-muted">{doc.description}</p> : null}

        <div className="mt-6 overflow-hidden rounded-lg border border-line bg-paper-sunken">
          {doc.hasPreview ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={`/api/documents/${encodeURIComponent(doc.slug)}/preview`}
              alt={`Aperçu de la première page de « ${doc.title} »`}
              className="mx-auto max-h-[70vh] w-auto"
            />
          ) : (
            <p className="p-10 text-center text-sm text-ink-soft">Aperçu indisponible pour ce document.</p>
          )}
        </div>

        {doc.isPremium ? (
          <p className="mt-4 rounded-md border border-premium/25 bg-premium-soft p-3 text-sm text-premium">
            Ce document est réservé aux abonnés Premium. <Link href="/pricing" className="underline">Voir les offres</Link>.
          </p>
        ) : null}
      </article>

      <aside className="space-y-4">
        <Card className="p-4">
          {user ? (
            <div className="space-y-2">
              <LinkButton href={`/documents/${doc.slug}/view`} variant="primary" className="w-full">
                Lire le document
              </LinkButton>
              <LinkButton href={`/dl/${encodeURIComponent(doc.slug)}`} variant="secondary" className="w-full" plain>
                Télécharger
              </LinkButton>
              <FavoriteButton kind="Document" entityId={doc.id} className="w-full justify-center" />
              {showQuota ? (
                <p className="rounded-md bg-paper-sunken p-2 text-xs text-ink-muted">
                  {quota.remaining > 0
                    ? <>Il vous reste <strong>{quota.remaining}</strong> téléchargement{quota.remaining > 1 ? "s" : ""} gratuit{quota.remaining > 1 ? "s" : ""}. </>
                    : <>Vous avez utilisé vos téléchargements gratuits. </>}
                  <Link href="/classify" className="link">Classez quelques documents</Link> pour en gagner d’autres.
                </p>
              ) : null}
            </div>
          ) : (
            <LinkButton href={`/login?next=/documents/${doc.slug}`} variant="primary" className="w-full">
              {doc.isPremium ? "Se connecter pour accéder" : "Se connecter pour télécharger"}
            </LinkButton>
          )}
          <dl className="mt-4 space-y-2 text-sm">
            <Row label="Type" value={documentTypeLabel(doc.type)} />
            {doc.pageCount ? <Row label="Pages" value={String(doc.pageCount)} /> : null}
            <Row label="Taille" value={formatBytes(doc.fileSize)} />
            <Row label="Format" value={doc.mimeType} />
            {doc.publishedAt ? <Row label="Publié le" value={formatDate(doc.publishedAt)} /> : null}
            <Row label="Vues" value={String(doc.viewCount)} />
            {doc.source ? <Row label="Source" value={doc.source} /> : null}
          </dl>
        </Card>

        {doc.solutionDocumentIds.length > 0 ? (
          <Card className="p-4 text-sm">
            <p className="font-semibold">Corrigé(s) disponible(s)</p>
            <p className="mt-1 text-ink-muted">
              Ce document possède {doc.solutionDocumentIds.length} corrigé(s) lié(s).
            </p>
          </Card>
        ) : null}

        <ReportButton slug={doc.slug} />
      </aside>
    </Container>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-4">
      <dt className="text-ink-soft">{label}</dt>
      <dd className="text-right text-ink">{value}</dd>
    </div>
  );
}
