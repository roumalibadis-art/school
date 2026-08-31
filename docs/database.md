# Database — USTHB Study

> Living document. Schema is owned by EF Core migrations (§6) — this file is the map, not the source of truth.
> Engine: MySQL 8, `utf8mb4` / `utf8mb4_unicode_ci`. Provider: `Pomelo.EntityFrameworkCore.MySql`.

## Conventions

- **PK:** `Guid` (`char(36)` or binary(16) — default `char(36)` for readability), named `Id`.
- **Timestamps:** `CreatedAt` (UTC, set on insert), `UpdatedAt` (UTC, set on update) via `AuditableEntity` +
  `AuditableEntityInterceptor`.
- **Soft delete:** `ISoftDeletable` (`IsDeleted`, `DeletedAt`) only where content must be recoverable
  (documents, academic entities). Hard delete elsewhere. Global query filter excludes soft-deleted rows.
- **Slugs:** public entities carry a unique, accent-folded slug (§54); unique index per entity (scoped where needed).
- **Enums:** stored as `int` (or a lookup table where admin-editability is required). `DocumentType` /
  `DocumentStatus` are `int` enums (§12).
- **Money:** `decimal(10,2)` + explicit `Currency` (ISO 4217) — no floating point (§26).
- **FKs:** `RESTRICT` by default; `CASCADE` only for owned child collections.

## Phase 1 tables (Identity + auth)

ASP.NET Core Identity schema (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetRoleClaims`,
`AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`) plus:

### AspNetUsers (`ApplicationUser : IdentityUser<Guid>`) — §9

| Column | Type | Notes |
|---|---|---|
| FirstName, LastName | varchar(100) | required |
| StudentId | varchar(50) null | optional, not required unless a real need (§9) |
| UniversityId, FacultyId, DepartmentId, SpecialtyId, LevelId | Guid null | academic profile; FKs added in Phase 2 |
| IsPremium | bit | derived flag; source of truth is an active `Subscription` (Phase 6) |
| PremiumExpiresAt | datetime(6) null | server-enforced (§24) |
| IsActive | bit | suspended users fail authorization (§33, §61) |
| CreatedAt, UpdatedAt | datetime(6) | |

### RefreshTokens — §44

| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid | FK → AspNetUsers, CASCADE |
| TokenHash | varchar(128) | SHA-256 of the opaque token; index |
| ExpiresAt | datetime(6) | |
| CreatedAt | datetime(6) | |
| RevokedAt | datetime(6) null | set on rotation/logout |
| ReplacedByTokenHash | varchar(128) null | rotation chain |
| CreatedByIp / RevokedByIp | varchar(45) null | audit |

Index: `(TokenHash)` unique, `(UserId, RevokedAt)`.

### Role permission claims — §43

Roles `Admin`, `Moderator`, `Student` seeded with `permission` claims in `AspNetRoleClaims`
(e.g. `Document.Publish`, `User.Suspend`, `Subscription.Manage`, `AcademicData.Manage`).
Authorization policies map 1:1 to permission strings.

## Planned tables by phase (not yet created)

- **Phase 2 — Academic:** `Universities, Faculties, Departments, Domains, Specialties, Levels, Semesters,
  AcademicYears, Modules, Sessions`. Hierarchy per §10. `Module` fields per §11.
- **Phase 3 — Documents:** `Documents` (§12: `Title, Slug, DocumentType, ModuleId, AcademicYearId, SessionId,
  FileStorageKey, PreviewStorageKey, FileName, FileSize, PageCount, MimeType, FileHashSha256, IsPremium,
  Status, Source, RightsStatus, PermissionNotes, UploadedById, ViewCount, DownloadCount`), `DocumentTags`,
  `ExamSolutionLinks` (§13).
- **Phase 5 — Engagement:** `Favorites` (unique `(UserId, EntityType, EntityId)`, §27), `ViewHistory`,
  `DownloadHistory` (§28).
- **Phase 6 — Premium:** `SubscriptionPlans` (§22), `Subscriptions`, `Payments` (§26).
- **Phase 7 — Ops:** `Notifications` (§41), `Contributions` (§37), `DocumentReports` (§39), `AuditLogs` (§42).

## Indexing plan (§53)

Create deliberately, backed by real query patterns — not blindly.

| Table | Index | Serves |
|---|---|---|
| Documents | `(Status, IsPremium, CreatedAt)` | public listing / "recently added" |
| Documents | `(ModuleId, DocumentType, Status)` | module page resource lists |
| Documents | `(AcademicYearId, DocumentType)` | exam archive by year |
| Documents | `Slug` unique | SEO routes |
| Documents | `FileHashSha256` | duplicate detection (§75) |
| Modules | `Slug` unique, `(SemesterId)`, `(SpecialtyId)` | module routing & filters |
| Specialties/Levels/Semesters | `Slug` unique, parent FK | dependent dropdowns (§34) |
| Favorites | `(UserId, EntityType, EntityId)` unique | dedupe + lookup (§27) |
| Subscriptions | `(UserId, Status)`, `(EndsAt)` | expiry sweep (§24) |
| RefreshTokens | `TokenHash` unique | refresh lookup |

Composite indexes mirror the common filter combos from §15 (faculty/department/specialty/level/semester/module
/ type / year / session / free-premium).

## Migration workflow

```
dotnet tool restore                     # pinned dotnet-ef 8.0.x
dotnet ef migrations add <Name>  -p src/USTHBStudy.Infrastructure -s src/USTHBStudy.API
dotnet ef database update        -p src/USTHBStudy.Infrastructure -s src/USTHBStudy.API
dotnet ef migrations script --idempotent -o artifacts/migrate.sql   # for prod review
```

Production schema is never hand-edited; every change is a migration (§6). Dev DB: `usthbstudy` on local `MySQL80`.
