# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

Greenfield. The repository currently contains only `PRD.md` — a detailed "MASTER PROMPT" that fully specifies the platform to be built. There is no code, no solution, no `.git`, and no `/docs` yet.

**`PRD.md` is the single source of truth.** Read it before making architectural decisions. It is numbered in sections (1–84); cite sections when justifying choices. The workflow it mandates (see below) is binding, not advisory.

## What is being built

**USTHB Study** — a centralized academic resource platform for university students (initially USTHB, Algiers). Students browse and search courses, TDs, TPs, exercises, exams, tests, retake exams and their solutions, organized strictly by:

```
University → Faculty → Department → Specialty → Level → AcademicYear → Semester → Module → ResourceType → Document
```

Revenue model is paid student accounts, so a Premium subscription system with server-enforced access control is core, not optional.

## Technology stack (mandated by PRD §4–§7)

| Layer | Choice |
|---|---|
| Backend | ASP.NET Core Web API, .NET 8+ LTS, Clean Architecture |
| ORM / DB | Entity Framework Core + MySQL (schema via migrations only) |
| Auth | ASP.NET Core Identity + JWT (refresh tokens where appropriate) |
| Backend libs | FluentValidation, Serilog, Swagger/OpenAPI |
| Frontend | Next.js + TypeScript + Tailwind CSS (SSR for public/SEO pages) |
| File storage | S3-compatible object storage behind `IFileStorageService`; local impl for dev |
| Search | MySQL-based behind `ISearchService`; pluggable for Meilisearch later |
| Infra | Docker + docker-compose (frontend, backend, MySQL, storage), `.env.example` |

Deviate from this stack only with a strong technical reason, stated explicitly.

## Planned repository layout (PRD §4, §8)

```
src/
  USTHBStudy.API              # controllers, middleware, DI wiring — NO business logic
  USTHBStudy.Application      # DTOs, services, interfaces, validators, use cases
  USTHBStudy.Domain           # entities, enums, domain rules — depends on NOTHING
  USTHBStudy.Infrastructure   # EF Core, Identity, storage, payment, search impls
tests/
  USTHBStudy.UnitTests
  USTHBStudy.IntegrationTests
frontend/                     # Next.js app
docs/                         # architecture.md, database.md, api.md, security.md, deployment.md, roadmap.md
docker-compose.yml
```

Dependency direction is strictly `Domain ← Application ← Infrastructure ← API`. The Domain layer must not reference Infrastructure. The API layer must not contain business logic.

## Expected commands (once Phase 1 scaffolds the solution)

These do not work yet — no solution exists. After scaffolding they will be:

```bash
# Backend (run from repo root or src/)
dotnet build
dotnet test                                             # all tests
dotnet test tests/USTHBStudy.UnitTests                  # one project
dotnet test --filter "FullyQualifiedName~AccessControl" # one class/test
dotnet run --project src/USTHBStudy.API
dotnet ef migrations add <Name> --project src/USTHBStudy.Infrastructure --startup-project src/USTHBStudy.API
dotnet ef database update --project src/USTHBStudy.Infrastructure --startup-project src/USTHBStudy.API

# Frontend (from frontend/)
npm install
npm run dev
npm run build
npm run lint

# Full stack
docker-compose up
```

Keep this section updated with the real commands as the solution takes shape.

## Non-negotiable design rules (from the PRD)

**Centralize cross-cutting concerns — do not scatter them:**
- Premium / role / access checks go through `IAccessControlService`. Never write `if (user.IsPremium)` throughout the codebase (§23).
- File storage through `IFileStorageService` (§7). Payment providers through `IPaymentProvider` (§25). Search through `ISearchService` (§14).
- Centralized exception handling; standardized error body `{ "success": false, "message": "...", "errors": [] }`; never leak stack traces (§45).

**Authorization is server-side, always:**
- Premium access, download rights, and admin access are verified in the backend regardless of what the frontend sends (§24, §29, §61).
- Downloads flow: API → check auth → check Premium → generate a short-lived signed URL → storage. Never expose permanent public storage URLs (§29, §31).
- Never activate Premium because the frontend says a payment succeeded. Payment approval is a manual admin action for the MVP (§25).
- Premium expires server-side when `PremiumExpiresAt < now` (§24).

**Do not hard-code:**
- `"USTHB"` — the platform must conceptually support multiple universities; USTHB lives only in seed data (§10, §67).
- Specialties, modules, or any academic data — all managed from the admin dashboard (§10).
- Subscription prices or plans — admin-configurable (§22, §68).
- Document types as free-form strings — use an enum or normalized table consistently (§12).

**Data & schema:**
- Every schema change requires an EF Core migration; never hand-edit the DB (§6).
- PDFs/images/thumbnails go to object storage; MySQL holds only metadata (§7).
- Public entities use stable, unique, accent-safe slugs (§54).
- All large collections are paginated with a capped `pageSize` (§55). Avoid N+1 and unbounded queries (§70).
- Add indexes deliberately (composite indexes for real filter/search patterns), not blindly (§53).
- Store a SHA-256 hash of every uploaded file; use it to *warn* about duplicates, never auto-delete (§75, §76).

**Frontend:**
- Mobile-first, responsive, accessible, SEO-friendly; SSR for public academic pages with full meta/OG/Twitter tags (§16, §48, §70).
- Architect for i18n (French / Arabic / English) and RTL from the start — do not hard-code user-facing strings; do not blindly mirror the layout for RTL (§72, §73).
- No fake features: no dead buttons, no simulated payments, no faked stats. Mark unimplemented things "coming soon" or hide them (§79).
- Prepare for PWA (manifest, icons, installable). Do not offline-cache Premium content (§50).

**Content rights:** store `Source`, `Uploader`, `RightsStatus`, `PermissionNotes` on documents; provide a reporting mechanism; do not build anything whose purpose is circumventing copyright (§40).

## Core domain model (PRD §9–§13, §22, §27, §42)

- **Academic hierarchy:** `University, Faculty, Department, Domain, Specialty, Level, Semester, AcademicYear, Module, Session`
- **User:** ASP.NET Identity + `FirstName, LastName, StudentId?, UniversityId, FacultyId, DepartmentId, SpecialtyId, LevelId, IsPremium, PremiumExpiresAt, IsActive`, timestamps
- **Document:** metadata + `DocumentType` enum, `Status` (`Draft, PendingReview, Published, Rejected, Archived`), `FileStorageKey`, `PreviewStorageKey`, `IsPremium`, `Source`, `ViewCount`, `DownloadCount`
- **Exam ↔ Solution:** a document (exam/test/exercise) may link to its correction; navigation between the two must be easy
- **Premium:** `SubscriptionPlan`, `Subscription`, `Payment` (store `TransactionReference`, `Status`, `AdminNote` — never raw card data)
- **Engagement:** `Favorite` (no duplicates), view/download history, `Notification`, `DocumentReport`, `AuditLog`
- **Roles:** `Admin, Moderator, Student` with explicit permission constants (`Document.Publish`, `User.Suspend`, `Subscription.Manage`, `AcademicData.Manage`, …)

## Development workflow (PRD §2, §62, §65, §82) — binding

Build incrementally, one module at a time: analyze requirements → inspect existing code → design → implement → **run build + unit tests + integration tests + migrations + app startup for real** → fix every error → verify UI/API/DB → only then move on. Do not proceed with a broken feature. Do not say "this should work" — verify it.

Phase order (do not skip):
0. Analysis + `docs/architecture.md` + `docs/roadmap.md` (no business features yet)
1. Foundation: solution structure, DB, EF Core, Identity, JWT, roles, Serilog, Swagger, health checks (`/health`), Docker, env config
2. Academic structure + admin CRUD (dependent dropdowns)
3. Documents: upload, storage, metadata, status/publishing, PDF preview
4. Public website: homepage, browse pages, search, SEO
5. Student experience: registration, academic profile, dashboard, personalization, favorites, history, downloads
6. Premium: plans, subscriptions, access control, expiration, payment records, manual activation
7. Admin: dashboard, users, contributions, reports, subscriptions, payments, analytics, audit logs
8. Quality: security / performance / responsive / SEO / accessibility / authorization audits

At the end of each phase produce the short report in PRD §83.

## Security tests that must exist (PRD §61)

Free user → Premium document = 403. Unauthenticated download = fail. Student → admin endpoint = fail. Suspended user → restricted actions blocked. Expired Premium → no Premium access. Backend stays secure against a tampered frontend.

## Secrets & config (PRD §44, §58)

Never commit credentials. Use `appsettings.Development.json`, environment variables, and secret management. Provide `.env.example`; never commit a real `.env`. Never expose DB credentials, JWT secrets, storage keys, payment secrets, or SMTP passwords in source.

## Commits (PRD §63)

Conventional, scoped, one concern per commit: `feat(auth): implement student authentication`, `fix(documents): secure premium download`. No giant multi-feature commits.

## Docs to keep current (PRD §64)

`docs/architecture.md`, `docs/database.md`, `docs/api.md`, `docs/security.md`, `docs/deployment.md`, `docs/roadmap.md` — update whenever architecture changes.

## Demo accounts (dev only, PRD §52)

`admin@example.local`, `student@example.local`, `moderator@example.local` — clearly fake credentials, never reused in production. Seed data may contain USTHB; no real student PII.

---
