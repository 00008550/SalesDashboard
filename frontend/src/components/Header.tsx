import type { Period } from '../state/period';
import { PeriodControls } from './PeriodControls';

export function Header({
  period,
  onChange,
  updating,
  timezone,
}: {
  period: Period;
  onChange: (p: Period) => void;
  updating: boolean;
  timezone?: string;
}) {
  return (
    <header className="sticky top-0 z-10 border-b border-slate-200 bg-white/90 backdrop-blur">
      <div className="mx-auto flex max-w-[1440px] flex-wrap items-center justify-between gap-3 px-6 py-3">
        <div>
          <h1 className="text-lg font-semibold tracking-tight text-slate-900">Sales Performance</h1>
          <p className="text-xs text-slate-400">Reporting timezone: {timezone ?? 'UTC+03:00 (MSK)'}</p>
        </div>
        <div className="flex items-center gap-3">
          <span
            className={`inline-flex items-center gap-1.5 text-xs font-medium text-slate-500 transition-opacity duration-200 ${updating ? 'opacity-100' : 'opacity-0'}`}
            aria-live="polite"
          >
            <span className="h-2 w-2 animate-pulse rounded-full bg-blue-500" aria-hidden />
            Updating…
          </span>
          <PeriodControls period={period} onChange={onChange} />
        </div>
      </div>
    </header>
  );
}
