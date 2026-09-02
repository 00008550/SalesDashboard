export type PresetKey = 'today' | 'last7' | 'last30' | 'thisMonth' | 'prevMonth';

export type Period =
  | { kind: 'preset'; preset: PresetKey }
  | { kind: 'custom'; from: string; to: string }; // inclusive date-only YYYY-MM-DD

export const PRESETS: readonly { key: PresetKey; label: string }[] = [
  { key: 'today', label: 'Today' },
  { key: 'last7', label: 'Last 7 days' },
  { key: 'last30', label: 'Last 30 days' },
  { key: 'thisMonth', label: 'This month' },
  { key: 'prevMonth', label: 'Previous month' },
];

export const DEFAULT_PERIOD: Period = { kind: 'preset', preset: 'last30' };

export const MAX_CUSTOM_RANGE_DAYS = 731;

const MILLISECONDS_PER_DAY = 24 * 60 * 60 * 1000;

function dateOnlyToUtcMilliseconds(value: string): number {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return Number.NaN;

  const parsed = new Date(`${value}T00:00:00.000Z`);
  return Number.isNaN(parsed.getTime()) || parsed.toISOString().slice(0, 10) !== value
    ? Number.NaN
    : parsed.getTime();
}

/** Returns an inline validation message once both custom-range dates have been entered. */
export function customRangeValidationMessage(from: string, to: string): string | null {
  if (from === '' || to === '') return null;

  const fromMilliseconds = dateOnlyToUtcMilliseconds(from);
  const toMilliseconds = dateOnlyToUtcMilliseconds(to);
  if (!Number.isFinite(fromMilliseconds) || !Number.isFinite(toMilliseconds)) {
    return 'Enter a valid “From” and “To” date.';
  }
  if (fromMilliseconds > toMilliseconds) {
    return '“From” must be on or before “To”.';
  }

  const inclusiveDays = (toMilliseconds - fromMilliseconds) / MILLISECONDS_PER_DAY + 1;
  return inclusiveDays > MAX_CUSTOM_RANGE_DAYS
    ? `Date ranges can include at most ${MAX_CUSTOM_RANGE_DAYS} days.`
    : null;
}

/** Stable cache/identity key for a period. */
export function periodKey(p: Period): string {
  return p.kind === 'preset' ? `preset:${p.preset}` : `custom:${p.from}:${p.to}`;
}

export function periodToQuery(p: Period): string {
  return p.kind === 'preset'
    ? `preset=${encodeURIComponent(p.preset)}`
    : `from=${encodeURIComponent(p.from)}&to=${encodeURIComponent(p.to)}`;
}
