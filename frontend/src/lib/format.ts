// One fixed demo currency (USD). The backend owns every figure; these helpers only present them.

const usd = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 2 });
const usdCompact = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', notation: 'compact', maximumFractionDigits: 1 });
const int = new Intl.NumberFormat('en-US');

export const money = (n: number): string => usd.format(n);
export const moneyCompact = (n: number): string => usdCompact.format(n);
export const count = (n: number): string => int.format(n);

/** Margin/ratio fraction (0.42) -> "42.3%". Null -> "—" (never NaN). */
export const percentFromFraction = (fraction: number | null | undefined): string =>
  fraction == null ? '—' : `${(fraction * 100).toFixed(1)}%`;

export type Direction = 'up' | 'down' | 'flat';

/** A percentage change (fraction) as display text + direction. Null baseline -> "New". */
export function changeDisplay(changePercent: number | null | undefined): { text: string; dir: Direction } {
  if (changePercent == null) return { text: 'New', dir: 'flat' };
  const pct = changePercent * 100;
  const sign = pct > 0 ? '+' : '';
  return { text: `${sign}${pct.toFixed(1)}%`, dir: pct > 0.05 ? 'up' : pct < -0.05 ? 'down' : 'flat' };
}

/** Margin change in percentage points (fraction difference -> "+2.1 pp"). Null -> "—". */
export function pointsDisplay(changePoints: number | null | undefined): { text: string; dir: Direction } {
  if (changePoints == null) return { text: '—', dir: 'flat' };
  const pp = changePoints * 100;
  const sign = pp > 0 ? '+' : '';
  return { text: `${sign}${pp.toFixed(1)} pp`, dir: pp > 0.05 ? 'up' : pp < -0.05 ? 'down' : 'flat' };
}

const dateFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', timeZone: 'UTC' });
const dateTimeFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: 'UTC' });

export const shortDate = (iso: string): string => dateFmt.format(new Date(iso));
export const dateTime = (iso: string): string => dateTimeFmt.format(new Date(iso));
