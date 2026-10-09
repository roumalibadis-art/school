import type { Metadata } from "next";
import "./globals.css";
import { ContributionPrompt } from "@/components/classification/contribution-prompt";
import { SiteFooter } from "@/components/layout/site-footer";
import { SiteHeader } from "@/components/layout/site-header";

const siteUrl = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";

export const metadata: Metadata = {
  metadataBase: new URL(siteUrl),
  title: {
    default: "USTHB Study — Cours, TD, TP, examens et corrigés",
    template: "%s · USTHB Study",
  },
  description:
    "Cours, TD, TP, examens et solutions organisés par spécialité, niveau et module pour les étudiants de l'USTHB.",
  openGraph: {
    type: "website",
    siteName: "USTHB Study",
    locale: "fr_DZ",
  },
  twitter: { card: "summary_large_image" },
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="fr">
      <body className="flex min-h-screen flex-col">
        <SiteHeader />
        <main className="flex-1">{children}</main>
        <SiteFooter />
        <ContributionPrompt />
      </body>
    </html>
  );
}
