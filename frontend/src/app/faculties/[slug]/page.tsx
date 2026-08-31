import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Card, Container } from "@/components/ui";
import { apiGetOrNull, apiGetPaged } from "@/lib/api";
import type { Department, Faculty, Specialty } from "@/lib/types";

type Params = { params: Promise<{ slug: string }> };

async function loadFaculty(slug: string) {
  const faculty = await apiGetOrNull<Faculty>(`/api/faculties/slug/${encodeURIComponent(slug)}`);
  if (!faculty) return null;
  const departments = await apiGetPaged<Department>("/api/departments", {
    query: { parentId: faculty.id, pageSize: 100 },
  });
  const specialtiesByDept = await Promise.all(
    departments.items.map((d) =>
      apiGetPaged<Specialty>("/api/specialties", { query: { parentId: d.id, pageSize: 100 } }).then((s) => ({
        department: d,
        specialties: s.items,
      })),
    ),
  );
  return { faculty, specialtiesByDept };
}

export async function generateMetadata({ params }: Params): Promise<Metadata> {
  const { slug } = await params;
  const faculty = await apiGetOrNull<Faculty>(`/api/faculties/slug/${encodeURIComponent(slug)}`);
  if (!faculty) return { title: "Faculté introuvable" };
  return {
    title: faculty.name,
    description: `Départements, spécialités et modules de ${faculty.name} à l'USTHB.`,
    alternates: { canonical: `/faculties/${faculty.slug}` },
  };
}

export default async function FacultyPage({ params }: Params) {
  const { slug } = await params;
  const data = await loadFaculty(slug);
  if (!data) notFound();
  const { faculty, specialtiesByDept } = data;

  return (
    <Container className="py-10">
      <nav className="text-sm text-ink-soft">
        <Link href="/faculties" className="no-underline hover:text-ink">Facultés</Link> / {faculty.name}
      </nav>
      <h1 className="mt-2 text-2xl font-semibold">{faculty.name}</h1>

      <div className="mt-6 space-y-8">
        {specialtiesByDept.map(({ department, specialties }) => (
          <section key={department.id}>
            <h2 className="text-lg font-semibold">{department.name}</h2>
            <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {specialties.map((s) => (
                <Link key={s.id} href={`/specialties/${s.slug}`} className="no-underline">
                  <Card className="p-4 transition-shadow hover:shadow-md">
                    <h3 className="text-sm font-semibold">{s.name}</h3>
                    {s.description ? (
                      <p className="mt-1 line-clamp-2 text-xs text-ink-muted">{s.description}</p>
                    ) : null}
                  </Card>
                </Link>
              ))}
              {specialties.length === 0 ? (
                <p className="text-sm text-ink-soft">Aucune spécialité listée.</p>
              ) : null}
            </div>
          </section>
        ))}
        {specialtiesByDept.length === 0 ? (
          <p className="text-sm text-ink-soft">Aucun département listé pour cette faculté.</p>
        ) : null}
      </div>
    </Container>
  );
}
