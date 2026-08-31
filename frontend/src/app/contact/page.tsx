import type { Metadata } from "next";
import { Container } from "@/components/ui";

export const metadata: Metadata = {
  title: "Contact",
  alternates: { canonical: "/contact" },
};

export default function ContactPage() {
  return (
    <Container className="py-12">
      <div className="mx-auto max-w-prose">
        <h1 className="text-2xl font-semibold">Contact</h1>
        <p className="mt-4 text-sm text-ink-muted">
          Pour signaler un problème sur un document (mauvais module, mauvaise année, illisible,
          doublon, question de droits) ou toute autre demande, un formulaire dédié sera bientôt
          disponible.
        </p>
      </div>
    </Container>
  );
}
