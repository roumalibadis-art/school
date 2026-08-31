import type { DocumentType } from "@/lib/types";

export const documentTypeLabels: Record<DocumentType, string> = {
  Course: "Cours",
  TD: "TD",
  TP: "TP",
  Exam: "Examen",
  ExamSolution: "Corrigé d'examen",
  Test: "Test",
  TestSolution: "Corrigé de test",
  Exercise: "Exercice",
  ExerciseSolution: "Corrigé d'exercice",
  Summary: "Résumé",
  Other: "Autre",
};

export const documentTypeLabel = (type: string) =>
  documentTypeLabels[type as DocumentType] ?? type;

export const quickNav: { type: DocumentType; label: string; emoji: string }[] = [
  { type: "Course", label: "Cours", emoji: "📚" },
  { type: "TD", label: "TD", emoji: "📐" },
  { type: "TP", label: "TP", emoji: "🧪" },
  { type: "Exam", label: "Examens", emoji: "📝" },
  { type: "ExamSolution", label: "Corrigés", emoji: "✅" },
];

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} o`;
  const kb = bytes / 1024;
  if (kb < 1024) return `${kb.toFixed(0)} Ko`;
  return `${(kb / 1024).toFixed(1)} Mo`;
}

export function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("fr-FR", { day: "numeric", month: "long", year: "numeric" });
}
