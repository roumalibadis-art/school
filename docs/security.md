# Security — USTHB Study

> Living document. Backed by PRD §44 (controls), §61 (critical tests), §36 (uploads), §29 (downloads).

## Threat model (summary)

The platform sells access to gated content, so the primary threats are: **paywall bypass**
(free/expired users reaching Premium files), **privilege escalation** (students hitting admin APIs),
**malicious uploads** (contribution system), and **credential attacks** on the auth endpoints.
The backend must stay correct even if the frontend is fully attacker-controlled (§61).

## Controls (§44)

| Area | Control |
|---|---|
| Passwords | ASP.NET Core Identity hashing (PBKDF2); min length + complexity + lockout on failed attempts. |
| Tokens | JWT access (short TTL, HS256, `Jwt:Secret` ≥ 32 bytes from secrets/env). Refresh tokens: opaque, stored **hashed** (SHA-256), single-use, rotating, rev{ocable}. |
| AuthZ | Per-permission policies (§43). `IAccessControlService` is the only decision point for premium/ownership. Suspended & expired-premium users rejected centrally. |
| Transport | HTTPS redirection + HSTS (non-dev). Secure, `HttpOnly`, `SameSite` cookies if cookies are ever used. |
| Headers | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, minimal `Content-Security-Policy`, `Permissions-Policy`. |
| CORS | Explicit all-listed origins from config; no wildcard with credentials. |
| Rate limiting | Global fixed-window; tighter bucket for `/api/auth/*` (brute-force resistance). |
| Input | FluentValidation on every request DTO; EF Core parameterization (no string-concatenated SQL). |
| Output | API never returns exception details/stack traces (§45); errors logged server-side with correlation id. |
| Uploads (§36) | Validate extension **and** content sniff (magic bytes), size cap, filename sanitization, storage-path containment. Never trust client `Content-Type`. Store outside web root; serve only via authorized, signed, time-boxed URLs. |
| Downloads (§29) | `Authorize` → `IAccessControlService.EnsureCanAccess` → short-TTL signed URL. No permanent public object URLs. |
| Secrets | Never in source or logs (§42, §44). `dotnet user-secrets` (dev) / env vars (prod). `.env` git-ignored. |
| Audit | `AuditLog` (Phase 7) records admin actions without storing secrets/tokens (§42). |

## Critical security tests (§61) — must always be green

| # | Scenario | Expected |
|---|---|---|
| 1 | Free (or unauthenticated) user requests a Premium document | `403` (or a preview-only business response) |
| 2 | Unauthenticated download request | Fail (`401`) |
| 3 | Student calls an admin endpoint | `403` |
| 4 | Suspended user (`IsActive = false`) uses a restricted feature | Fail |
| 5 | User whose `PremiumExpiresAt` < now accesses Premium content | Fail — treated as free (§24) |
| 6 | Client tampers with JS / forges request fields | Backend authorization unaffected |
| 7 | Expired / revoked / reused refresh token | `401`, token chain invalidated |
| 8 | Registration/login flooding | Rate-limited |

Phase 1 implements and tests #2, #3, #6 (partial), #7. #1, #4, #5 land with their features and are
added to the suite then — but `IAccessControlService` and its unit tests encode the logic from Phase 1.

## Secret inventory

| Key | Dev source | Prod source |
|---|---|---|
| `ConnectionStrings:Default` | user-secrets | env `ConnectionStrings__Default` |
| `Jwt:Secret` | user-secrets | env `Jwt__Secret` / secret store |
| `Storage:S3:AccessKey` / `SecretKey` | user-secrets | env / secret store |
| SMTP password (Phase 7) | user-secrets | env / secret store |

## Non-goals / explicit limits

- The platform does **not** implement or encourage copyright circumvention (§40). Documents carry
  `RightsStatus` + `Source` + `PermissionNotes`; a takedown/report path exists (§39, §40).
- Premium content is **not** offline-cached by the PWA unless the authorization model explicitly allows it (§50).
