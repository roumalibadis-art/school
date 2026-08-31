import Link from "next/link";
import { Container } from "@/components/ui";

const nav = [
  { href: "/faculties", label: "Facultés" },
  { href: "/modules", label: "Modules" },
  { href: "/documents", label: "Documents" },
  { href: "/exams", label: "Examens" },
  { href: "/pricing", label: "Premium" },
];

export function SiteHeader() {
  return (
    <header className="sticky top-0 z-40 border-b border-line bg-paper/90 backdrop-blur">
      <Container className="flex h-14 items-center justify-between gap-6">
        <Link href="/" className="flex items-center gap-2 font-serif text-lg font-semibold no-underline">
          <span aria-hidden className="text-brand">◆</span>
          USTHB&nbsp;Study
        </Link>
        <nav className="hidden items-center gap-5 text-sm text-ink-muted sm:flex">
          {nav.map((item) => (
            <Link key={item.href} href={item.href} className="no-underline hover:text-ink">
              {item.label}
            </Link>
          ))}
        </nav>
        <div className="flex items-center gap-3 text-sm">
          <Link href="/search" className="text-ink-muted no-underline hover:text-ink" aria-label="Recherche">
            Rechercher
          </Link>
          <Link
            href="/login"
            className="rounded-md border border-line px-3 py-1.5 no-underline hover:bg-paper-sunken"
          >
            Connexion
          </Link>
        </div>
      </Container>
    </header>
  );
}
