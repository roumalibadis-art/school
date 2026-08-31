import Link from "next/link";
import { notFound } from "next/navigation";
import { Badge, Button, Card } from "@/components/ui";
import { adminGet } from "@/lib/admin";
import { grantPremiumAction, setRolesAction, setUserActiveAction } from "@/lib/admin-actions";
import { formatDate } from "@/lib/format";

interface Detail {
  user: {
    id: string; email: string; firstName: string; lastName: string;
    isActive: boolean; isPremium: boolean; premiumExpiresAt: string | null; roles: string[]; createdAt: string;
  };
  studentId: string | null;
  specialtyName: string | null;
  levelName: string | null;
  favoritesCount: number;
  subscriptionsCount: number;
  lastActivityAt: string | null;
}

const ALL_ROLES = ["Admin", "Moderator", "Student"];

export default async function AdminUserDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  let detail: Detail;
  try {
    detail = await adminGet<Detail>(`/api/admin/users/${id}`);
  } catch {
    notFound();
  }
  const u = detail!.user;

  return (
    <div className="space-y-6">
      <Link href="/admin/users" className="text-xs text-ink-soft no-underline hover:text-ink">← Utilisateurs</Link>
      <div>
        <h1 className="text-2xl font-semibold">{u.firstName} {u.lastName}</h1>
        <p className="text-sm text-ink-muted">{u.email}</p>
        <p className="mt-2 flex flex-wrap gap-2">
          {u.isActive ? <Badge tone="brand">Actif</Badge> : <Badge>Suspendu</Badge>}
          {u.isPremium ? <Badge tone="premium">Premium{u.premiumExpiresAt ? ` — ${formatDate(u.premiumExpiresAt)}` : ""}</Badge> : null}
          {u.roles.map((r) => <Badge key={r}>{r}</Badge>)}
        </p>
      </div>

      <Card className="grid gap-2 p-4 text-sm sm:grid-cols-2">
        <Info label="Matricule" value={detail!.studentId ?? "—"} />
        <Info label="Inscrit le" value={formatDate(u.createdAt)} />
        <Info label="Spécialité" value={detail!.specialtyName ?? "—"} />
        <Info label="Niveau" value={detail!.levelName ?? "—"} />
        <Info label="Favoris" value={String(detail!.favoritesCount)} />
        <Info label="Abonnements" value={String(detail!.subscriptionsCount)} />
        <Info label="Dernière activité" value={detail!.lastActivityAt ? formatDate(detail!.lastActivityAt) : "—"} />
      </Card>

      <div className="grid gap-4 sm:grid-cols-2">
        <Card className="p-4">
          <p className="mb-2 text-sm font-semibold">Compte</p>
          <form action={setUserActiveAction}>
            <input type="hidden" name="id" value={u.id} />
            <input type="hidden" name="active" value={String(!u.isActive)} />
            <Button variant={u.isActive ? "secondary" : "primary"} type="submit">
              {u.isActive ? "Suspendre" : "Réactiver"}
            </Button>
          </form>
        </Card>

        <Card className="p-4">
          <p className="mb-2 text-sm font-semibold">Premium</p>
          <form action={grantPremiumAction} className="flex items-center gap-2">
            <input type="hidden" name="id" value={u.id} />
            <select name="months" className="rounded-md border border-line bg-paper-raised px-2 py-1.5 text-sm">
              {[1, 3, 6, 12].map((m) => <option key={m} value={m}>{m} mois</option>)}
            </select>
            <Button type="submit">Accorder / prolonger</Button>
          </form>
        </Card>

        <Card className="p-4 sm:col-span-2">
          <p className="mb-2 text-sm font-semibold">Rôles</p>
          <form action={setRolesAction} className="flex flex-wrap items-center gap-4">
            <input type="hidden" name="id" value={u.id} />
            {ALL_ROLES.map((r) => (
              <label key={r} className="flex items-center gap-1.5 text-sm">
                <input type="checkbox" name="roles" value={r} defaultChecked={u.roles.includes(r)} />
                {r}
              </label>
            ))}
            <Button type="submit">Enregistrer</Button>
          </form>
        </Card>
      </div>
    </div>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-4">
      <span className="text-ink-soft">{label}</span>
      <span className="text-right">{value}</span>
    </div>
  );
}
