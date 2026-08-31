import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ModuleCard } from "@/components/cards";
import { Container } from "@/components/ui";
import { apiGetOrNull, apiGetPaged } from "@/lib/api";
import type { Level, Module, Semester, Specialty } from "@/lib/types";

type Params = { params: Promise<{ slug: string }> };

async function loadSpecialty(slug: string) {
  const specialty = await apiGetOrNull<Specialty>(`/api/specialties/slug/${encodeURIComponent(slug)}`);
  if (!specialty) return null;

  const levels = await apiGetPaged<Level>("/api/levels", { query: { parentId: specialty.id, pageSize: 100 } });
  const structure = await Promise.all(
    levels.items.map(async (level) => {
      const semesters = await apiGetPaged<Semester>("/api/semesters", { query: { parentId: level.id, pageSize: 100 } });
      const withModules = await Promise.all(
        semesters.items.map(async (semester) => ({
          semester,
          modules: (await apiGetPaged<Module>("/api/modules", { query: { parentId: semester.id, pageSize: 100 } })).items,
        })),
      );
      return { level, semesters: withModules };
    }),
  );
  return { specialty, structure };
}

export async function generateMetadata({ params }: Params): Promise<Metadata> {
  const { slug } = await params;
  const specialty = await apiGetOrNull<Specialty>(`/api/specialties/slug/${encodeURIComponent(slug)}`);
  if (!specialty) return { title: "Spécialité introuvable" };
  return {
    title: specialty.name,
    description: specialty.description ?? `Niveaux, semestres et modules de la spécialité ${specialty.name}.`,
    alternates: { canonical: `/specialties/${specialty.slug}` },
  };
}

export default async function SpecialtyPage({ params }: Params) {
  const { slug } = await params;
  const data = await loadSpecialty(slug);
  if (!data) notFound();
  const { specialty, structure } = data;

  return (
    <Container className="py-10">
      <h1 className="text-2xl font-semibold">{specialty.name}</h1>
      {specialty.description ? <p className="mt-2 max-w-prose text-sm text-ink-muted">{specialty.description}</p> : null}

      <div className="mt-8 space-y-10">
        {structure.map(({ level, semesters }) => (
          <section key={level.id}>
            <h2 className="text-lg font-semibold">
              {level.name} <span className="text-ink-soft">· {level.shortName}</span>
            </h2>
            {semesters.map(({ semester, modules }) => (
              <div key={semester.id} className="mt-4">
                <h3 className="text-sm font-semibold text-ink-muted">{semester.name}</h3>
                <div className="mt-2 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  {modules.map((m) => (
                    <ModuleCard key={m.id} module={m} />
                  ))}
                  {modules.length === 0 ? <p className="text-sm text-ink-soft">Aucun module.</p> : null}
                </div>
              </div>
            ))}
          </section>
        ))}
        {structure.length === 0 ? (
          <p className="text-sm text-ink-soft">
            Aucun niveau n&apos;est encore configuré. <Link href="/faculties" className="link">Retour aux facultés</Link>
          </p>
        ) : null}
      </div>
    </Container>
  );
}
