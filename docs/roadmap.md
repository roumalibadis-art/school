# Roadmap — USTHB Study

> Living document. `[x]` done · `[~]` in progress · `[ ]` not started.
> Phase order is fixed by PRD §65 — do not skip. A PRD §83 report is appended at each phase boundary.

**Current status:** Phase 7 — Admin (starting)

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

## Phase 3 — Documents  `[x]`

- [x] `Document` entity, `DocumentType` / `DocumentStatus` / `RightsStatus` enums (§12, §40)
- [x] Upload: server-side extension allow-list + magic-byte sniff + size cap (§36)
- [x] SHA-256 hash + duplicate warning in the upload response (§75, §76)
- [x] `LocalFileStorageService` active; publishing workflow (Draft → Published / Rejected / Archived)
- [x] PDF first-page preview + thumbnail via pdfium (PDFtoImage), page count (§31)
- [x] Exam ↔ Solution linking (§13); public preview endpoint; view-count tracking (§77)

## Phase 4 — Public website  `[x]`

- [x] `MySqlSearchService` real impl (`LIKE` + year detection) + all §15 filters; `GET /api/search`
- [x] Academic reference-data caching + invalidation on write (§56)
- [x] `frontend/` Next.js 15 (App Router, TS, Tailwind, hand-built primitives — no component lib) (§48)
- [x] Homepage (§18: hero + search + quick nav + popular modules + recent + why), browse pages
      (faculties / faculty / specialty / modules / module / documents / document / exams / search)
- [x] SSR (server components) + per-page `generateMetadata` (title/description/canonical/OG) + `robots.ts` + dynamic `sitemap.ts` (§16)
- [x] Stub pages for login/register/legal marked "bientôt" (§79); `/api/*` proxied to the API in dev

## Phase 5 — Student experience  `[x]`

- [x] Academic profile (§19): `PUT /api/me` + FKs on `AspNetUsers`; frontend register → complete-profile flow with dependent dropdowns
- [x] `/dashboard` (§20) — my modules, recent resources, popular exams, favorites count, subscription state; personalized by specialty + level (§21)
- [x] Favorites (§27): `Favorite` (unique per user+kind+entity), `GET/POST/DELETE /api/favorites`, toggle UI
- [x] History (§28): `UserActivity` (views upserted, downloads appended), `GET /api/me/history`
- [x] Authorized download → short-lived signed link (§29): `IAccessControlService.EnsureCanAccess`,
      HMAC token service, `GET /api/documents/{slug}/download` + `GET /api/files`, S3 presigned path
- [x] `S3FileStorageService` (AWSSDK.S3) — config-selectable, MinIO-compatible
- [x] PDF viewer (§30): native browser viewer via `/dl/{slug}?inline=1` in an iframe (pagination/zoom/search/fullscreen)
- [x] Cookie-based frontend auth (httpOnly), middleware guard + token refresh; `/api/admin/users/{id}/{premium,suspend,restore}`

## Phase 6 — Premium  `[x]`

- [x] `SubscriptionPlan` (admin CRUD, slug, soft delete, prices in DB), `Subscription`, `Payment` (§22, §26)
- [x] `IPaymentProvider` + `ManualPaymentProvider` (reference + instructions); `/api/admin/payments`
      approve/reject, `/api/admin/subscriptions/{id}/extend` (§25)
- [x] Approval activates/stacks `user.PremiumExpiresAt`; `IsPremiumActive` (date-based) enforced on
      downloads and reported by `/api/me` (§24)
- [x] Frontend: `/pricing` (real plans), `/subscribe` (checkout status + instructions), dashboard section

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

### Phase 3 — Documents — completed 2026-08-31

**Implemented**
- `Document` entity (PRD §12 fields: storage keys, `PageCount`, `MimeType`, `FileHashSha256`, `IsPremium`,
  `Status`, `Source`, `RightsStatus`/`PermissionNotes` per §40, `ViewCount`/`DownloadCount` per §77) +
  `DocumentType` / `DocumentStatus` / `RightsStatus` enums; self-referencing exam↔solution link (§13).
- `FileValidation` — extension allow-list (`pdf`, `png`, `jpg/jpeg`) **and** magic-byte sniff; the
  client content type is never trusted (§36). Size cap from `Documents:MaxFileSizeBytes` (50 MB).
- `IPdfProcessor` + `PdfiumPdfProcessor` (PDFtoImage / pdfium, native binaries bundled — no external
  install): first-page preview PNG (1240px) + thumbnail (320px) + page count (§31). Preview failure is
  non-fatal — the document still stores.
- `DocumentService`: upload (validate → SHA-256 → duplicate lookup → store file → render → create Draft),
  list with filters (module/specialty/year/session/type/premium/search — §15) and visibility (public →
  Published only; staff → any status), get by slug (bumps `ViewCount`), metadata update, status change
  (publish/reject/archive), solution linking, soft delete, preview streaming.
- `DocumentsController` — `POST` multipart (`Document.Create`), `GET` list/detail/preview (public),
  `PUT` (`Document.Update`), `POST /{id}/status` (`Document.Publish`), `POST /{id}/solutions/{id}`,
  `DELETE` (`Document.Delete`). `Documents` migration applied to MySQL.
- FluentValidation messages forced to English (server locale is now fr/ar-aware).

**Tests** — Passed: 69 (44 unit + 25 integration). Failed: 0.
New: `FileValidationTests` (6); `DocumentTests` (upload→render→draft→publish→public→preview; disguised
executable → 400; duplicate → warning; student upload → 403; exam/solution link). Integration tests
render a real generated PDF through pdfium.

**Live smoke** — uploaded a PDF against MySQL: Draft, pageCount 1, preview 18 KB PNG served to anon
after publish; file + preview + thumbnail written under `_storage/documents/exam/…`.

**Database** — `20260831…_Documents` migration created and applied. Indexes: unique `Slug`,
`FileHashSha256`, `(Status, IsPremium, CreatedAt)`, `(ModuleId, Type, Status)`, `(AcademicYearId, Type)`.

**Security** — upload requires `Document.Create`; non-published documents return 404 to non-staff
(existence not disclosed); content sniffing blocks disguised files.

**Deviations from PRD**
- S3 storage still deferred — `LocalFileStorageService` covers dev; the S3 adapter lands with the
  signed-download flow in Phase 5.
- Authorized download endpoint (§29) is Phase 5; Phase 3 exposes only the (safe) preview image.

**Known issues** — none.

**Next phase** — Phase 4: Next.js public site, homepage, browse pages, real `ISearchService`, SEO.

### Phase 4 — Public website — completed 2026-08-31

**Implemented (backend)**
- `ISearchService` + `MySqlSearchService`: `LIKE`-based search over `Published` documents with the full
  §15 facet set (faculty → department → specialty → level → semester → module, type, academic year,
  session, premium); a bare 4-digit token is matched as a year. `GET /api/search` (public).
- Academic reference-data caching (§56): `AcademicCacheSignal` (version stamp) + `IMemoryCache` on the
  search-free list path; every academic write bumps the version so reads never serve stale rows.

**Implemented (frontend — `frontend/`)**
- Next.js 15 App Router, TypeScript, Tailwind 3, hand-built UI primitives (`Container`, `Card`, `Badge`,
  `Button`/`LinkButton`, `Pagination`, `SectionHeading`, `EmptyState`) + cards (`DocumentCard`,
  `ModuleCard`, `FacultyCard`, `SearchResultRow`) + a client `SearchBox`. No component library (owner's call).
- Server-side API client (`src/lib/api.ts`) with envelope unwrap, 404→null, ISR revalidate; browser
  calls (preview images, search) proxied through `/api/*` rewrite to the .NET API.
- Pages: `/` (hero + search + quick-nav + popular modules + recent documents + "why"),
  `/faculties`, `/faculties/[slug]`, `/specialties/[slug]`, `/modules`, `/modules/[slug]`,
  `/documents` (+ type filter), `/documents/[slug]` (preview image, premium notice, metadata),
  `/exams`, `/search`, `/pricing`, `/about`, legal stubs, `not-found`.
- SEO: `metadataBase` + templated titles, per-page `generateMetadata` (description, canonical, OG with
  preview image), `robots.ts`, dynamic `sitemap.ts` enumerating faculties/specialties/modules/documents.
- French UI copy; `lang="fr"`; RTL-ready structure (logical layout, no hard-coded mirroring).

**Tests** — Passed: 71 (44 unit + 27 integration). Failed: 0.
`next build` succeeds: 18 routes, type-check clean, static pages prerendered, dynamic pages on-demand.
Runtime check: homepage / faculties / modules / module detail / search / robots / 404 all render with
real seeded data.

**Deviations from PRD**
- No frontend unit tests yet (Playwright/RTL) — deferred to Phase 8 QA.
- Student-only affordances (favorite button, download, dashboard link) render as "se connecter"
  prompts — the real flows are Phase 5.

**Known issues** — none.

**Next phase** — Phase 5: registration + academic profile, `/dashboard`, personalization, favorites,
history, authorized download → signed URL (§29), S3 storage adapter, PDF viewer.

### Phase 5 — Student experience — completed 2026-08-31

**Implemented (backend)**
- Academic-profile FKs on `AspNetUsers` (`University`…`Level`, nullable, RESTRICT) + `StudentFeatures`
  migration. `IStudentService`: `GET/PUT /api/me` (full `StudentProfileDto` with resolved refs),
  `GET /api/me/dashboard` (my modules = specialty+level's semesters; recent + popular-exam docs in the
  specialty; favorites count; subscription state — `none|active|expired`), `GET /api/me/history`.
- `Favorite` (unique `(UserId,Kind,EntityId)`) + `IFavoriteService` (idempotent add, target-exists
  check) → `GET/POST/DELETE /api/favorites`. `UserActivity` + `IActivityService` (views/module upserted,
  downloads appended; document views recorded on authed `GET /api/documents/{slug}`).
- Access control: `IAccessControlService.EnsureCanAccess(isPremiumContent, subject)` → 403 for
  disabled account or Premium content without an active subscription. `IDownloadTokenService`
  (HMAC-SHA256, 120 s, constant-time verify). `DocumentService.RequestDownloadAsync` (auth →
  account-active → Premium check → presigned S3 URL *or* `/api/files?t=<token>`; bumps `DownloadCount`,
  records activity) + `OpenDownloadAsync`. `GET /api/documents/{slug}/download`, `GET /api/files`.
- `S3FileStorageService` (AWSSDK.S3): AWS + MinIO (`ServiceUrl` + path-style), presigned GET URLs;
  `Storage:Provider = S3` switches it on.
- `IUserAdminService` + `/api/admin/users/{id}/{premium,suspend,restore}` — grant/revoke Premium
  (extends from the later of now / current expiry), suspend (also revokes refresh tokens).

**Implemented (frontend)**
- httpOnly cookie sessions via Server Actions (login/register/logout/update-profile); `middleware.ts`
  guards `/dashboard`,`/favorites`,`/profile` and refreshes near-expired access tokens.
- Pages: real login/register forms, `/profile` (dependent university→level dropdowns), `/dashboard`,
  `/favorites`, `/documents/[slug]/view` (browser PDF viewer in an iframe — §30). `FavoriteButton`,
  header `UserMenu` (public pages stay static). Route handlers `/api/session`, `/api/fav`,
  `/dl/[slug]` (session → download ticket → stream; `?inline=1` for the viewer).

**Tests** — Passed: 91 (52 unit + 39 integration). Failed: 0.
New: `HmacDownloadTokenServiceTests` (6), `StudentTests` (5), `DownloadTests` (7).
§61 now covered: **#1** free→premium 403, **#2** anon download 401, **#3** student→admin 403,
**#4** suspended 403, **#5** expired-premium 403, **#6** tampered token 401, **#7** refresh reuse 401.
`next build`: 24 routes, type-check clean. Full student flow verified against the running stack.

**Deviations from PRD**
- S3 is implemented but unexercised locally (no S3/MinIO endpoint) — `Local` remains the dev default.
- PDF viewer uses the browser's native viewer (satisfies §30's feature list) rather than a bundled pdf.js.
- Frontend has no automated tests yet (→ Phase 8).

**Known issues** — none.

**Next phase** — Phase 6: `SubscriptionPlan`/`Subscription`/`Payment`, admin-configurable plans,
`IPaymentProvider` + manual verification, subscription-driven Premium, premium UI.

### Phase 6 — Premium — completed 2026-08-31

**Implemented**
- Entities: `SubscriptionPlan` (name/slug, `DurationDays`, `Price`+`Currency`, `Features`, `DisplayOrder`,
  `IsActive`, soft delete — prices live in the DB, PRD §22), `Subscription`
  (`Pending`/`Active`/`Expired`/`Cancelled`, snapshots duration + price), `Payment` (PRD §26:
  `TransactionReference` `USTHB-XXXX`, status, `PaidAt`, `AdminNote`; no card data). `Premium` migration.
- `IPaymentProvider` + `ManualPaymentProvider` (PRD §25): records a pending payment, returns
  instructions + reference (bank/CCP details from `Payments:Manual` config). Premium is **never**
  activated from a client-reported success.
- `ISubscriptionPlanService` (CRUD, public list = active only) + `ISubscriptionService`:
  `CheckoutAsync` (Pending sub + Pending payment + instructions; one pending per user),
  `GetMineAsync` (current + history + effective-premium + pending instructions),
  `ListPaymentsAsync` (`?status`), `ApprovePaymentAsync` (→ `Success`, activates & **stacks** on any
  remaining Premium time — PRD §24), `RejectPaymentAsync` (→ `Failed`, cancels the sub),
  `ExtendSubscriptionAsync` (admin, PRD §25).
- Endpoints: `GET /api/subscriptions/plans` (public), `POST /api/subscriptions` + `GET /api/subscriptions/me`
  (student), `/api/admin/subscription-plans` CRUD, `/api/admin/payments[?status]` +
  `/{id}/approve|reject`, `/api/admin/subscriptions/{id}/extend` (all `Subscription.Manage`).
- `/api/me` now reports the *effective* Premium state (date rules, PRD §24).
- Frontend: `/pricing` (real plans, subscribe action), `/subscribe` (pending instructions / active
  status), dashboard subscription section. `DbSeeder`: 4 sample DZD plans (Development).

**Tests** — Passed: 96 (52 unit + 44 integration). New `SubscriptionTests` (5): full checkout →
pending → not-premium (download 403) → admin approve → Premium active (download 200); reject cancels;
duplicate checkout 409; unknown plan 404; student → plan management 403.
Live-verified the whole flow through the frontend.

**Deviations from PRD**
- Preview-only fallback for Premium docs (PRD §30): free users already get the first-page preview +
  metadata; the document detail page shows a "réservé aux abonnés" notice. No partial-PDF rendering.
- Admin payment approval is API-only — the admin **UI** for it is Phase 7.

**Known issues** — none.

**Next phase** — Phase 7: admin dashboard + analytics (§32/§69), user management (§33),
contributions + moderation (§37/§38), reports (§39), audit log (§42), admin UI.
