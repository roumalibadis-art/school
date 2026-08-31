import { Badge, Button, Card } from "@/components/ui";
import { adminGet } from "@/lib/admin";
import { deletePlanAction, savePlanAction } from "@/lib/admin-actions";

interface Plan {
  id: string; name: string; description: string | null; durationDays: number;
  price: number; currency: string; features: string[]; isActive: boolean; displayOrder: number;
}

export default async function AdminPlansPage() {
  const plans = await adminGet<Plan[]>("/api/admin/subscription-plans");

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Offres Premium</h1>

      <div className="grid gap-4 lg:grid-cols-2">
        {plans.map((p) => (
          <Card key={p.id} className="p-4">
            <PlanForm plan={p} />
            <form action={deletePlanAction} className="mt-2">
              <input type="hidden" name="id" value={p.id} />
              <button className="text-xs text-red-700 hover:underline">Désactiver cette offre</button>
            </form>
          </Card>
        ))}
      </div>

      <Card className="p-4">
        <p className="mb-3 text-sm font-semibold">Nouvelle offre</p>
        <PlanForm />
      </Card>
    </div>
  );
}

function PlanForm({ plan }: { plan?: Plan }) {
  return (
    <form action={savePlanAction} className="space-y-2 text-sm">
      {plan ? <input type="hidden" name="id" value={plan.id} /> : null}
      <div className="grid gap-2 sm:grid-cols-2">
        <Field name="name" label="Nom" defaultValue={plan?.name} />
        <Field name="durationDays" label="Durée (jours)" type="number" defaultValue={plan?.durationDays ?? 30} />
        <Field name="price" label="Prix" type="number" defaultValue={plan?.price ?? 0} />
        <Field name="currency" label="Devise" defaultValue={plan?.currency ?? "DZD"} />
        <Field name="displayOrder" label="Ordre" type="number" defaultValue={plan?.displayOrder ?? 0} />
      </div>
      <Field name="description" label="Description" defaultValue={plan?.description ?? ""} required={false} />
      <label className="block">
        <span className="mb-1 block text-ink-soft">Avantages (une ligne par avantage)</span>
        <textarea
          name="features"
          rows={3}
          defaultValue={plan?.features?.join("\n") ?? ""}
          className="w-full rounded-md border border-line bg-paper-raised px-3 py-2"
        />
      </label>
      <label className="flex items-center gap-1.5">
        <input type="checkbox" name="isActive" defaultChecked={plan?.isActive ?? true} /> Active
      </label>
      <div className="flex items-center gap-2">
        <Button type="submit">{plan ? "Enregistrer" : "Créer"}</Button>
        {plan && !plan.isActive ? <Badge>Inactive</Badge> : null}
      </div>
    </form>
  );
}

function Field({
  name, label, type = "text", defaultValue, required = true,
}: {
  name: string; label: string; type?: string; defaultValue?: string | number; required?: boolean;
}) {
  return (
    <label className="block">
      <span className="mb-1 block text-ink-soft">{label}</span>
      <input
        name={name}
        type={type}
        required={required}
        step={type === "number" ? "any" : undefined}
        defaultValue={defaultValue}
        className="w-full rounded-md border border-line bg-paper-raised px-3 py-1.5"
      />
    </label>
  );
}
