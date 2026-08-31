import type { Metadata } from "next";
import { FacultyCard } from "@/components/cards";
import { Container, EmptyState } from "@/components/ui";
import { apiGetPaged } from "@/lib/api";
import type { Faculty } from "@/lib/types";

export const metadata: Metadata = {
  title: "Facultés",
  description: "Parcourez les facultés de l'USTHB et leurs départements, spécialités et modules.",
  alternates: { canonical: "/faculties" },
};

export default async function FacultiesPage() {
  const faculties = await apiGetPaged<Faculty>("/api/faculties", { query: { pageSize: 100 } }).catch(() => null);

  return (
    <Container className="py-10">
      <h1 className="text-2xl font-semibold">Facultés</h1>
      <p className="mt-2 text-sm text-ink-muted">
        Choisissez une faculté pour parcourir ses départements et spécialités.
      </p>

      <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {faculties?.items.map((f) => (
          <FacultyCard key={f.id} faculty={f} />
        ))}
      </div>

      {!faculties || faculties.items.length === 0 ? (
        <div className="mt-6">
          <EmptyState>Aucune faculté n&apos;a encore été ajoutée.</EmptyState>
        </div>
      ) : null}
    </Container>
  );
}
