import { addDays, combine, hoursBetween, startOfWeek, toLocalIso } from './dates';

describe('dates', () => {
  it('starts the week on Monday', () => {
    // Sunday Oct 18, 2026 belongs to the week starting Monday Oct 12.
    expect(toLocalIso(startOfWeek(new Date(2026, 9, 18, 14, 30)))).toBe('2026-10-12T00:00:00');
    expect(toLocalIso(startOfWeek(new Date(2026, 9, 12, 9, 0)))).toBe('2026-10-12T00:00:00');
  });

  it('formats local time without converting to UTC', () => {
    expect(toLocalIso(new Date(2026, 0, 5, 7, 5))).toBe('2026-01-05T07:05:00');
  });

  it('crosses month boundaries when adding days', () => {
    expect(toLocalIso(addDays(new Date(2026, 9, 30), 3))).toBe('2026-11-02T00:00:00');
  });

  it('measures shift length in hours', () => {
    expect(hoursBetween(combine('2026-10-15', '09:00'), combine('2026-10-15', '13:30'))).toBe(4.5);
  });
});
