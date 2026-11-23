import { expect, test } from '@playwright/test';
import { EMPLOYEE, MANAGER, signIn, uniqueSuffix, fillStable } from './helpers';

test.describe('signing in and roles', () => {
  test('a signed-out visitor is sent to the sign-in page', async ({ page }) => {
    await page.goto('/schedule');
    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
  });

  test('a wrong password shows an error and stays on the sign-in page', async ({ page }) => {
    await page.goto('/login');
    await fillStable(page.getByLabel('Email'), MANAGER);
    await fillStable(page.getByLabel('Password'), 'not-the-password');
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page.locator('p.error')).toBeVisible();
    await expect(page).toHaveURL(/\/login$/);
  });

  test('a manager sees the whole team and the manager-only pages', async ({ page }) => {
    await signIn(page, MANAGER);

    const nav = page.locator('nav.topbar');
    await expect(nav.getByRole('link', { name: 'Team' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Hours' })).toBeVisible();
    await expect(nav.locator('.who')).toHaveText('Jordan Lee');

    // The demo café has four staff plus the manager on the schedule grid.
    for (const name of ['Sam Patel', 'Priya Nair', 'Alex Moreau', 'Taylor Brooks']) {
      await expect(page.locator('tbody th.name-col', { hasText: name })).toBeVisible();
    }
  });

  test('an employee cannot open manager pages, even by typing the URL', async ({ page }) => {
    await signIn(page, EMPLOYEE);

    const nav = page.locator('nav.topbar');
    await expect(nav.getByRole('link', { name: 'Team' })).toHaveCount(0);
    await expect(nav.getByRole('link', { name: 'Hours' })).toHaveCount(0);

    await page.goto('/team');
    await expect(page).toHaveURL(/\/schedule$/);
    await page.goto('/hours');
    await expect(page).toHaveURL(/\/schedule$/);
  });

  test('signing out ends the session', async ({ page }) => {
    await signIn(page, EMPLOYEE);
    await page.getByRole('button', { name: 'Sign out' }).click();
    await expect(page).toHaveURL(/\/login$/);

    await page.goto('/schedule');
    await expect(page).toHaveURL(/\/login$/);
  });

  test('a new business can register and lands on its team page as manager', async ({ page }) => {
    const id = uniqueSuffix();
    await page.goto('/register');
    await fillStable(page.getByLabel('Business name'), `Test Bakery ${id}`);
    await fillStable(page.getByLabel('Your name'), 'Robin Owner');
    await fillStable(page.getByLabel('Email'), `owner-${id}@bakery.test`);
    await fillStable(page.getByLabel(/Password/), 'bakery-pass-1');
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page).toHaveURL(/\/team$/);
    const row = page.locator('tbody tr', { hasText: `owner-${id}@bakery.test` });
    await expect(row).toContainText('Manager');
    // A new business starts empty: no demo staff leak in from the café.
    await expect(page.locator('tbody tr', { hasText: 'harbour.test' })).toHaveCount(0);
  });
});
