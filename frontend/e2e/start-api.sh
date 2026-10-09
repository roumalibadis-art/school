#!/usr/bin/env bash
# Recreates the throw-away E2E database and starts the real API against it (Development: migrates + seeds the
# demo accounts and sample academic tree). Requires a local MySQL; see e2e/README.md.
set -euo pipefail
: "${E2E_DB_CONNECTION:?Set E2E_DB_CONNECTION, e.g. 'Server=localhost;Database=usthb_e2e;User=...;Password=...'}"
DB_NAME="$(sed -E 's/.*Database=([^;]+).*/\1/' <<<"$E2E_DB_CONNECTION")"
${E2E_MYSQL_ADMIN:-mysql -uroot} -e "DROP DATABASE IF EXISTS \`$DB_NAME\`; CREATE DATABASE \`$DB_NAME\` CHARACTER SET utf8mb4;"

cd "$(dirname "$0")/../.."
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="http://localhost:${API_PORT:-5188}"
export ConnectionStrings__Default="$E2E_DB_CONNECTION"
export Jwt__Secret="e2e-only-secret-e2e-only-secret-e2e-only-1234"
export Seed__DemoUsers=true
export RateLimiting__PermitPerMinute=100000
export RateLimiting__AuthPermitPerMinute=100000
export Storage__LocalRootPath="$(mktemp -d)"
export Authentication__Google__ClientId=e2e-client
export Authentication__Google__ClientSecret=e2e-secret
export Authentication__Google__RedirectUri="http://localhost:${WEB_PORT:-3100}/api/auth/google/callback"
export Authentication__Google__FrontendBaseUrl="http://localhost:${WEB_PORT:-3100}"
export Authentication__Google__AuthorizationEndpoint="http://localhost:${IDP_PORT:-5190}/authorize"
export Authentication__Google__TokenEndpoint="http://localhost:${IDP_PORT:-5190}/token"
export Authentication__Google__JwksUri="http://localhost:${IDP_PORT:-5190}/jwks"
export Cors__AllowedOrigins__0="http://localhost:${WEB_PORT:-3100}"
exec dotnet run --project src/USTHBStudy.API --no-launch-profile
