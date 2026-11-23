import { expect, test } from '@playwright/test';
import {
  EMPLOYEE,
  MANAGER,
  apiGet,
  apiPost,
  apiToken,
  localIso,
  signIn,
  fillStable,
  nextWeek,
} from './helpers';

interface Person {
  id: number;
  email: string;
  fullName: string;
}

test('an employee asks a coworker to take a shift and the manager approves it', async ({
  page,
  request,
}) => {
  // Setup through the API: a shift for Sam two weeks out, so it is always "upcoming".
  const token = await apiToken(request, MANAGER);
  const people = await apiGet<Person[]>(request, token, '/api/employees');
  const sam = people.find((p) => p.email === EMPLOYEE)!;
  const start = new Date();
  start.setDate(start.getDate() + 14);
  start.setHours(6, 0, 0, 0);
  const end = new Date(start);
  end.setHours(10);
  await apiPost(request, token, '/api/shifts', {
    employeeId: sam.id,
    startsAt: localIso(start),
    endsAt: localIso(end),
    position: 'Opening',
    notes: null,
  });

  // Sam asks Priya to cover it.
  await signIn(page, EMPLOYEE);
  await nextWeek(page);
  await nextWeek(page);
  await page.locator('button.shift.mine', { hasText: 'Opening' }).click();

  const panel = page.getByRole('dialog');
  await expect(
    panel.getByRole('heading', { name: 'Ask someone to take this shift' }),
  ).toBeVisible();
  await panel.getByLabel('Coworker').selectOption({ label: 'Priya Nair' });
  await fillStable(panel.getByLabel(/Note/), 'Dentist appointment');
  await panel.getByRole('button', { name: 'Send request' }).click();
  await expect(page.locator('p.notice')).toContainText('Swap requested');

  // The manager sees it waiting and approves it.
  await page.getByRole('button', { name: 'Sign out' }).click();
  await signIn(page, MANAGER);
  await page.getByRole('link', { name: 'Swaps' }).click();
  const card = page.locator('article.swap', { hasText: 'Dentist appointment' });
  await expect(card).toContainText('Sam Patel → Priya Nair');
  await card.getByRole('button', { name: 'Approve' }).click();
  await expect(card).toHaveCount(0);
  await expect(page.locator('.badge[data-status="Approved"]').first()).toBeVisible();

  // The shift now belongs to Priya on the schedule.
  await page.getByRole('link', { name: 'Schedule' }).first().click();
  await nextWeek(page);
  await nextWeek(page);
  const priya = page.locator('tbody tr', {
    has: page.locator('th.name-col', { hasText: 'Priya Nair' }),
  });
  await expect(priya.locator('button.shift', { hasText: 'Opening' })).toBeVisible();
});
