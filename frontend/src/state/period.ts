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

/** Stable cache/identity key for a period. */
export function periodKey(p: Period): string {
  return p.kind === 'preset' ? `preset:${p.preset}` : `custom:${p.from}:${p.to}`;
}

export function periodToQuery(p: Period): string {
  return p.kind === 'preset'
    ? `preset=${encodeURIComponent(p.preset)}`
    : `from=${encodeURIComponent(p.from)}&to=${encodeURIComponent(p.to)}`;
}
