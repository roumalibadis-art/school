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

## Document endpoints (Phase 3 — live)

| Method | Route | Auth | Notes |
|---|---|---|---|
| GET | `/api/documents?moduleId=&specialtyId=&academicYearId=&sessionId=&type=&status=&isPremium=&search=&page=&pageSize=` | public | non-staff see only `Published`; staff may filter by `status` (§14/§15) |
| GET | `/api/documents/{slug}` | public | `Published` for anyone; other statuses → 404 unless staff. Increments `viewCount` |
| GET | `/api/documents/{slug}/preview` | public | first-page PNG (§31) |
| GET | `/api/documents/id/{id}` | `Document.Update` | staff fetch by id (any status) |
| POST | `/api/documents` | `Document.Create` | `multipart/form-data`: `File` + metadata fields. Creates a `Draft`. Response `message` warns if the file hash already exists (§75) |
| PUT | `/api/documents/{id}` | `Document.Update` | metadata replace; slug regenerated iff title changed |
| POST | `/api/documents/{id}/status` | `Document.Publish` | body `{ status, note }` — `Published` / `Rejected` / `Archived` |
| POST | `/api/documents/{parentId}/solutions/{solutionId}` | `Document.Update` | links a solution to its exam (§13) |
| DELETE | `/api/documents/{id}` | `Document.Delete` | soft delete |

Upload constraints (§36): extensions `pdf`, `png`, `jpg`, `jpeg`; content sniffed against the extension;
size ≤ `Documents:MaxFileSizeBytes` (50 MB default).

## Search endpoint (Phase 4 — live)

`GET /api/search` (public). Query params: `q` (free text; a bare 4-digit token is treated as an
academic year), `facultyId`, `departmentId`, `specialtyId`, `levelId`, `semesterId`, `moduleId`,
`type`, `academicYearId`, `sessionId`, `isPremium`, `page`, `pageSize`. Returns a paged envelope of
hits: `{ id, title, slug, type, moduleName, moduleSlug, specialtyName, year, session, isPremium, hasPreview }`.
Only `Published` documents are searchable.

## Planned endpoints (later phases — from §46)

```
# Public
GET  /api/exams                GET /api/exams/{slug}   # frontend currently uses /api/documents?type=Exam

# Student (Phase 5 — live)
GET  /api/me                       PUT /api/me                    # profile + academic profile (§19)
GET  /api/me/dashboard             GET /api/me/history            # §20/§21, §28
GET  /api/favorites   POST /api/favorites   DELETE /api/favorites?kind=&entityId=   # §27
GET  /api/documents/{slug}/download    # [Authorize] → account-active + Premium check → { url, expiresAtUtc, fileName }
GET  /api/files?t=<token>             # [AllowAnonymous] the signed token is the credential (§29)

# Admin (Phase 5 — live; full user mgmt is Phase 7)
POST   /api/admin/users/{id}/premium   { months }      # Subscription.Manage
DELETE /api/admin/users/{id}/premium
POST   /api/admin/users/{id}/suspend | /restore        # User.Suspend

# Premium (Phase 6 — live)
GET  /api/subscriptions/plans                 # public; active plans, prices from DB (§22)
POST /api/subscriptions   { planId }          # student; → { subscription, reference, instructions } (§25)
GET  /api/subscriptions/me                    # student; current + history + effective premium + pending instructions
CRUD /api/admin/subscription-plans            # Subscription.Manage
GET  /api/admin/payments?status=&page=&pageSize=
POST /api/admin/payments/{id}/approve  { note }   # → Success + activates/stacks Premium (§24)
POST /api/admin/payments/{id}/reject   { note }
POST /api/admin/subscriptions/{id}/extend { days }

# Contributions & reports (Phase 7 — live)
POST /api/contributions               # student, multipart (Title/Type/ModuleId/Description/File)
GET  /api/me/contributions
POST /api/documents/{slug}/report     { reason, comment }
GET  /api/me/notifications  (+ /unread-count, POST /read?id=)   # §41

# Admin (Phase 7 — live)
GET  /api/admin/dashboard                              # Users.View
GET  /api/admin/users?search=&role=&isActive=&isPremium=       GET /api/admin/users/{id}
PUT  /api/admin/users/{id}/roles   { roles: [] }       # Users.Update
GET  /api/admin/contributions?status=   POST /api/admin/contributions/{id}/approve|reject   # Contribution.Moderate
GET  /api/admin/reports?status=          POST /api/admin/reports/{id}/resolve   { status, note }  # Report.Resolve
GET  /api/admin/audit?action=&entityType=             # AuditLog.View
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
