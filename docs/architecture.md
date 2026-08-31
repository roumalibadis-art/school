# Architecture — USTHB Study

> Living document. Update whenever the architecture changes (PRD §64).
> Section references (`§n`) point to `PRD.md`.

## 1. Overview

**USTHB Study** is a centralized academic resource platform. Students browse, search, preview
and download academic documents (courses, TDs, TPs, exercises, exams, tests, retakes and their
solutions), organized by a strict academic hierarchy. A Premium subscription gates part of the
catalogue; the business model is paid student accounts (§1).

- **Backend:** ASP.NET Core Web API, .NET 8 (LTS), Clean Architecture.
- **Frontend:** Next.js + TypeScript + Tailwind CSS (added Phase 4).
- **Database:** MySQL 8 via EF Core (Pomelo provider). Schema changes only through migrations (§6).
- **Object storage:** S3-compatible for binaries; local filesystem provider for dev (§7).
- **Search:** MySQL-based behind `ISearchService`, pluggable for Meilisearch later (§14).

## 2. Solution layers & dependency rule

```
┌─────────────┐
│    API      │  controllers, middleware, filters, DI composition root, auth wiring
└──────┬──────┘  → depends on Application + Infrastructure
       │
┌──────▼──────────┐        ┌──────────────────┐
│ Infrastructure  │───────▶│   Application    │  use-case services, DTOs, validators,
│ EF Core,        │        └────────┬─────────┘  abstraction interfaces (ports)
│ Identity, JWT,  │                 │            → depends on Domain only
│ storage, search,│        ┌────────▼─────────┐
│ payments        │───────▶│     Domain       │  entities, enums, domain rules,
└─────────────────┘        └──────────────────┘  invariants → depends on NOTHING
```

**Rules (enforced by project references, §8):**

- `Domain` references no other project and no infrastructure packages.
- `Application` references `Domain` only. It declares **ports** (interfaces) that Infrastructure implements.
- `Infrastructure` references `Application` (+ `Domain`) and implements the ports.
- `API` references `Application` + `Infrastructure`, wires DI, and contains **no business logic** (§8).
- The API composition root is the only place all three lower layers meet.

## 3. Project map

| Project | Responsibility |
|---|---|
| `USTHBStudy.Domain` | Entities, value objects, enums (`DocumentType`, `DocumentStatus`, …), domain invariants. |
| `USTHBStudy.Application` | Use-case services (`AuthService`, later `DocumentService`, …), DTOs, FluentValidation validators, ports: `IAccessControlService`, `IFileStorageService`, `IPaymentProvider`, `ISearchService`, `IJwtTokenService`, `ICurrentUser`, `IIdentityService`, `IDateTimeProvider`, `IEmailSender`. Response primitives (`ApiResponse`, `PagedResult`). |
| `USTHBStudy.Infrastructure` | `AppDbContext` (`IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`), EF configurations, migrations, `DbSeeder`, Identity types, `JwtTokenService`, `LocalFileStorageService` / `S3FileStorageService`, `AccessControlService`, `MySqlSearchService`, `ManualPaymentProvider`. `AddInfrastructure(config)`. |
| `USTHBStudy.API` | `Program.cs` pipeline, controllers, `ExceptionHandlingMiddleware`, Swagger/JWT/CORS/rate-limit/health-check wiring, authorization policies. |
| `tests/USTHBStudy.UnitTests` | Pure logic: token generation, access control, validators. |
| `tests/USTHBStudy.IntegrationTests` | End-to-end HTTP via `WebApplicationFactory` against SQLite in-memory. |

## 4. Cross-cutting concerns — centralized, never scattered

| Concern | Single home | Rule |
|---|---|---|
| Premium / role / ownership checks | `IAccessControlService` (§23) | Never write `if (user.IsPremium)` in controllers or use-case code. |
| Binary storage | `IFileStorageService` (§7) | Providers swap via config; callers never see storage keys become public URLs. |
| Payments | `IPaymentProvider` (§25) | Premium is activated by a persisted, admin-approved payment — never by a client claim. |
| Search | `ISearchService` (§14) | Query building lives here; swap MySQL → Meilisearch without touching callers. |
| Errors | `ExceptionHandlingMiddleware` (§45) | One place maps exceptions → the error envelope; details logged server-side only. |
| Time | `IDateTimeProvider` | No `DateTime.UtcNow` in business code (keeps premium-expiry logic testable, §24). |
| Current user | `ICurrentUser` | Reads the authenticated principal; use-case code never touches `HttpContext`. |

## 5. Request flows

### 5.1 Authentication & refresh (§44)

```
POST /api/auth/register → AuthService → IIdentityService.CreateUser → assign "Student" role
POST /api/auth/login    → verify credentials → IJwtTokenService.CreateAccessToken
                          + issue RefreshToken row (hashed, expiry) → return { accessToken, refreshToken }
POST /api/auth/refresh  → look up RefreshToken by hash → validate (not expired / revoked)
                          → rotate: revoke old, issue new pair
Access token: short-lived JWT (HS256). Refresh token: opaque, stored hashed, single-use, rotating.
```

### 5.2 Authorized download → signed URL (§29, target Phase 5)

```
GET /api/documents/{slug}/download
  → [Authorize]                                  (401 if anonymous)
  → IAccessControlService.EnsureCanAccess(doc, user)
        free doc            → allow
        premium doc + IsPremiumActive(user) → allow
        premium doc + not premium/expired    → 403 (or preview-only response)
  → IFileStorageService.CreateSignedUrl(key, TTL≈2 min)
  → 302 redirect / return { url }
  → increment DownloadCount (deduped per user/session)
Permanent public storage URLs are never exposed (§29, §31).
```

## 6. API response contract

Envelope for data endpoints (§47):

```json
{ "data": {}, "message": null, "errors": [], "pagination": { "page": 1, "pageSize": 20, "total": 100 } }
```

Error body (§45) — internal exceptions never surface:

```json
{ "success": false, "message": "A human-readable message", "errors": ["field: reason"] }
```

Standard HTTP responses are used where a wrapper adds nothing (e.g. `204 No Content`, health checks) (§47).

## 7. Configuration & secrets

- Non-secret defaults in `appsettings.json`; environment overrides in `appsettings.Development.json`.
- Secrets (`ConnectionStrings:Default`, `Jwt:Secret`, `Storage:S3:*`) via `dotnet user-secrets` (dev) or
  environment variables with `__` nesting (containers/prod). `.env` is never committed; `.env.example` is (§58).
- No credentials in source, ever (§44).

## 8. Multi-tenancy & i18n readiness

- `"USTHB"` is **seed data only**. Every academic entity hangs off `University`; nothing hard-codes USTHB (§10, §67).
- Frontend is built for `fr` / `ar` / `en` with RTL support; user-facing strings are externalized from day one (§72, §73).

## 9. Testing strategy

- **Unit:** `Domain` invariants + `Application` services with mocked ports (NSubstitute). Fast, no I/O.
- **Integration:** real HTTP pipeline via `WebApplicationFactory`; `AppDbContext` swapped to **SQLite in-memory**;
  seeded roles + demo users. Covers auth flow, authorization (401/403), health checks, and later each feature slice.
- **Manual MySQL check:** one migration + `database update` + app-run pass against the local `MySQL80` per phase.
- Security tests from §61 are first-class and must always be green.

## 10. Observability (§57)

- Serilog: console + rolling file sinks, request logging, correlation/trace enrichers.
- Health checks: `/health` (aggregate), `/health/ready` (DB + storage), `/health/live` (process).
