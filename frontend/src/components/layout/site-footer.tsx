import Link from "next/link";
import { Container } from "@/components/ui";

export function SiteFooter() {
  return (
    <footer className="mt-16 border-t border-line bg-paper-sunken/60">
      <Container className="flex flex-col gap-4 py-8 text-sm text-ink-muted sm:flex-row sm:items-center sm:justify-between">
        <p>
          <span className="font-serif font-semibold text-ink">USTHB Study</span> — ressources
          académiques organisées pour les étudiants.
        </p>
        <nav className="flex flex-wrap gap-4">
          <Link href="/about" className="no-underline hover:text-ink">À propos</Link>
          <Link href="/pricing" className="no-underline hover:text-ink">Premium</Link>
          <Link href="/terms" className="no-underline hover:text-ink">Conditions</Link>
          <Link href="/privacy" className="no-underline hover:text-ink">Confidentialité</Link>
          <Link href="/contact" className="no-underline hover:text-ink">Contact</Link>
        </nav>
      </Container>
    </footer>
  );
}
