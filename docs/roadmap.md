# Roadmap — USTHB Study

> Living document. `[x]` done · `[~]` in progress · `[ ]` not started.
> Phase order is fixed by PRD §65 — do not skip. A PRD §83 report is appended at each phase boundary.

**Current status:** Phase 1 — Foundation (in progress)

---

## Phase 0 — Analysis  `[~]`

- [x] Inspect repository (only `PRD.md` present) and environment
- [x] `git init`, `.gitignore`
- [x] `docs/architecture.md`, `docs/roadmap.md`, `docs/database.md`, `docs/api.md`, `docs/security.md`, `docs/deployment.md`
- [x] Implementation plan agreed (autonomous, `net8.0`, local MySQL)
- [ ] Commit Phase 0 deliverables

## Phase 1 — Foundation  `[~]`

- [ ] Solution + 4 src projects + 2 test projects, `Directory.Build.props`, CPM, `.editorconfig`, local `dotnet-ef`
- [ ] `AppDbContext` + ASP.NET Core Identity (`ApplicationUser` with §9 fields, `ApplicationRole`)
- [ ] JWT access + rotating refresh tokens
- [ ] Roles + permission claims (`Admin`, `Moderator`, `Student`) + per-permission authorization policies
- [ ] `IAccessControlService` skeleton (premium-active + role/ownership checks)
- [ ] Serilog, Swagger (+ JWT), CORS, rate limiting, secure headers, global exception handling
- [ ] Health checks `/health`, `/health/ready`, `/health/live`
- [ ] `IFileStorageService` (local + S3 impls), `ISearchService` stub, `IPaymentProvider` stub
- [ ] `DbSeeder` (roles always; demo users in Development)
- [ ] Docker: API `Dockerfile`, `docker-compose.yml`, `.env.example`, `.dockerignore`
- [ ] `InitialCreate` migration + applied to local MySQL
- [ ] Unit + integration tests green; API smoke (register/login/refresh/me/401/403) verified
- [ ] Phase 1 §83 report + commit

## Phase 2 — Academic structure  `[ ]`

- [ ] Entities: `University, Faculty, Department, Domain, Specialty, Level, Semester, AcademicYear, Module, Session` (§10, §11)
- [ ] Slug generation, accent-safe, unique (§54)
- [ ] Admin CRUD APIs + filtered list endpoints for dependent dropdowns (§34)
- [ ] Caching of static academic data + invalidation (§56)
- [ ] Indexes (§53), migration, tests

## Phase 3 — Documents  `[ ]`

- [ ] `Document` entity, `DocumentType` / `DocumentStatus` enums (§12)
- [ ] Upload: server-side extension/MIME-sniff/size/name validation (§36)
- [ ] SHA-256 hash + duplicate warning (§75, §76)
- [ ] Local storage provider active; publishing workflow (Draft→PendingReview→Published/…) 
- [ ] PDF first-page preview + thumbnail (§31)
- [ ] Exam ↔ Solution linking (§13)

## Phase 4 — Public website  `[ ]`

- [ ] `frontend/` Next.js scaffold, design system components (§48)
- [ ] Homepage (§18), browse pages (faculties/specialties/levels/modules/documents/exams)
- [ ] `MySqlSearchService` real impl + filters (§14, §15)
- [ ] SSR + SEO metadata / OG / canonical (§16)

## Phase 5 — Student experience  `[ ]`

- [ ] Registration with academic profile (§19), `/dashboard` (§20), personalization (§21)
- [ ] Favorites (§27), history (§28)
- [ ] Authorized download → signed URL (§29), PDF viewer (§30)

## Phase 6 — Premium  `[ ]`

- [ ] `SubscriptionPlan`, `Subscription`, `Payment` (§22, §26); admin-configurable plans/prices
- [ ] `IPaymentProvider` + `ManualPaymentProvider`; admin approve/reject → activate/extend (§25)
- [ ] Server-side expiration enforcement (§24); premium UI states; preview-only fallback (§30)

## Phase 7 — Admin  `[ ]`

- [ ] Dashboard stats + charts (§32, §69), user management (§33)
- [ ] Contributions + moderation (§37, §38), reports (§39)
- [ ] Subscription/payment admin (§25), audit log (§42), analytics

## Phase 8 — Quality  `[ ]`

- [ ] Security audit vs §44 / §61; performance (N+1, pagination caps, indexes, §70)
- [ ] Responsive + RTL (§49, §73), SEO, accessibility (§71), upload-abuse tests

---

## Phase reports (§83)

_Appended as phases complete._
