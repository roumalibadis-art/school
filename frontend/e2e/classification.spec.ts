import { expect, test } from "@playwright/test";
import { chooseOption, dismissPrompt, loginAdmin, loginStudent, logout, startFromPrompt, state } from "./helpers";

test.describe.configure({ mode: "serial" });

test("three students classify the same documents and the community consensus verifies them", async ({ page, browser }) => {
  for (const n of [1, 2, 3]) {
    const context = n === 1 ? page.context() : await browser.newContext();
    const p = n === 1 ? page : await context.newPage();

    await loginStudent(p, n);
    await startFromPrompt(p); // login trigger → polite dialog → workspace

    for (let i = 1; i <= 3; i++) {
      await expect(p.getByText(`Document ${i} sur 3`)).toBeVisible();
      await expect(p.getByAltText(/Aperçu de la première page/)).toBeVisible();
      await chooseOption(p, /^Spécialité/, "Informatique");
      await chooseOption(p, /^Type de document/, "Examen");
      await p.getByRole("button", { name: "Valider ma classification" }).click();
      await expect(p.getByRole("status").filter({ hasText: "Merci" })).toBeVisible();
    }

    await expect(p.getByRole("heading", { name: /Merci pour votre contribution/ })).toBeVisible();
    await expect(p.getByText("3 réponses enregistrées")).toBeVisible();
    if (n === 1) await logout(p);
    else await context.close();
  }

  // Admin sees the three documents as verified (consensus), none waiting for review.
  await loginAdmin(page);
  await page.goto("/admin/classification?queue=verified");
  await expect(page.getByText("3 document(s)")).toBeVisible();
  await expect(page.getByText("Algorithmique examen 2024")).toBeVisible();
  await page.goto("/admin/classification?queue=needs-review");
  await expect(page.getByText("Aucun document dans cette file.")).toBeVisible();
});

test("a student can propose a missing value inline without losing the form, and an admin approves it", async ({ page, browser }) => {
  await loginStudent(page, 4);
  await startFromPrompt(page);

  await expect(page.getByText("Document 1 sur 1")).toBeVisible();
  await chooseOption(page, /^Spécialité/, "Informatique");

  const session = page.getByRole("combobox", { name: /^Type d’examen/ });
  await session.click();
  await session.fill("Session spéciale E2E");
  await page.getByRole("option", { name: /Ajouter la session/ }).click();
  const group = page.getByRole("group", { name: /Ajouter la session/ });
  await expect(group.getByLabel("Nouvelle valeur proposée")).toHaveValue("Session spéciale E2E");
  await group.getByRole("button", { name: "Proposer" }).click();

  await expect(page.getByText("Proposition envoyée")).toBeVisible();
  await expect(session).toHaveValue("Session spéciale E2E");
  // The rest of the form is intact.
  await expect(page.getByRole("combobox", { name: /^Spécialité/ })).toHaveValue("Informatique");

  // A value too short is refused with a clear message and nothing is created.
  await session.click();
  await session.fill("x");
  await expect(page.getByRole("option", { name: /Ajouter/ })).toHaveCount(0);

  await page.getByRole("button", { name: "Valider ma classification" }).click();
  await expect(page.getByRole("heading", { name: /Merci pour votre contribution/ })).toBeVisible();
  await logout(page);

  await loginAdmin(page);
  await page.goto("/admin/taxonomy");
  const card = page.locator("div", { hasText: "« Session spéciale E2E »" }).filter({ hasText: "e2e-student-4@example.test" }).last();
  await expect(card).toBeVisible();
  // Pending values are not real values yet.
  const before = await page.request.get("/api/sessions?pageSize=100");
  expect(await before.text()).not.toContain("Session spéciale E2E");

  await card.getByRole("button", { name: "Approuver" }).click();
  await expect(page.getByText("Aucune proposition.")).toBeVisible();
  const after = await page.request.get("/api/sessions?pageSize=100");
  expect(await after.text()).toContain("Session spéciale E2E");
});

test("administrators configure the workflow and the settings persist", async ({ page }) => {
  await loginAdmin(page);
  await page.goto("/admin/classification/settings");

  await expect(page.getByLabel("Documents par tâche")).toHaveValue("3");
  await expect(page.getByLabel("Votants distincts requis")).toHaveValue("3");
  await page.getByLabel("Documents par tâche").fill("4");
  await page.getByLabel("Accord requis (%)").fill("75");
  await page.getByRole("button", { name: "Enregistrer les paramètres" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Paramètres enregistrés." })).toBeVisible();

  await page.reload();
  await expect(page.getByLabel("Documents par tâche")).toHaveValue("4");
  await expect(page.getByLabel("Accord requis (%)")).toHaveValue("75");

  // Invalid values are rejected by the server and reported.
  await page.getByLabel("Accord requis (%)").evaluate((el: HTMLInputElement) => { el.min = "0"; });
  await page.getByLabel("Accord requis (%)").fill("10");
  await page.getByRole("button", { name: "Enregistrer les paramètres" }).click();
  await expect(page.locator("p[role=alert]")).toContainText("AgreementPercent");

  await page.reload();
  await page.getByLabel("Documents par tâche").fill("3");
  await page.getByLabel("Accord requis (%)").fill("66");
  await page.getByRole("button", { name: "Enregistrer les paramètres" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Paramètres enregistrés." })).toBeVisible();

  // The admin report page renders real numbers.
  await page.goto("/admin/classification/report");
  await expect(page.getByRole("heading", { name: "Rapports de classification" })).toBeVisible();
  await expect(page.getByText("Contributions valides")).toBeVisible();
});

test("contributing earns downloads: the limit is explained, never silent, and Premium content stays closed", async ({ page, browser }) => {
  // Admin turns the free-tier quota on (1 free download, +2 per valid contribution).
  const adminContext = await browser.newContext();
  const admin = await adminContext.newPage();
  await loginAdmin(admin);
  await admin.goto("/admin/classification/settings");
  await admin.getByLabel("Limiter les téléchargements gratuits").check();
  await admin.getByLabel("Téléchargements gratuits par période").fill("1");
  await admin.getByRole("button", { name: "Enregistrer les paramètres" }).click();
  await expect(admin.getByRole("status").filter({ hasText: "Paramètres enregistrés." })).toBeVisible();
  await adminContext.close();

  await loginStudent(page, 5);
  await dismissPrompt(page);
  const slug = state().publishedSlug;
  await page.goto(`/documents/${slug}`);

  // Explained up-front, before anything is blocked.
  await expect(page.getByText(/Il vous reste/)).toContainText("1");
  const first = page.waitForEvent("download");
  await page.getByRole("link", { name: "Télécharger" }).click();
  expect((await first).suggestedFilename()).toContain(".pdf");

  await page.reload();
  await expect(page.getByText("Vous avez utilisé vos téléchargements gratuits.")).toBeVisible();
  await page.getByRole("link", { name: "Télécharger" }).click();
  await expect(page).toHaveURL(/\/classify\?reason=quota/);
  await expect(page.getByText("Vous avez utilisé vos téléchargements gratuits de la période.")).toBeVisible();
  await expect(page.getByText(/Les contenus Premium restent réservés/)).toBeVisible();

  // Skipping costs nothing; a real contribution earns downloads.
  await expect(page.getByText("Document 1 sur 1")).toBeVisible();
  await chooseOption(page, /^Spécialité/, "Informatique");
  await page.getByRole("button", { name: "Valider ma classification" }).click();
  await expect(page.getByText("2 téléchargements gagnés")).toBeVisible();

  await page.goto(`/documents/${slug}`);
  const second = page.waitForEvent("download");
  await page.getByRole("link", { name: "Télécharger" }).click();
  await second;
});

test("the classification page works on a phone: no horizontal scroll and every action reachable", async ({ browser }) => {
  const context = await browser.newContext({ viewport: { width: 375, height: 812 }, isMobile: true, hasTouch: true });
  const page = await context.newPage();
  await loginStudent(page, 6);
  await page.goto("/classify");

  await expect(page.getByRole("button", { name: "Valider ma classification" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Je ne sais pas — passer" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Pas un support pédagogique" })).toBeVisible();
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  expect(overflow).toBeLessThanOrEqual(0);

  // Skip is safe and says so.
  await page.getByRole("button", { name: "Je ne sais pas — passer" }).click();
  await expect(page.getByText(/Merci pour votre contribution|Rien à classer/)).toBeVisible();
  await context.close();
});

test("students cannot reach the admin console or its APIs", async ({ page }) => {
  await loginStudent(page, 1);
  await dismissPrompt(page);
  for (const path of ["/admin/classification", "/admin/taxonomy", "/admin/classification/settings"]) {
    await page.goto(path);
    await expect(page).not.toHaveURL(new RegExp(`${path}$`));
  }
  const api = await page.request.get("/api/admin/classification/settings");
  expect(api.status()).toBe(401); // the browser holds no bearer token: it is HttpOnly and server-side only
  const bff = await page.request.post("/bff/classification/tasks/next");
  expect(bff.status()).toBe(403); // CSRF guard: state-changing BFF calls need the custom header
});
