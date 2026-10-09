import { expect, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";
import { STUDENT_PASSWORD, studentEmail } from "./global-setup";

export const state = () => JSON.parse(readFileSync("e2e/.state.json", "utf8")) as { publishedSlug: string };

export async function login(page: Page, email: string, password: string, next?: string) {
  await page.goto(next ? `/login?next=${encodeURIComponent(next)}` : "/login");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Mot de passe").fill(password);
  await page.getByRole("button", { name: "Se connecter" }).click();
  await page.waitForURL((url) => !url.pathname.startsWith("/login"));
}

export const loginStudent = (page: Page, n: number) => login(page, studentEmail(n), STUDENT_PASSWORD);

export async function loginAdmin(page: Page) {
  await login(page, "admin@example.local", "Admin#2026!", "/admin");
  await dismissPrompt(page);
}

/** The login prompt may appear on any non-critical page; answer "later" if it does. */
export async function dismissPrompt(page: Page) {
  const later = page.getByRole("button", { name: "Plus tard" });
  try {
    await later.click({ timeout: 2500 });
  } catch {
    /* no prompt */
  }
}

/** Opens the classification workspace through the polite login prompt, like a real user. */
export async function startFromPrompt(page: Page) {
  // New accounts land on profile completion, which the prompt deliberately never interrupts; it appears on the
  // next ordinary page.
  await page.goto("/documents");
  const dialog = page.getByRole("dialog", { name: /coup de main/i });
  await expect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: "Je classe maintenant" }).click();
  await expect(page).toHaveURL(/\/classify/);
}

export async function chooseOption(page: Page, label: RegExp, option: string) {
  const box = page.getByRole("combobox", { name: label });
  await box.click();
  await box.fill(option);
  await page.getByRole("option", { name: option, exact: true }).click();
}

export async function logout(page: Page) {
  await page.getByRole("button", { name: "Déconnexion" }).click();
  await expect(page.getByRole("link", { name: "Connexion" })).toBeVisible();
}
