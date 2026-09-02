import { describe, expect, it } from 'vitest';
import { dateTime, moneyCompact, pointsDisplay, shortDate } from '../lib/format';

describe('moneyCompact uses frozen 3-significant-figure precision', () => {
  it('formats millions as $1.23M', () => {
    expect(moneyCompact(1_234_567)).toBe('$1.23M');
  });
  it('formats thousands as $12.3K', () => {
    expect(moneyCompact(12_345)).toBe('$12.3K');
  });
  it('keeps three significant figures for low thousands', () => {
    expect(moneyCompact(1_234)).toBe('$1.23K');
  });
  it('formats hundred-thousands without a decimal', () => {
    expect(moneyCompact(123_456)).toBe('$123K');
  });
  it('drops trailing zeros ($1M, not $1.00M)', () => {
    expect(moneyCompact(1_000_000)).toBe('$1M');
  });
  it('shows sub-thousand amounts in full dollars', () => {
    expect(moneyCompact(950)).toBe('$950');
  });
  it('formats zero as $0', () => {
    expect(moneyCompact(0)).toBe('$0');
  });
});

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
