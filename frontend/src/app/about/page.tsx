import type { Metadata } from "next";
import { Container } from "@/components/ui";

export const metadata: Metadata = {
  title: "À propos",
  description: "USTHB Study centralise les ressources académiques des étudiants de l'USTHB.",
  alternates: { canonical: "/about" },
};

export default function AboutPage() {
  return (
    <Container className="py-12">
      <div className="prose-neutral mx-auto max-w-prose">
        <h1 className="text-2xl font-semibold">À propos</h1>
        <p className="mt-4 text-sm text-ink-muted">
          USTHB Study rassemble cours, TD, TP, exercices, examens et corrigés, organisés par
          université, faculté, département, spécialité, niveau, semestre et module. L&apos;objectif
          est simple : retrouver rapidement le bon document.
        </p>
        <p className="mt-4 text-sm text-ink-muted">
          Chaque document conserve sa source et son statut de droits. Une procédure de signalement
          permet de demander le retrait d&apos;un contenu.
        </p>
      </div>
    </Container>
  );
}
