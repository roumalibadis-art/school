import Link from "next/link";
import { DocumentCard, ModuleCard } from "@/components/cards";
import { SearchBox } from "@/components/search-box";
import { Container, EmptyState, LinkButton, SectionHeading } from "@/components/ui";
import { apiGetPaged } from "@/lib/api";
import { quickNav } from "@/lib/format";
import type { DocumentDto, Module } from "@/lib/types";

export const revalidate = 120;

export default async function HomePage() {
  const [modules, recent] = await Promise.all([
    apiGetPaged<Module>("/api/modules", { query: { pageSize: 8 } }).catch(() => null),
    apiGetPaged<DocumentDto>("/api/documents", { query: { pageSize: 6 } }).catch(() => null),
  ]);

  return (
    <>
      {/* Hero */}
      <section className="border-b border-line bg-gradient-to-b from-paper to-paper-sunken/50">
        <Container className="py-16 sm:py-20">
          <div className="mx-auto max-w-2xl text-center">
            <h1 className="text-3xl font-semibold sm:text-4xl">
              Toutes vos ressources USTHB au même endroit.
            </h1>
            <p className="mx-auto mt-4 max-w-prose text-ink-muted">
              Trouvez cours, TD, TP, examens et corrigés organisés par spécialité, niveau et module.
            </p>
            <div className="mx-auto mt-8 max-w-xl">
              <SearchBox size="lg" />
            </div>
            <div className="mt-4 flex justify-center gap-3">
              <LinkButton href="/faculties" variant="primary">
                Explorer les ressources
              </LinkButton>
              <LinkButton href="/pricing" variant="secondary">
                Passer Premium
              </LinkButton>
            </div>
          </div>
        </Container>
      </section>

      {/* Quick navigation */}
      <Container className="py-10">
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-5">
          {quickNav.map((item) => (
            <li key={item.type}>
              <Link
                href={`/documents?type=${item.type}`}
                className="flex flex-col items-center gap-2 rounded-lg border border-line bg-paper-raised p-4 text-sm font-medium no-underline shadow-card transition-shadow hover:shadow-md"
              >
                <span aria-hidden className="text-2xl">{item.emoji}</span>
                {item.label}
              </Link>
            </li>
          ))}
        </ul>
      </Container>

      {/* Popular modules */}
      <Container className="py-6">
        <SectionHeading title="Modules populaires" action={{ href: "/modules", label: "Tous les modules" }} />
        {modules && modules.items.length > 0 ? (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {modules.items.map((m) => (
              <ModuleCard key={m.id} module={m} />
            ))}
          </div>
        ) : (
          <EmptyState>Les modules apparaîtront ici une fois ajoutés par l&apos;équipe.</EmptyState>
        )}
      </Container>

      {/* Recently added */}
      <Container className="py-10">
        <SectionHeading title="Ajouts récents" action={{ href: "/documents", label: "Tous les documents" }} />
        {recent && recent.items.length > 0 ? (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {recent.items.map((d) => (
              <DocumentCard key={d.id} doc={d} />
            ))}
          </div>
        ) : (
          <EmptyState>Aucun document publié pour le moment.</EmptyState>
        )}
      </Container>

      {/* Why */}
      <section className="border-t border-line bg-paper-sunken/40">
        <Container className="grid gap-6 py-12 sm:grid-cols-2 lg:grid-cols-3">
          {[
            ["Contenu organisé", "Chaque document est rattaché à une spécialité, un niveau et un module."],
            ["Recherche rapide", "« algo examen 2025 » vous amène directement au bon document."],
            ["Archive d'examens", "Sujets des années précédentes, avec leurs corrigés quand ils existent."],
            ["Corrigés", "Naviguez d'un examen à son corrigé en un clic."],
            ["Accès mobile", "Consultez et téléchargez depuis votre téléphone."],
            ["Premium", "Débloquez les ressources Premium avec un abonnement étudiant."],
          ].map(([title, body]) => (
            <div key={title}>
              <h3 className="text-base font-semibold">{title}</h3>
              <p className="mt-1 text-sm text-ink-muted">{body}</p>
            </div>
          ))}
        </Container>
      </section>
    </>
  );
}
