# End-to-end tests (Playwright)

Drives the **real stack** in Chromium: MySQL → .NET API → Next.js (production build), with a fake Google IdP
(`fake-idp.mjs`) so the genuine OAuth code flow, PKCE, state cookie and ID-token validation run end to end.

## Prerequisites

* MySQL reachable locally, and a user that may create the throw-away database.
* .NET 8 SDK, Node 20+, Chromium (`npx playwright install chromium`, or a preinstalled `PLAYWRIGHT_BROWSERS_PATH`).

## Run

```bash
cd frontend
export E2E_DB_CONNECTION='Server=localhost;Database=usthb_e2e;User=<user>;Password=<password>;'
export E2E_MYSQL_ADMIN='mysql -uroot'      # any command that can DROP/CREATE the database
npx playwright test
```

`playwright.config.ts` starts the fake IdP (`:5190`), the API (`:5188`, Development, fresh database, demo data)
and the Next.js server (`:3100`) itself. The database named in `E2E_DB_CONNECTION` is **dropped and recreated**.
