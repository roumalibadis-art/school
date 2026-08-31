# Roadmap — USTHB Study

> Living document. `[x]` done · `[~]` in progress · `[ ]` not started.
> Phase order is fixed by PRD §65 — do not skip. A PRD §83 report is appended at each phase boundary.

**Current status:** Phase 3 — Documents (starting)

---

## Phase 0 — Analysis  `[x]`

- [x] Inspect repository (only `PRD.md` present) and environment
- [x] `git init`, `.gitignore`, `.gitattributes`
- [x] `docs/architecture.md`, `docs/roadmap.md`, `docs/database.md`, `docs/api.md`, `docs/security.md`, `docs/deployment.md`
- [x] Implementation plan agreed (autonomous, `net8.0`, local MySQL, no Docker)
- [x] Commit Phase 0 deliverables

## Phase 1 — Foundation  `[x]`

- [x] Solution + 4 src projects + 2 test projects, `Directory.Build.props`, CPM, `.editorconfig`, local `dotnet-ef`
- [x] `AppDbContext` + ASP.NET Core Identity (`ApplicationUser` with §9 fields, `ApplicationRole`)
- [x] JWT access (HS256) + rotating, hashed, single-use refresh tokens
- [x] Roles + permission claims (`Admin`, `Moderator`, `Student`) + per-permission authorization policies
- [x] `IAccessControlService` + real premium-expiry rule (§24), unit-tested
- [x] Serilog, Swagger (+ JWT), CORS, fixed-window rate limiting, secure headers, global exception handling
- [x] Health checks `/health`, `/health/ready`, `/health/live`
- [x] `IFileStorageService` + `LocalFileStorageService` (S3 impl deferred to Phase 3 — see report)
- [x] `DbSeeder` (roles/permissions always; demo users in Development)
- [x] `InitialCreate` migration + applied to local MySQL `usthbstudy`
- [x] 40 tests green (26 unit + 14 integration); API smoke (health/register/login/refresh/me-401/probe-403/tamper-401/validation-400) verified
- [x] Phase 1 §83 report + commit
- [~] Docker — **excluded** by explicit user direction ("don't use docker, keep it simple"); deviates from PRD §58

## Phase 2 — Academic structure  `[x]`

- [x] Entities: `University, Faculty, Department, AcademicDomain, Specialty, Level, Semester, AcademicYear, Module, Session` (§10, §11)
- [x] Slug generation, accent-safe (ICU), unique with `-N` suffix (§54)
- [x] Admin CRUD APIs (`AcademicData.Manage`) + public reads + `?parentId=` filter for dependent dropdowns (§34)
- [~] Caching of static academic data — **deferred to Phase 4** (belongs with the public read paths; admin views want fresh data) (§56)
- [x] Indexes (§53), `AcademicStructure` migration applied to MySQL, sample USTHB tree seeded (Development), tests

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

### Phase 1 — Foundation — completed 2026-08-31

**Implemented**
- Clean-architecture solution: `Domain` ← `Application` ← `Infrastructure` ← `API`, central package
  management, `net8.0`, warnings-as-errors, `.editorconfig`, pinned local `dotnet-ef` 8.0.16.
- Persistence: `AppDbContext` (Identity + `RefreshTokens`), Pomelo MySQL 8, auditing save-interceptor,
  entity configs, `InitialCreate` migration applied to local `usthbstudy`.
- Identity & auth: `ApplicationUser` with all PRD §9 fields; HS256 JWT access tokens; opaque refresh
  tokens stored as SHA-256 hashes, single-use, rotating, with reuse → 401; lockout after 5 failures.
- AuthZ: roles `Admin`/`Moderator`/`Student` seeded with `permission` claims; one policy per
  permission; `IAccessControlService` centralizes the premium-active rule (PRD §24).
- Cross-cutting: Serilog (console + rolling file), Swagger with bearer scheme, config-driven CORS,
  fixed-window rate limiting (global + stricter `auth`), security headers, global exception middleware
  producing the PRD §45 error body, `/health` + `/health/ready` (DB) + `/health/live`.
- Storage: `IFileStorageService` + `LocalFileStorageService` (path-contained), provider-selected by config.
- Seeding: idempotent roles/permissions always; demo users (`admin|moderator|student@example.local`)
  only under Development.
- API surface: `POST /api/auth/{register,login,refresh,logout}`, `GET /api/me`, `GET /api/diagnostics/*`.

**Tests** — Passed: 40 (26 unit + 14 integration). Failed: 0.
Integration tests run the real pipeline over SQLite in-memory. Security cases covered now:
unauthenticated → 401, student → permission-gated → 403, tampered token → 401, refresh reuse → 401.

**Database** — Migration created: yes (`20260831111724_InitialCreate`). Applied to MySQL: yes.

**Security** — Verified: JWT validation params (issuer/audience/lifetime/signing key), secrets only in
user-secrets, error responses carry no stack traces, rate limiter active (observed 429 under burst).

**Deviations from PRD**
- **Docker (§58) excluded** at the user's explicit request ("keep it simple"). Local dev is
  `dotnet run` against the existing MySQL service.
- **S3 storage impl deferred to Phase 3** (no value shipping untested cloud code in Phase 1; the
  `IFileStorageService` port and provider switch exist).
- **`ISearchService` / `IPaymentProvider` deferred** to Phases 4 / 6 respectively (their features).
- **Immediate suspended-user rejection (§61-#4)** enforced on token refresh in Phase 1; per-request
  DB enforcement lands with user management in Phase 7.

**Known issues** — none blocking.

**Next phase** — Phase 2: academic hierarchy entities + admin CRUD.

### Phase 2 — Academic structure — completed 2026-08-31

**Implemented**
- 10 entities under `Domain/Academic` on a shared `AcademicEntity` base (name, unique slug, active flag,
  audit timestamps, soft-delete). `AcademicDomain` named to avoid the `USTHBStudy.Domain` namespace clash.
- `Slugifier` (ICU normalization, accent-folding) — turned `InvariantGlobalization` **off** project-wide
  since the platform handles French/Arabic (PRD §72/§73).
- Generic `AcademicNodeService<TEntity,TDto,TInput>` (list + `?parentId` filter + search + paging, get,
  get-by-slug, create, replace, soft-delete, unique-slug helper, parent-existence checks) with 10 thin
  concrete services; `AcademicYear` auto-manages the single `IsCurrent` flag.
- Generic `AcademicNodeController<TDto,TInput>` + 10 route shims: `GET` public, `POST/PUT/DELETE` behind
  `AcademicData.Manage`. Routes `/api/{universities,faculties,departments,domains,specialties,levels,`
  `semesters,academic-years,sessions,modules}`.
- FluentValidation for every input; `AcademicStructure` migration applied to MySQL; `DbSeeder` plants a
  small USTHB tree (1 specialty, L1–L3, S1–S6, 18 modules, 2 academic years, 2 sessions) in Development.

**Tests** — Passed: 57 (37 unit + 20 integration). Failed: 0.
New: `SlugifierTests`; `AcademicCrudTests` (public read / student write → 403 / full hierarchy build /
missing-parent → 404 / duplicate-slug `-2` / rename-reslug / soft-delete / validation → 400).

**Database** — Migration `20260831…_AcademicStructure` created and applied. 10 tables + indexes
(parent FKs, `(SpecialtyId,Order)` on levels, `(LevelId,Order)` on semesters, `(SpecialtyId,SemesterId)`
on modules, unique `Slug` per table, unique `StartYear` on academic years).

**Security** — writes require `AcademicData.Manage` (Admin only by default); verified via integration test.

**Deviations from PRD** — caching (§56) deferred to Phase 4 (see checklist note).

**Known issues** — none.

**Next phase** — Phase 3: `Document` entity, upload + validation + hashing, storage, PDF preview, publishing.
