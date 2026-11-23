import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { MANAGER, signIn } from './helpers';

test.describe('hours report', () => {
  test('shows each person on the schedule and a total that matches the rows', async ({ page }) => {
    await signIn(page, MANAGER);
    await page.getByRole('link', { name: 'Hours' }).click();

    const rows = page.locator('table.table tbody tr');
    await expect(rows.first()).toBeVisible();
    for (const name of ['Sam Patel', 'Priya Nair', 'Alex Moreau', 'Taylor Brooks']) {
      await expect(rows.filter({ hasText: name })).toHaveCount(1);
    }

    const hours = await rows.locator('td.num:last-child').allInnerTexts();
    const sum = hours.reduce((total, h) => total + Number(h.replace(/,/g, '')), 0);
    const shown = Number((await page.locator('tfoot th.num').innerText()).replace(/,/g, ''));
    expect(shown).toBeCloseTo(sum, 2);
    expect(shown).toBeGreaterThan(0);
  });

  test('exports the report as a CSV file', async ({ page }) => {
    await signIn(page, MANAGER);
    await page.getByRole('link', { name: 'Hours' }).click();
    await expect(page.locator('table.table tbody tr').first()).toBeVisible();

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('button', { name: 'Export CSV' }).click(),
    ]);
    expect(download.suggestedFilename()).toMatch(
      /^hours_\d{4}-\d{2}-\d{2}_to_\d{4}-\d{2}-\d{2}\.csv$/,
    );

    const csv = await readFile((await download.path())!, 'utf8');
    const lines = csv.trim().split(/\r?\n/);
    expect(lines.length).toBeGreaterThan(1);
    expect(csv).toContain('Sam Patel');
  });
});
