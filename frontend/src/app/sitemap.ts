import type { MetadataRoute } from "next";
import { apiGetPaged } from "@/lib/api";
import type { DocumentDto, Faculty, Module, Specialty } from "@/lib/types";

const siteUrl = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";

export const revalidate = 3600;

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const staticRoutes: MetadataRoute.Sitemap = [
    "", "/faculties", "/modules", "/documents", "/exams", "/pricing", "/about",
  ].map((path) => ({ url: `${siteUrl}${path}`, changeFrequency: "weekly", priority: path === "" ? 1 : 0.7 }));

  try {
    const [faculties, specialties, modules, documents] = await Promise.all([
      apiGetPaged<Faculty>("/api/faculties", { query: { pageSize: 500 } }),
      apiGetPaged<Specialty>("/api/specialties", { query: { pageSize: 1000 } }),
      apiGetPaged<Module>("/api/modules", { query: { pageSize: 2000 } }),
      apiGetPaged<DocumentDto>("/api/documents", { query: { pageSize: 5000 } }),
    ]);

    const dynamic: MetadataRoute.Sitemap = [
      ...faculties.items.map((f) => ({ url: `${siteUrl}/faculties/${f.slug}`, priority: 0.6 })),
      ...specialties.items.map((s) => ({ url: `${siteUrl}/specialties/${s.slug}`, priority: 0.6 })),
      ...modules.items.map((m) => ({ url: `${siteUrl}/modules/${m.slug}`, priority: 0.6 })),
      ...documents.items.map((d) => ({
        url: `${siteUrl}/documents/${d.slug}`,
        lastModified: d.publishedAt ?? d.createdAt,
        priority: 0.5,
      })),
    ];

    return [...staticRoutes, ...dynamic];
  } catch {
    return staticRoutes;
  }
}
