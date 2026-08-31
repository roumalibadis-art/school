import type { Metadata } from "next";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { Container, LinkButton } from "@/components/ui";
import { apiGetOrNull } from "@/lib/api";
import { getCurrentUser } from "@/lib/session";
import type { DocumentDto } from "@/lib/types";

type Params = { params: Promise<{ slug: string }> };

export const metadata: Metadata = { title: "Lecture", robots: { index: false } };

export default async function ViewerPage({ params }: Params) {
  const { slug } = await params;
  if (!(await getCurrentUser())) redirect(`/login?next=/documents/${slug}/view`);

  const doc = await apiGetOrNull<DocumentDto>(`/api/documents/${encodeURIComponent(slug)}`);
  if (!doc) notFound();

  return (
    <div className="flex h-[calc(100vh-3.5rem)] flex-col">
      <Container className="flex items-center justify-between gap-4 border-b border-line py-3">
        <div className="min-w-0">
          <Link href={`/documents/${doc.slug}`} className="text-xs text-ink-soft no-underline hover:text-ink">
            ← Retour à la fiche
          </Link>
          <h1 className="truncate text-sm font-semibold">{doc.title}</h1>
        </div>
        <LinkButton href={`/dl/${encodeURIComponent(doc.slug)}`} variant="secondary">
          Télécharger
        </LinkButton>
      </Container>

      <iframe
        title={`Lecture : ${doc.title}`}
        src={`/dl/${encodeURIComponent(doc.slug)}?inline=1`}
        className="w-full flex-1 bg-paper-sunken"
      />
    </div>
  );
}
