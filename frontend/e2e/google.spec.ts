import { expect, test } from "@playwright/test";
import { IDP } from "./global-setup";
import { logout } from "./helpers";

test.describe.configure({ mode: "serial" });

const setIdentity = (identity: Record<string, unknown>) =>
  fetch(`${IDP}/__identity`, { method: "POST", body: JSON.stringify(identity) });

test("Sign in with Google creates a plain student account through the real OAuth/OIDC flow, and sign-out works", async ({ page }) => {
  await setIdentity({ sub: "e2e-sub-gina", email: "gina.e2e@example.test", email_verified: true, given_name: "Gina" });

  await page.goto("/login");
  const google = page.getByRole("link", { name: "Continuer avec Google" });
  await expect(google).toBeVisible();
  await google.click();

  // Back from "Google": signed in, new accounts land on profile completion.
  await page.waitForURL(/\/profile/);
  await expect(page.getByRole("link", { name: "Gina" })).toBeVisible();

  const session = await (await page.request.get("/api/session")).json();
  expect(session).toMatchObject({ authenticated: true, firstName: "Gina", isStaff: false });
  await page.goto("/admin");
  await expect(page).not.toHaveURL(/\/admin$/); // no elevated access from signing in with Google

  await page.goto("/profile");
  await expect(page.getByText("Un compte Google est lié")).toBeVisible();

  await logout(page);
  expect((await (await page.request.get("/api/session")).json()).authenticated).toBe(false);

  // Signing in again reuses the same account (no duplicate).
  await page.goto("/login");
  await page.getByRole("link", { name: "Continuer avec Google" }).click();
  await page.waitForURL(/\/profile|\/dashboard/);
  expect((await (await page.request.get("/api/session")).json()).firstName).toBe("Gina");
});

test("Google never signs anyone into an existing admin account by matching its email", async ({ page }) => {
  await setIdentity({ sub: "e2e-sub-evil", email: "admin@example.local", email_verified: true, given_name: "Mallory" });

  await page.goto("/login");
  await page.getByRole("link", { name: "Continuer avec Google" }).click();

  await page.waitForURL(/\/login\?error=google_link_required/);
  await expect(page.locator("p[role=alert]")).toContainText("Un compte existe déjà avec cette adresse");
  expect((await (await page.request.get("/api/session")).json()).authenticated).toBe(false);
});

test("an unverified Google email is refused", async ({ page }) => {
  await setIdentity({ sub: "e2e-sub-unv", email: "unverified.e2e@example.test", email_verified: false });
  await page.goto("/login");
  await page.getByRole("link", { name: "Continuer avec Google" }).click();
  await page.waitForURL(/\/login\?error=google_unverified/);
  await expect(page.locator("p[role=alert]")).toContainText("n’est pas vérifiée");
});

test("a tampered or replayed callback is rejected", async ({ page }) => {
  await setIdentity({ sub: "e2e-sub-replay", email: "replay.e2e@example.test", email_verified: true });
  const res = await page.request.get("/api/auth/google/callback?code=abc&state=forged", { maxRedirects: 0 });
  expect(res.status()).toBe(302);
  expect(res.headers().location).toContain("/login?error=google_state");

  const exchange = await page.request.post("/api/auth/google/exchange", { data: { ticket: "not-a-real-ticket" } });
  expect(exchange.status()).toBe(401);
});
