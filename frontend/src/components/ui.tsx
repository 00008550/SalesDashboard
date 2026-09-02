import type { ReactNode } from 'react';
import type { Direction } from '../lib/format';

export function Card({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <section className={`rounded-xl border border-slate-200 bg-white shadow-sm ${className}`}>
      {children}
    </section>
  );
}

export function CardHeader({ title, subtitle, action }: { title: string; subtitle?: string; action?: ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-3 border-b border-slate-100 px-5 py-3.5">
      <div>
        <h2 className="text-sm font-semibold text-slate-800">{title}</h2>
        {subtitle && <p className="mt-0.5 text-xs text-slate-500">{subtitle}</p>}
      </div>
      {action}
    </div>
  );
}

const dirClass: Record<Direction, string> = {
  up: 'text-emerald-700 bg-emerald-50 ring-emerald-600/20',
  down: 'text-rose-700 bg-rose-50 ring-rose-600/20',
  flat: 'text-slate-600 bg-slate-100 ring-slate-500/20',
};
const dirArrow: Record<Direction, string> = { up: '▲', down: '▼', flat: '·' };

export function Delta({ display, title }: { display: { text: string; dir: Direction }; title?: string }) {
  return (
    <span
      title={title}
      className={`inline-flex items-center gap-1 rounded-full px-1.5 py-0.5 text-xs font-medium ring-1 ring-inset tabular ${dirClass[display.dir]}`}
    >
      <span aria-hidden>{dirArrow[display.dir]}</span>
      {display.text}
    </span>
  );
}

const statusStyle: Record<string, string> = {
  Paid: 'text-emerald-700 bg-emerald-50 ring-emerald-600/20',
  Cancelled: 'text-slate-600 bg-slate-100 ring-slate-500/20',
  Refunded: 'text-amber-700 bg-amber-50 ring-amber-600/20',
};

/** Status shown by text + a shaped mark, never colour alone (accessibility). */
export function StatusBadge({ status }: { status: string }) {
  const mark = status === 'Paid' ? '●' : status === 'Refunded' ? '↩' : '✕';
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${statusStyle[status] ?? statusStyle.Cancelled}`}>
      <span aria-hidden>{mark}</span>
      {status}
    </span>
  );
}

export function EmptyState({ message }: { message: string }) {
  return (
    <div className="flex h-full min-h-[120px] flex-col items-center justify-center gap-1 px-6 py-10 text-center">
      <div className="text-sm font-medium text-slate-500">{message}</div>
      <div className="text-xs text-slate-500">Try a different period.</div>
    </div>
  );
}

export function ErrorCard({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <Card className="mt-4">
      <div className="flex flex-col items-center justify-center gap-3 px-6 py-16 text-center">
        <div className="text-base font-semibold text-slate-800">Couldn’t load the dashboard</div>
        <p className="max-w-md text-sm text-slate-500">{message}</p>
        <button
          onClick={onRetry}
          className="mt-1 rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-slate-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-400"
        >
          Retry
        </button>
      </div>
    </Card>
  );
}

export function Skeleton({ className = '' }: { className?: string }) {
  return <div className={`animate-pulse rounded bg-slate-200/70 ${className}`} />;
}
