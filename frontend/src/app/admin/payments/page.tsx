import { Badge, Button, Card, EmptyState } from "@/components/ui";
import { adminGetPaged } from "@/lib/admin";
import { resolvePaymentAction } from "@/lib/admin-actions";
import { formatDate } from "@/lib/format";

interface AdminPayment {
  id: string; userEmail: string; userName: string; planName: string | null;
  amount: number; currency: string; reference: string; status: string; createdAt: string; adminNote: string | null;
}

export default async function AdminPaymentsPage({ searchParams }: { searchParams: Promise<{ status?: string }> }) {
  const status = (await searchParams).status ?? "Pending";
  const payments = await adminGetPaged<AdminPayment>(`/api/admin/payments?status=${status}&pageSize=50`);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Paiements — {status}</h1>
        <div className="flex gap-2 text-xs">
          {["Pending", "Success", "Failed"].map((s) => (
            <a key={s} href={`/admin/payments?status=${s}`} className={`rounded-full border px-3 py-1 no-underline ${s === status ? "border-brand bg-brand-soft text-brand-strong" : "border-line"}`}>
              {s}
            </a>
          ))}
        </div>
      </div>

      {payments.items.length === 0 ? (
        <EmptyState>Aucun paiement.</EmptyState>
      ) : (
        payments.items.map((p) => (
          <Card key={p.id} className="p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <p className="font-semibold">{p.amount.toLocaleString("fr-FR")} {p.currency} · {p.planName ?? "—"}</p>
                <p className="text-sm text-ink-muted">{p.userName} ({p.userEmail})</p>
                <p className="text-xs text-ink-soft">Réf. {p.reference} · {formatDate(p.createdAt)}</p>
              </div>
              <Badge tone={p.status === "Success" ? "brand" : p.status === "Failed" ? "neutral" : "premium"}>{p.status}</Badge>
            </div>

            {p.status === "Pending" ? (
              <form action={resolvePaymentAction} className="mt-3 flex flex-wrap items-center gap-2">
                <input type="hidden" name="id" value={p.id} />
                <input
                  name="note"
                  placeholder="Note (ex. CCP vérifié)…"
                  className="min-w-[16rem] flex-1 rounded-md border border-line bg-paper-raised px-3 py-1.5 text-sm"
                />
                <Button type="submit" name="decision" value="approve">Approuver</Button>
                <Button type="submit" name="decision" value="reject" variant="secondary">Rejeter</Button>
              </form>
            ) : p.adminNote ? (
              <p className="mt-2 text-xs text-ink-soft">Note : {p.adminNote}</p>
            ) : null}
          </Card>
        ))
      )}
    </div>
  );
}
