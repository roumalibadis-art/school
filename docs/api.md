# API — USTHB Study

> Living document. REST over HTTPS, JSON. Base path `/api`. Section refs point to `PRD.md`.

## Conventions

- **Auth:** `Authorization: Bearer <accessToken>` (JWT, HS256). Access tokens are short-lived; clients
  refresh via `/api/auth/refresh` with a rotating opaque refresh token (§44).
- **Envelope** for data endpoints (§47):
  ```json
  { "data": {}, "message": null, "errors": [], "pagination": { "page": 1, "pageSize": 20, "total": 100 } }
  ```
  Plain HTTP responses where a wrapper adds nothing (`204`, `/health`).
- **Errors** (§45): `{ "success": false, "message": "...", "errors": ["..."] }`. No stack traces.
- **Pagination** (§55): `?page=1&pageSize=20`, `pageSize` capped (default 20, max 100). Never return unbounded lists.
- **Filtering** (§15): faculty, department, specialty, level, semester, module, documentType, academicYear,
  session, access (`free|premium`).
- **Rate limiting** (§44): global fixed-window; stricter bucket on `/api/auth/*`.
- **Slugs** identify public resources in URLs (§16, §54).

## Phase 1 endpoints

| Method | Route | Auth | Purpose |
|---|---|---|---|
| POST | `/api/auth/register` | anon | Create a Student account (§19 fields land in Phase 5; Phase 1 = email/password/name). |
| POST | `/api/auth/login` | anon | Returns `{ accessToken, refreshToken, expiresAt }`. |
| POST | `/api/auth/refresh` | anon (refresh token in body) | Rotates token pair; old refresh token revoked. |
| POST | `/api/auth/logout` | bearer | Revokes the presented refresh token. |
| GET | `/api/me` | bearer | Current user profile + roles + premium status. |
| GET | `/health` `/health/ready` `/health/live` | anon | Liveness / readiness (§57). |
| GET | `/api/dev/permission-probe` | bearer + `AcademicData.Manage` | Dev-only; used by the §61 "student → 403" test. Removed/kept behind Development only. |

### Auth payloads (Phase 1)

```
POST /api/auth/register
{ "email": "...", "password": "...", "confirmPassword": "...", "firstName": "...", "lastName": "..." }

POST /api/auth/login
{ "email": "...", "password": "..." }
→ 200 { "data": { "accessToken": "...", "refreshToken": "...", "expiresAt": "2026-01-01T00:00:00Z" } }
→ 401 { "success": false, "message": "Invalid credentials", "errors": [] }

POST /api/auth/refresh
{ "refreshToken": "..." }
```

## Academic structure endpoints (Phase 2 — live)

Resources: `universities`, `faculties`, `departments`, `domains`, `specialties`, `levels`,
`semesters`, `academic-years`, `sessions`, `modules`. Each exposes:

| Method | Route | Auth | Notes |
|---|---|---|---|
| GET | `/api/{resource}?parentId=&search=&includeInactive=&page=&pageSize=` | public | `parentId` filters by the immediate parent FK (dependent dropdowns §34); paged envelope |
| GET | `/api/{resource}/{id}` | public | |
| GET | `/api/{resource}/slug/{slug}` | public | |
| POST | `/api/{resource}` | `AcademicData.Manage` | server generates a unique slug |
| PUT | `/api/{resource}/{id}` | `AcademicData.Manage` | full replace; slug regenerated iff name changed |
| DELETE | `/api/{resource}/{id}` | `AcademicData.Manage` | soft delete |

`parentId` targets: faculties→university, departments/domains→faculty, specialties→department,
levels→specialty, semesters→level, modules→semester **or** specialty. `academic-years`/`sessions` have none.

## Planned endpoints (later phases — from §46)

```
# Public (Phase 3–4)
GET  /api/documents            GET /api/documents/{slug}
GET  /api/exams                GET /api/exams/{slug}
GET  /api/search?q=...&<filters>

# Student (Phase 5)
GET  /api/me   PUT /api/me
GET  /api/favorites   POST /api/favorites   DELETE /api/favorites/{id}
GET  /api/documents/{slug}/download          # → authz → signed URL (§29)
GET  /api/history

# Premium (Phase 6)
GET  /api/subscriptions/plans
POST /api/subscriptions                      # creates a pending payment record
GET  /api/subscriptions/me

# Contributions & reports (Phase 7)
POST /api/contributions
POST /api/reports

# Admin (Phase 7) — require permission policies
GET  /api/admin/dashboard
GET  /api/admin/users            PUT /api/admin/users/{id}
GET  /api/admin/documents        POST/PUT/DELETE /api/admin/documents[/{id}]
GET  /api/admin/contributions    POST /api/admin/contributions/{id}/approve|reject
GET  /api/admin/payments         POST /api/admin/payments/{id}/approve|reject
CRUD /api/admin/academic/*       # universities … modules … sessions
```

## Authorization policies (§43)

One policy per permission string; controllers use `[Authorize(Policy = Permissions.X)]`.

| Policy | Held by |
|---|---|
| `Document.View/Create/Update/Delete/Publish` | Admin (all), Moderator (view/update/publish) |
| `User.View/Update/Suspend` | Admin |
| `Subscription.View/Manage` | Admin |
| `AcademicData.Manage` | Admin |
| `Contribution.Moderate` | Admin, Moderator |

Suspended (`IsActive = false`) or expired-premium users are rejected regardless of role/claims (§24, §61).

## Swagger

`/swagger` in Development. JWT bearer scheme registered so protected endpoints are callable from the UI.
