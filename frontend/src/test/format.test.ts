import { describe, expect, it } from 'vitest';
import { dateTime, pointsDisplay, shortDate } from '../lib/format';

describe('reporting-timezone formatting (UTC+03:00 / MSK)', () => {
  it('formats an instant in MSK, not UTC', () => {
    // 08:35Z + 3h = 11:35 MSK
    expect(dateTime('2026-09-02T08:35:00Z')).toContain('11:35');
  });

  it('an MSK-midnight bucket stored as 21:00Z shows the following MSK calendar date', () => {
    // 2026-09-01T21:00:00Z == 2026-09-02 00:00 MSK
    expect(shortDate('2026-09-01T21:00:00Z')).toBe('Sep 2');
  });
});

describe('pointsDisplay does not multiply an already-pp value', () => {
  it('renders a pp delta verbatim', () => {
    expect(pointsDisplay(-4.7).text).toBe('-4.7 pp');
    expect(pointsDisplay(2.1).text).toBe('+2.1 pp');
    expect(pointsDisplay(null).text).toBe('—');
  });
});
