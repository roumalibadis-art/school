import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { Container } from "@/components/ui";
import { requireStaff } from "@/lib/admin";

export const metadata: Metadata = { title: "Administration", robots: { index: false } };

const nav = [
  { href: "/admin", label: "Tableau de bord" },
  { href: "/admin/users", label: "Utilisateurs" },
  { href: "/admin/contributions", label: "Contributions" },
  { href: "/admin/reports", label: "Signalements" },
  { href: "/admin/payments", label: "Paiements" },
  { href: "/admin/plans", label: "Offres Premium" },
  { href: "/admin/audit", label: "Journal d'audit" },
];

export default async function AdminLayout({ children }: { children: React.ReactNode }) {
  const staff = await requireStaff();
  if (!staff) redirect("/login?next=/admin");

  return (
    <Container className="grid gap-8 py-8 lg:grid-cols-[13rem_minmax(0,1fr)]">
      <aside>
        <p className="mb-3 text-xs font-semibold uppercase tracking-wide text-ink-soft">Administration</p>
        <nav className="flex flex-col gap-1 text-sm">
          {nav.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className="rounded-md px-3 py-2 no-underline text-ink-muted hover:bg-paper-sunken hover:text-ink"
            >
              {item.label}
            </Link>
          ))}
        </nav>
        <p className="mt-6 text-xs text-ink-soft">Connecté : {staff.firstName} ({staff.roles.join(", ")})</p>
      </aside>
      <div>{children}</div>
    </Container>
  );
}
