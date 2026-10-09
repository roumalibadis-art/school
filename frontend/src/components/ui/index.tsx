import Link from "next/link";
import type { ComponentProps, ReactNode } from "react";

export function cx(...parts: Array<string | false | null | undefined>): string {
  return parts.filter(Boolean).join(" ");
}

/* ---------- Container ---------- */

export function Container({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cx("mx-auto w-full max-w-6xl px-4 sm:px-6", className)}>{children}</div>;
}

/* ---------- Card ---------- */

export function Card({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div className={cx("rounded-lg border border-line bg-paper-raised shadow-card", className)}>{children}</div>
  );
}

/* ---------- Badge ---------- */

export function Badge({
  children,
  tone = "neutral",
}: {
  children: ReactNode;
  tone?: "neutral" | "brand" | "premium";
}) {
  const tones = {
    neutral: "bg-paper-sunken text-ink-muted border-line",
    brand: "bg-brand-soft text-brand-strong border-brand/20",
    premium: "bg-premium-soft text-premium border-premium/25",
  } as const;
  return (
    <span
      className={cx(
        "inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-medium",
        tones[tone],
      )}
    >
      {children}
    </span>
  );
}

/* ---------- Button / link-button ---------- */

const buttonBase =
  "inline-flex items-center justify-center gap-2 rounded-md px-4 py-2 text-sm font-medium transition-colors disabled:opacity-50";

const buttonVariants = {
  primary: "bg-brand text-white hover:bg-brand-strong no-underline",
  secondary: "border border-line bg-paper-raised text-ink hover:bg-paper-sunken no-underline",
  ghost: "text-ink-muted hover:text-ink hover:bg-paper-sunken no-underline",
} as const;

type Variant = keyof typeof buttonVariants;

export function Button({
  variant = "primary",
  className,
  ...props
}: ComponentProps<"button"> & { variant?: Variant }) {
  return <button className={cx(buttonBase, buttonVariants[variant], className)} {...props} />;
}

export function LinkButton({
  href,
  variant = "primary",
  className,
  children,
  plain,
}: {
  href: string;
  variant?: Variant;
  className?: string;
  children: ReactNode;
  /**
   * Render a plain `<a>` (full navigation, no router prefetch / RSC fetch). Required for links whose GET has side
   * effects, e.g. `/dl/…` issues a download ticket and may spend a free download.
   */
  plain?: boolean;
}) {
  const classes = cx(buttonBase, buttonVariants[variant], className);
  if (plain) {
    return <a href={href} className={classes}>{children}</a>;
  }
  return (
    <Link href={href} className={classes}>
      {children}
    </Link>
  );
}

/* ---------- Section heading ---------- */

export function SectionHeading({
  title,
  action,
}: {
  title: string;
  action?: { href: string; label: string };
}) {
  return (
    <div className="mb-4 flex items-end justify-between gap-4">
      <h2 className="text-xl font-semibold">{title}</h2>
      {action ? (
        <Link href={action.href} className="link text-sm">
          {action.label} →
        </Link>
      ) : null}
    </div>
  );
}

/* ---------- Empty state ---------- */

export function EmptyState({ children }: { children: ReactNode }) {
  return (
    <Card className="p-8 text-center text-sm text-ink-muted">{children}</Card>
  );
}

/* ---------- Pagination ---------- */

export function Pagination({
  page,
  totalPages,
  hrefFor,
}: {
  page: number;
  totalPages: number;
  hrefFor: (page: number) => string;
}) {
  if (totalPages <= 1) return null;
  return (
    <nav className="mt-6 flex items-center justify-center gap-2 text-sm" aria-label="Pagination">
      {page > 1 ? (
        <Link href={hrefFor(page - 1)} className="rounded-md border border-line px-3 py-1.5 no-underline hover:bg-paper-sunken">
          ← Précédent
        </Link>
      ) : null}
      <span className="px-2 text-ink-muted">
        Page {page} sur {totalPages}
      </span>
      {page < totalPages ? (
        <Link href={hrefFor(page + 1)} className="rounded-md border border-line px-3 py-1.5 no-underline hover:bg-paper-sunken">
          Suivant →
        </Link>
      ) : null}
    </nav>
  );
}
