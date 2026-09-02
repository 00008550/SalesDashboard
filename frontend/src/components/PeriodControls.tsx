import { useState } from 'react';
import { type Period, PRESETS } from '../state/period';

export function PeriodControls({ period, onChange }: { period: Period; onChange: (p: Period) => void }) {
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const invalid = from !== '' && to !== '' && from > to;
  const canApply = from !== '' && to !== '' && !invalid;

  return (
    <div className="flex flex-wrap items-center gap-2">
      <div className="inline-flex rounded-lg bg-slate-100 p-0.5 text-sm font-medium" role="group" aria-label="Preset period">
        {PRESETS.map((p) => {
          const active = period.kind === 'preset' && period.preset === p.key;
          return (
            <button
              key={p.key}
              onClick={() => onChange({ kind: 'preset', preset: p.key })}
              aria-pressed={active}
              className={`rounded-md px-3 py-1.5 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-600 ${
                active ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              {p.label}
            </button>
          );
        })}
      </div>

      <div className="flex items-center gap-1.5">
        <input
          type="date"
          aria-label="From date"
          value={from}
          onChange={(e) => setFrom(e.target.value)}
          aria-invalid={invalid || undefined}
          aria-describedby={invalid ? 'date-range-error' : undefined}
          className={`rounded-md border bg-white px-2 py-1.5 text-sm text-slate-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-600 ${invalid ? 'border-rose-400' : 'border-slate-200'}`}
        />
        <span className="text-slate-500" aria-hidden>→</span>
        <input
          type="date"
          aria-label="To date"
          value={to}
          onChange={(e) => setTo(e.target.value)}
          aria-invalid={invalid || undefined}
          aria-describedby={invalid ? 'date-range-error' : undefined}
          className={`rounded-md border bg-white px-2 py-1.5 text-sm text-slate-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-600 ${invalid ? 'border-rose-400' : 'border-slate-200'}`}
        />
        <button
          onClick={() => canApply && onChange({ kind: 'custom', from, to })}
          disabled={!canApply}
          className="rounded-md bg-slate-900 px-3 py-1.5 text-sm font-medium text-white transition-colors hover:bg-slate-700 disabled:cursor-not-allowed disabled:opacity-40 focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-600"
        >
          Apply
        </button>
      </div>

      {invalid && (
        <span id="date-range-error" role="alert" className="text-xs text-rose-600">
          “From” must be on or before “To”.
        </span>
      )}
      {period.kind === 'custom' && !invalid && (
        <span className="rounded bg-slate-100 px-2 py-1 text-xs text-slate-500 tabular">
          {period.from} → {period.to}
        </span>
      )}
    </div>
  );
}
