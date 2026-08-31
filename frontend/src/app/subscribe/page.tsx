import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { Card, Container, LinkButton } from "@/components/ui";
import { authedApiGet } from "@/lib/api";
import { formatDate } from "@/lib/format";
import { getCurrentUser } from "@/lib/session";

export const metadata: Metadata = { title: "Mon abonnement", robots: { index: false } };

interface MySubscriptions {
  current: {
    planName: string;
    status: string;
    endsAt: string | null;
    priceAtPurchase: number;
    currency: string;
    latestPayment: { reference: string; status: string } | null;
  } | null;
  isPremiumActive: boolean;
  premiumExpiresAt: string | null;
  pendingPaymentInstructions: string | null;
}

const banners: Record<string, string> = {
  created: "Votre demande a été enregistrée. Suivez les instructions de paiement ci-dessous.",
  "already-pending": "Vous avez déjà une demande en attente de confirmation.",
  error: "La demande n'a pas pu être créée. Réessayez.",
};

export default async function SubscribePage({ searchParams }: { searchParams: Promise<{ state?: string }> }) {
  if (!(await getCurrentUser())) redirect("/login?next=/pricing");
  const { state } = await searchParams;
  const data = await authedApiGet<MySubscriptions>("/api/subscriptions/me");

  return (
    <Container className="py-12">
      <div className="mx-auto max-w-lg space-y-4">
        <h1 className="text-2xl font-semibold">Mon abonnement</h1>

        {state && banners[state] ? (
          <p className="rounded-md border border-brand/20 bg-brand-soft p-3 text-sm text-brand-strong">
            {banners[state]}
          </p>
        ) : null}

        {data.isPremiumActive ? (
          <Card className="p-6">
            <p className="text-sm font-semibold text-premium">Premium actif</p>
            {data.premiumExpiresAt ? (
              <p className="mt-1 text-sm text-ink-muted">Jusqu&apos;au {formatDate(data.premiumExpiresAt)}.</p>
            ) : null}
          </Card>
        ) : null}

        {data.current?.status === "Pending" && data.pendingPaymentInstructions ? (
          <Card className="p-6">
            <p className="text-sm font-semibold">Paiement en attente — {data.current.planName}</p>
            <pre className="mt-3 whitespace-pre-wrap rounded-md bg-paper-sunken p-3 text-sm text-ink">
              {data.pendingPaymentInstructions}
            </pre>
            <p className="mt-3 text-xs text-ink-soft">
              Un administrateur activera votre abonnement après vérification du paiement.
            </p>
          </Card>
        ) : null}

        {!data.isPremiumActive && data.current?.status !== "Pending" ? (
          <Card className="p-6 text-sm text-ink-muted">
            Vous n&apos;avez pas d&apos;abonnement actif.
            <div className="mt-4">
              <LinkButton href="/pricing">Voir les offres</LinkButton>
            </div>
          </Card>
        ) : null}

        <p className="text-sm">
          <Link href="/dashboard" className="link">← Retour au tableau de bord</Link>
        </p>
      </div>
    </Container>
  );
}
