import { Container, LinkButton } from "@/components/ui";

export default function NotFound() {
  return (
    <Container className="py-24 text-center">
      <p className="font-serif text-5xl font-semibold text-brand">404</p>
      <h1 className="mt-4 text-xl font-semibold">Page introuvable</h1>
      <p className="mx-auto mt-2 max-w-prose text-sm text-ink-muted">
        Le document ou la page que vous cherchez n&apos;existe pas ou n&apos;est pas encore publié.
      </p>
      <div className="mt-6 flex justify-center gap-3">
        <LinkButton href="/" variant="primary">Accueil</LinkButton>
        <LinkButton href="/search" variant="secondary">Rechercher</LinkButton>
      </div>
    </Container>
  );
}
