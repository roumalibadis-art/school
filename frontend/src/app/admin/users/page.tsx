import Link from "next/link";
import { Badge, Card } from "@/components/ui";
import { adminGetPaged } from "@/lib/admin";
import { formatDate } from "@/lib/format";

interface AdminUser {
  id: string; email: string; firstName: string; lastName: string;
  isActive: boolean; isPremium: boolean; roles: string[]; createdAt: string;
}

export default async function AdminUsersPage({ searchParams }: { searchParams: Promise<{ q?: string; page?: string }> }) {
  const sp = await searchParams;
  const page = Math.max(1, Number(sp.page ?? "1") || 1);
  const q = new URLSearchParams({ page: String(page), pageSize: "25" });
  if (sp.q) q.set("search", sp.q);

  const users = await adminGetPaged<AdminUser>(`/api/admin/users?${q.toString()}`);

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Utilisateurs ({users.total})</h1>

      <form className="flex gap-2" action="/admin/users">
        <input
          name="q"
          defaultValue={sp.q ?? ""}
          placeholder="Nom ou email…"
          className="w-64 rounded-md border border-line bg-paper-raised px-3 py-1.5 text-sm"
        />
        <button className="rounded-md border border-line px-3 py-1.5 text-sm hover:bg-paper-sunken">Chercher</button>
      </form>

      <Card className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="border-b border-line text-left text-xs text-ink-soft">
            <tr>
              <th className="p-3">Nom</th><th className="p-3">Email</th><th className="p-3">Rôles</th>
              <th className="p-3">État</th><th className="p-3">Inscrit</th>
            </tr>
          </thead>
          <tbody>
            {users.items.map((u) => (
              <tr key={u.id} className="border-b border-line last:border-0 hover:bg-paper-sunken">
                <td className="p-3">
                  <Link href={`/admin/users/${u.id}`} className="no-underline font-medium hover:text-brand-strong">
                    {u.firstName} {u.lastName}
                  </Link>
                </td>
                <td className="p-3 text-ink-muted">{u.email}</td>
                <td className="p-3">{u.roles.join(", ")}</td>
                <td className="p-3">
                  <span className="flex gap-1">
                    {u.isActive ? null : <Badge>Suspendu</Badge>}
                    {u.isPremium ? <Badge tone="premium">Premium</Badge> : null}
                  </span>
                </td>
                <td className="p-3 text-ink-soft">{formatDate(u.createdAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>

      <Pager page={users.page} totalPages={users.totalPages} q={sp.q} />
    </div>
  );
}

function Pager({ page, totalPages, q }: { page: number; totalPages: number; q?: string }) {
  if (totalPages <= 1) return null;
  const href = (p: number) => `/admin/users?page=${p}${q ? `&q=${encodeURIComponent(q)}` : ""}`;
  return (
    <div className="flex gap-2 text-sm">
      {page > 1 ? <Link href={href(page - 1)} className="rounded border border-line px-2 py-1 no-underline">←</Link> : null}
      <span className="text-ink-soft">Page {page}/{totalPages}</span>
      {page < totalPages ? <Link href={href(page + 1)} className="rounded border border-line px-2 py-1 no-underline">→</Link> : null}
    </div>
  );
}
