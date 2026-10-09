# Deployment & Local Development — USTHB Study

> Living document. Section refs point to `PRD.md`.

## Local development (current machine)

Prereqs already present: .NET SDK 9.0.300 (builds `net8.0`), MySQL 8 (`MySQL80` service, `localhost:3306`),
Node 22, git. **Docker is not used** for this project (explicit decision — keep the toolchain minimal);
run everything with the `dotnet` CLI against the local MySQL service.

### One-time setup (already done on the dev machine)

```sql
-- run as MySQL root (mysql.exe is at "C:\Program Files\MySQL\MySQL Server 8.0\bin")
CREATE DATABASE usthbstudy CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'usthb_app'@'localhost' IDENTIFIED BY '<password>';
GRANT ALL PRIVILEGES ON usthbstudy.* TO 'usthb_app'@'localhost';
FLUSH PRIVILEGES;
```

Secrets for the API project (stored by `dotnet user-secrets`, id `usthbstudy-api`, never committed):

```bash
dotnet user-secrets --project src/USTHBStudy.API set "ConnectionStrings:Default" \
  "Server=localhost;Port=3306;Database=usthbstudy;User=usthb_app;Password=<password>;TreatTinyAsBoolean=true;AllowUserVariables=true"
dotnet user-secrets --project src/USTHBStudy.API set "Jwt:Secret" "<random 48+ char string>"
```

### Run

```bash
dotnet tool restore                                                     # pinned dotnet-ef
dotnet build USTHBStudy.sln
dotnet ef database update -p src/USTHBStudy.Infrastructure -s src/USTHBStudy.API
dotnet run --project src/USTHBStudy.API --no-launch-profile             # http://localhost:5175/swagger , /health
dotnet test USTHBStudy.sln                                             # 40 tests (SQLite in-memory for integration)
```

Windows PowerShell equivalents are identical (the `dotnet` CLI is cross-shell).

## Configuration reference

| Setting | Where | Default (dev) | Notes |
|---|---|---|---|
| `ConnectionStrings:Default` | secret / env | — | MySQL connection string |
| `Jwt:Secret` | secret / env | — | ≥ 32 bytes |
| `Jwt:Issuer` / `Jwt:Audience` | appsettings | `usthbstudy` | |
| `Jwt:AccessTokenMinutes` | appsettings | `15` | |
| `Jwt:RefreshTokenDays` | appsettings | `14` | |
| `Storage:Provider` | appsettings | `Local` | `Local` \| `S3` |
| `Storage:LocalRootPath` | appsettings | `./_storage` | git-ignored |
| `Storage:S3:*` | secret / env | — | endpoint, bucket, keys, `ForcePathStyle` |
| `Cors:AllowedOrigins` | appsettings | `http://localhost:3000` | array |
| `RateLimiting:PermitPerMinute` / `RateLimiting:AuthPermitPerMinute` | appsettings | `100` / `10` | per-IP fixed window |
| `Seed:DemoUsers` | appsettings.Development | `true` | demo accounts (§52) |
| `ASPNETCORE_ENVIRONMENT` | env | `Development` | |

For non-dev hosts, pass secrets as environment variables with `__` nesting
(`ConnectionStrings__Default`, `Jwt__Secret`, `Storage__S3__AccessKey`, …). `.env` files are git-ignored.

## Containerisation (§58)

Deferred. The PRD asks for Docker/compose; the project owner opted to keep the local toolchain
minimal (`dotnet` + local MySQL). If containerisation is revisited later, the needed pieces are a
multi-stage API `Dockerfile` (sdk → aspnet) and a compose file with `mysql`, an S3-compatible store
(e.g. MinIO), `api`, and `frontend`. Configuration is already environment-variable friendly (below).

## Migrations strategy

- **Dev:** run `dotnet ef database update` manually, or auto-apply on startup when
  `ASPNETCORE_ENVIRONMENT != Production`.
- **Production:** generate an idempotent SQL script and apply it as a reviewed deploy step —
  `dotnet ef migrations script --idempotent -o artifacts/migrate.sql`. Never auto-migrate prod (§6).

## Environments

| Env | DB | Storage | Swagger | Seed demo users | HTTPS/HSTS |
|---|---|---|---|---|---|
| Development | local MySQL | Local FS | on | yes | redirect only |
| Staging | managed MySQL | S3/MinIO | on (protected) | no | on |
| Production | managed MySQL | S3 | off | no | on + HSTS |

## Health & observability (§57)

- `/health/live` — process up.
- `/health/ready` — MySQL reachable + storage reachable.
- Serilog → console (structured) + rolling file (`logs/usthbstudy-.log`, daily). Ship to a sink in staging/prod.

## Community classification & Google (additional configuration)

| Key | Source | Notes |
|---|---|---|
| `Authentication:Google:ClientId` / `ClientSecret` / `RedirectUri` / `FrontendBaseUrl` | secret / env | feature off when any of the first three is empty — see [google-auth.md](google-auth.md) |
| `Authentication:Google:AuthorizationEndpoint` / `TokenEndpoint` / `JwksUri` | appsettings | Google defaults; overridden only by the E2E fake IdP |
| `API_URL` (frontend) | env, **build time** | baked into the `/api/*` rewrite; set it for `next build` |

Behaviour (task size, consensus, triggers, quota) is **runtime data**, edited at `/admin/classification/settings`.
Apply the migration before deploying (`dotnet ef database update …`). Run behind HTTPS and keep the Data Protection
key ring persistent/shared if you scale out.

### Tests
```bash
dotnet test USTHBStudy.sln                                    # unit + SQLite integration (MySQL-backed tests skip)
export USTHB_TEST_MYSQL='Server=localhost;User=<u>;Password=<p>;'   # enables concurrency + migration tests
dotnet test USTHBStudy.sln
cd frontend && npm ci && npx tsc --noEmit && npm run build
E2E_DB_CONNECTION='Server=localhost;Database=usthb_e2e;User=<u>;Password=<p>;' E2E_MYSQL_ADMIN='mysql -uroot' npm run e2e
```
