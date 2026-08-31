import type { Metadata } from "next";
import { Button, Card, Container, LinkButton } from "@/components/ui";
import { apiGet } from "@/lib/api";
import { subscribeAction } from "@/lib/subscription-actions";

export const metadata: Metadata = {
  title: "Premium",
  description: "Passez Premium pour accéder à l'ensemble des ressources d'USTHB Study.",
  alternates: { canonical: "/pricing" },
};

interface Plan {
  id: string;
  name: string;
  description?: string | null;
  durationDays: number;
  price: number;
  currency: string;
  features: string[];
}

export const revalidate = 300;

export default async function PricingPage() {
  const plans = await apiGet<Plan[]>("/api/subscriptions/plans", { revalidate: 300 }).catch(() => []);

  return (
    <Container className="py-12">
      <div className="mx-auto max-w-2xl text-center">
        <h1 className="text-3xl font-semibold">Premium</h1>
        <p className="mt-3 text-ink-muted">
          La plupart des documents sont gratuits. L&apos;abonnement Premium débloque les ressources
          marquées Premium et soutient la plateforme.
        </p>
      </div>

      {plans.length === 0 ? (
        <Card className="mx-auto mt-10 max-w-md p-6 text-center text-sm text-ink-muted">
          Les offres seront bientôt disponibles.
        </Card>
      ) : (
        <div className="mx-auto mt-10 grid max-w-4xl gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {plans.map((plan) => (
            <Card key={plan.id} className="flex flex-col p-6">
              <h2 className="text-lg font-semibold">{plan.name}</h2>
              <p className="mt-1 text-2xl font-semibold text-ink">
                {plan.price.toLocaleString("fr-FR")} <span className="text-sm font-normal text-ink-soft">{plan.currency}</span>
              </p>
              <p className="text-xs text-ink-soft">{plan.durationDays} jours</p>
              {plan.description ? <p className="mt-2 text-sm text-ink-muted">{plan.description}</p> : null}
              <ul className="mt-4 flex-1 space-y-1 text-sm text-ink-muted">
                {plan.features.map((f) => (
                  <li key={f}>· {f}</li>
                ))}
              </ul>
              <form action={subscribeAction} className="mt-6">
                <input type="hidden" name="planId" value={plan.id} />
                <Button type="submit" className="w-full">Choisir</Button>
              </form>
            </Card>
          ))}
        </div>
      )}

      <p className="mx-auto mt-8 max-w-prose text-center text-xs text-ink-soft">
        Après avoir choisi une offre, vous recevrez les instructions de paiement et une référence.
        Votre abonnement est activé par un administrateur une fois le paiement vérifié.
      </p>

      <div className="mt-6 text-center">
        <LinkButton href="/register" variant="secondary">Créer un compte gratuit</LinkButton>
      </div>
    </Container>
  );
}
