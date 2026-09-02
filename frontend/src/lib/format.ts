// One fixed demo currency (USD). The backend owns every figure; these helpers only present them.

const usd = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 2 });
// Compact money at a frozen 3-significant-figure precision: $1.23M, $12.3K, $123K, $950. Significant
// digits (not fraction digits) keep the precision consistent across magnitudes and drop trailing
// zeros ($1M, not $1.00M).
const usdCompact = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', notation: 'compact', maximumSignificantDigits: 3 });
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

/** Margin delta already in percentage points ("+2.1 pp"). Null -> "—". Does NOT multiply again. */
export function pointsDisplay(deltaPp: number | null | undefined): { text: string; dir: Direction } {
  if (deltaPp == null) return { text: '—', dir: 'flat' };
  const sign = deltaPp > 0 ? '+' : '';
  return { text: `${sign}${deltaPp.toFixed(1)} pp`, dir: deltaPp > 0.05 ? 'up' : deltaPp < -0.05 ? 'down' : 'flat' };
}

// All dates are displayed in the documented reporting timezone (UTC+03:00 / MSK). Etc/GMT-3 is the
// IANA name for a fixed UTC+3 offset (sign inverted), matching the backend's fixed offset exactly.
const REPORTING_TZ = 'Etc/GMT-3';
const dateFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', timeZone: REPORTING_TZ });
const dateTimeFmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: REPORTING_TZ });

export const shortDate = (iso: string): string => dateFmt.format(new Date(iso));
export const dateTime = (iso: string): string => dateTimeFmt.format(new Date(iso));
