import { APIRequestContext, Locator, Page, expect } from '@playwright/test';

export const DEMO_PASSWORD = 'shiftwise123';
export const MANAGER = 'manager@harbour.test';
export const EMPLOYEE = 'sam@harbour.test';

const API = 'http://localhost:5080';

export async function signIn(page: Page, email: string, password = DEMO_PASSWORD): Promise<void> {
  await page.goto('/login');
  await fillStable(page.getByLabel('Email'), email);
  await fillStable(page.getByLabel('Password'), password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/schedule$/);
}

// Uses the API directly to set up data a test needs, so each test only clicks
// through the part of the app it is actually checking.
export async function apiToken(request: APIRequestContext, email: string): Promise<string> {
  const res = await request.post(`${API}/api/auth/login`, {
    data: { email, password: DEMO_PASSWORD },
  });
  expect(res.ok()).toBeTruthy();
  return (await res.json()).token as string;
}

export async function apiGet<T>(
  request: APIRequestContext,
  token: string,
  path: string,
): Promise<T> {
  const res = await request.get(`${API}${path}`, { headers: { Authorization: `Bearer ${token}` } });
  expect(res.ok()).toBeTruthy();
  return (await res.json()) as T;
}

export async function apiPost<T>(
  request: APIRequestContext,
  token: string,
  path: string,
  data: unknown,
): Promise<T> {
  const res = await request.post(`${API}${path}`, {
    headers: { Authorization: `Bearer ${token}` },
    data,
  });
  expect(res.ok(), `${path} -> ${res.status()} ${await res.text()}`).toBeTruthy();
  return (await res.json()) as T;
}

// Local wall-clock time without a zone, the format the API stores (e.g. 2030-03-04T09:00:00).
export function localIso(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;
}

export function uniqueSuffix(): string {
  return Date.now().toString(36);
}

// Angular's ngModel writes its initial value a tick after the form renders, which can wipe
// text typed in the same instant. Fill, then retry until the value has actually stuck.
export async function fillStable(field: Locator, value: string): Promise<void> {
  await expect(async () => {
    await field.fill(value);
    await expect(field).toHaveValue(value, { timeout: 500 });
  }).toPass({ timeout: 10_000 });
}

// Moves the schedule one week forward and waits until the new week's shifts have loaded.
export async function nextWeek(page: Page): Promise<void> {
  const label = page.locator('.page-head p.muted');
  const before = await label.innerText();
  await page.getByRole('button', { name: 'Next week' }).click();
  await expect(label).not.toHaveText(before);
  await expect(page.locator('.grid-wrap.loading')).toHaveCount(0);
}
