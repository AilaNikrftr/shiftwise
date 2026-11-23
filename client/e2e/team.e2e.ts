import { expect, test } from '@playwright/test';
import { Page } from '@playwright/test';
import { MANAGER, fillStable, signIn, uniqueSuffix } from './helpers';

// The add-employee form is only safe to type into once the team list has loaded:
// that first render after the request comes back also settles the form's initial values.
async function openTeam(page: Page): Promise<void> {
  await page.getByRole('link', { name: 'Team' }).click();
  await expect(page.locator('tbody tr', { hasText: MANAGER })).toBeVisible();
}

test.describe('team management', () => {
  test('a manager adds an employee, who can then sign in', async ({ page }) => {
    const id = uniqueSuffix();
    const email = `casey-${id}@harbour.test`;

    await signIn(page, MANAGER);
    await openTeam(page);

    await fillStable(page.getByLabel('Name'), 'Casey Kim');
    await fillStable(page.getByLabel('Email'), email);
    await fillStable(page.getByLabel('Temporary password'), 'welcome-123');
    await page.getByRole('button', { name: 'Add' }).click();

    await expect(page.locator('tbody tr', { hasText: email })).toContainText('Employee');

    await page.getByRole('button', { name: 'Sign out' }).click();
    await signIn(page, email, 'welcome-123');
    await expect(page.locator('nav.topbar .who')).toHaveText('Casey Kim');
  });

  test('adding someone whose email is already used shows an error', async ({ page }) => {
    await signIn(page, MANAGER);
    await openTeam(page);

    await fillStable(page.getByLabel('Name'), 'Another Sam');
    await fillStable(page.getByLabel('Email'), 'sam@harbour.test');
    await fillStable(page.getByLabel('Temporary password'), 'welcome-123');
    await page.getByRole('button', { name: 'Add' }).click();

    await expect(page.locator('p.error')).toBeVisible();
    await expect(page.locator('tbody tr', { hasText: 'sam@harbour.test' })).toHaveCount(1);
  });
});
