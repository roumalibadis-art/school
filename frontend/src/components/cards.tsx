import Link from "next/link";
import { Badge, Card } from "@/components/ui";
import { documentTypeLabel, formatDate } from "@/lib/format";
import type { DocumentDto, Faculty, Module, SearchHit } from "@/lib/types";

export function FacultyCard({ faculty }: { faculty: Faculty }) {
  return (
    <Link href={`/faculties/${faculty.slug}`} className="no-underline">
      <Card className="h-full p-5 transition-shadow hover:shadow-md">
        <h3 className="text-base font-semibold">{faculty.name}</h3>
        {faculty.code ? <p className="mt-1 text-xs text-ink-soft">{faculty.code}</p> : null}
      </Card>
    </Link>
  );
}

export function ModuleCard({ module }: { module: Module }) {
  return (
    <Link href={`/modules/${module.slug}`} className="no-underline">
      <Card className="h-full p-5 transition-shadow hover:shadow-md">
        <h3 className="text-base font-semibold">{module.name}</h3>
        <p className="mt-2 flex flex-wrap gap-2 text-xs text-ink-muted">
          {module.code ? <span>{module.code}</span> : null}
          <span>Coeff. {module.coefficient}</span>
          <span>{module.credits} crédits</span>
        </p>
      </Card>
    </Link>
  );
}

export function DocumentCard({ doc }: { doc: DocumentDto }) {
  return (
    <Link href={`/documents/${doc.slug}`} className="no-underline">
      <Card className="flex h-full gap-4 p-4 transition-shadow hover:shadow-md">
        <PreviewThumb slug={doc.slug} hasPreview={doc.hasPreview} />
        <div className="min-w-0 flex-1">
          <div className="mb-1 flex flex-wrap items-center gap-2">
            <Badge tone="brand">{documentTypeLabel(doc.type)}</Badge>
            {doc.isPremium ? <Badge tone="premium">Premium</Badge> : null}
          </div>
          <h3 className="truncate text-sm font-semibold">{doc.title}</h3>
          <p className="mt-1 text-xs text-ink-soft">
            {doc.pageCount ? `${doc.pageCount} p. · ` : ""}
            {doc.publishedAt ? formatDate(doc.publishedAt) : "Brouillon"}
          </p>
        </div>
      </Card>
    </Link>
  );
}

export function SearchResultRow({ hit }: { hit: SearchHit }) {
  return (
    <Link href={`/documents/${hit.slug}`} className="no-underline">
      <Card className="flex items-center gap-4 p-4 transition-shadow hover:shadow-md">
        <PreviewThumb slug={hit.slug} hasPreview={hit.hasPreview} />
        <div className="min-w-0 flex-1">
          <div className="mb-1 flex flex-wrap items-center gap-2">
            <Badge tone="brand">{documentTypeLabel(hit.type)}</Badge>
            {hit.isPremium ? <Badge tone="premium">Premium</Badge> : null}
            {hit.year ? <Badge>{hit.year}</Badge> : null}
          </div>
          <h3 className="truncate text-sm font-semibold">{hit.title}</h3>
          <p className="mt-1 truncate text-xs text-ink-soft">
            {hit.moduleName}
            {hit.specialtyName ? ` · ${hit.specialtyName}` : ""}
            {hit.session ? ` · ${hit.session}` : ""}
          </p>
        </div>
      </Card>
    </Link>
  );
}

function PreviewThumb({ slug, hasPreview }: { slug: string; hasPreview: boolean }) {
  if (!hasPreview) {
    return (
      <div
        aria-hidden
        className="grid h-16 w-12 shrink-0 place-items-center rounded border border-line bg-paper-sunken text-ink-soft"
      >
        PDF
      </div>
    );
  }
  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={`/api/documents/${encodeURIComponent(slug)}/preview`}
      alt=""
      loading="lazy"
      className="h-16 w-12 shrink-0 rounded border border-line object-cover object-top"
    />
  );
}
