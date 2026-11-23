import { Page, expect, test } from '@playwright/test';
import { MANAGER, signIn, fillStable, nextWeek } from './helpers';

// Shift tests work in next week, so they never collide with the seeded demo week.

function rowFor(page: Page, name: string) {
  return page.locator('tbody tr', { has: page.locator('th.name-col', { hasText: name }) });
}

test.describe('scheduling', () => {
  test('a manager adds a shift and the weekly hours update', async ({ page }) => {
    await signIn(page, MANAGER);
    await nextWeek(page);

    const row = rowFor(page, 'Taylor Brooks');
    const hoursBefore = Number(await row.locator('td.hours-col').innerText());

    // Third day column (Wednesday) of next week.
    await row.getByRole('button', { name: 'Add shift for Taylor Brooks' }).nth(2).click();
    const panel = page.getByRole('dialog');
    await expect(panel.getByRole('heading', { name: 'New shift' })).toBeVisible();
    await fillStable(panel.getByLabel('Start'), '09:00');
    await fillStable(panel.getByLabel('End'), '13:30');
    await fillStable(panel.getByLabel('Position'), 'Baker');
    await panel.getByRole('button', { name: 'Save' }).click();

    await expect(page.locator('p.notice')).toHaveText('Shift added.');
    await expect(row.locator('button.shift', { hasText: 'Baker' })).toContainText('09:00–13:30');
    await expect(row.locator('td.hours-col')).toHaveText(String(hoursBefore + 4.5));
  });

  test('double-booking an employee is refused with a clear message', async ({ page }) => {
    await signIn(page, MANAGER);
    await nextWeek(page);
    const row = rowFor(page, 'Alex Moreau');

    const add = row.getByRole('button', { name: 'Add shift for Alex Moreau' }).nth(4);
    await add.click();
    let panel = page.getByRole('dialog');
    await fillStable(panel.getByLabel('Start'), '10:00');
    await fillStable(panel.getByLabel('End'), '14:00');
    await fillStable(panel.getByLabel('Position'), 'Cashier');
    await panel.getByRole('button', { name: 'Save' }).click();
    await expect(page.locator('p.notice')).toHaveText('Shift added.');

    // Same person, overlapping hours on the same day.
    await add.click();
    panel = page.getByRole('dialog');
    await fillStable(panel.getByLabel('Start'), '12:00');
    await fillStable(panel.getByLabel('End'), '16:00');
    await fillStable(panel.getByLabel('Position'), 'Barista');
    await panel.getByRole('button', { name: 'Save' }).click();

    await expect(panel.locator('p.error')).toBeVisible();
    await expect(row.locator('button.shift', { hasText: 'Barista' })).toHaveCount(0);
  });

  test('a manager edits a shift and then deletes it', async ({ page }) => {
    await signIn(page, MANAGER);
    await nextWeek(page);
    const row = rowFor(page, 'Priya Nair');

    await row.getByRole('button', { name: 'Add shift for Priya Nair' }).nth(5).click();
    let panel = page.getByRole('dialog');
    await fillStable(panel.getByLabel('Start'), '08:00');
    await fillStable(panel.getByLabel('End'), '12:00');
    await fillStable(panel.getByLabel('Position'), 'Prep');
    await panel.getByRole('button', { name: 'Save' }).click();

    const shift = row.locator('button.shift', { hasText: 'Prep' });
    await shift.click();
    panel = page.getByRole('dialog');
    await expect(panel.getByRole('heading', { name: 'Edit shift' })).toBeVisible();
    await fillStable(panel.getByLabel('End'), '15:00');
    await panel.getByRole('button', { name: 'Save' }).click();
    await expect(page.locator('p.notice')).toHaveText('Shift updated.');
    await expect(shift).toContainText('08:00–15:00');

    await shift.click();
    page.once('dialog', (dialog) => dialog.accept()); // the "Delete ...?" confirm
    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click();
    await expect(page.locator('p.notice')).toHaveText('Shift deleted.');
    await expect(shift).toHaveCount(0);
  });
});
