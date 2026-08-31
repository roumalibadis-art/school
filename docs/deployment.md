# Deployment & Local Development — USTHB Study

> Living document. Section refs point to `PRD.md`.

## Local development (current machine)

Prereqs already present: .NET SDK 9.0.300 (builds `net8.0`), MySQL 8 (`MySQL80` service, `localhost:3306`),
Node 22, git. Docker is **not** installed — the compose path is optional.

### One-time setup

```sql
-- run as MySQL root (mysql.exe is at "C:\Program Files\MySQL\MySQL Server 8.0\bin")
CREATE DATABASE usthbstudy CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'usthb_app'@'localhost' IDENTIFIED BY '<password>';
GRANT ALL PRIVILEGES ON usthbstudy.* TO 'usthb_app'@'localhost';
FLUSH PRIVILEGES;
```

```bash
dotnet tool restore                                   # restores pinned dotnet-ef

# secrets for the API project (never committed)
cd src/USTHBStudy.API
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Port=3306;Database=usthbstudy;User=usthb_app;Password=<password>;"
dotnet user-secrets set "Jwt:Secret" "<random 48+ char string>"
cd ../..
```

### Run

```bash
dotnet build USTHBStudy.sln
dotnet ef database update -p src/USTHBStudy.Infrastructure -s src/USTHBStudy.API
dotnet run --project src/USTHBStudy.API          # https://localhost:7xxx/swagger , /health
dotnet test                                       # unit + integration (SQLite in-memory)
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
| `RateLimiting:PermitPerMinute` | appsettings | `100` (global), `10` (auth) | |
| `Seed:DemoUsers` | appsettings.Development | `true` | demo accounts (§52) |
| `ASPNETCORE_ENVIRONMENT` | env | `Development` | |

`.env.example` lists every variable for the container path; copy to `.env` (git-ignored) and fill in.

## Docker (§58) — written, not exercised locally

`docker compose config` is validated in CI/verification; full `up` requires Docker Desktop.

```
docker-compose.yml
  mysql      : mysql:8.0            volume mysql-data, healthcheck
  minio      : S3-compatible storage (buckets on first run)
  api        : build src/USTHBStudy.API/Dockerfile, depends_on mysql (healthy)
  frontend   : added in Phase 4
```

```bash
cp .env.example .env      # fill secrets
docker compose up --build
docker compose exec api dotnet ef database update   # or run migrations on startup (non-prod only)
```

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
