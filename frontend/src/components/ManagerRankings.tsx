import { useState } from 'react';
import type { ManagerRankRow, RankingsDto } from '../api/types';
import { changeDisplay, count, moneyCompact, percentFromFraction } from '../lib/format';
import { Card, CardHeader, Delta, EmptyState } from './ui';

type Mode = 'gp' | 'avg';

function Row({ row, mode }: { row: ManagerRankRow; mode: Mode }) {
  const value = mode === 'gp' ? moneyCompact(row.grossProfit) : row.averageCheck == null ? '—' : moneyCompact(row.averageCheck);
  const delta = mode === 'gp' ? row.grossProfitChangePercent : row.averageCheckChangePercent;
  return (
    <li className="flex items-center gap-3 px-5 py-2.5 transition-colors hover:bg-slate-50">
      <span className="w-5 shrink-0 text-right text-sm font-semibold text-slate-400 tabular">{row.rank}</span>
      <span
        className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-[11px] font-semibold text-white"
        style={{ backgroundColor: row.avatarColor }}
        aria-hidden
      >
        {row.initials}
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-1.5">
          <span className="truncate text-sm font-medium text-slate-800">{row.name}</span>
          {!row.active && <span className="rounded bg-slate-100 px-1 text-[10px] font-medium text-slate-500">inactive</span>}
        </div>
        <div className="text-xs text-slate-400 tabular">
          {count(row.paidSales)} sales · {percentFromFraction(row.margin)} margin
        </div>
      </div>
      <div className="shrink-0 text-right">
        <div className="text-sm font-semibold text-slate-900 tabular">{value}</div>
        <Delta display={changeDisplay(delta)} />
      </div>
    </li>
  );
}

export function ManagerRankings({ rankings }: { rankings: RankingsDto }) {
  const [mode, setMode] = useState<Mode>('gp');
  const rows = mode === 'gp' ? rankings.grossProfit : rankings.averageCheck;

  const toggle = (
    <div className="inline-flex rounded-lg bg-slate-100 p-0.5 text-xs font-medium" role="tablist" aria-label="Ranking metric">
      {(['gp', 'avg'] as const).map((m) => (
        <button
          key={m}
          role="tab"
          aria-selected={mode === m}
          onClick={() => setMode(m)}
          className={`rounded-md px-2.5 py-1 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-slate-400 ${
            mode === m ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'
          }`}
        >
          {m === 'gp' ? 'Gross Profit' : 'Avg Check'}
        </button>
      ))}
    </div>
  );

  return (
    <Card className="flex h-full flex-col">
      <CardHeader title="Manager ranking" action={toggle} />
      {rows.length === 0 ? (
        <EmptyState message="No ranked managers in this period" />
      ) : (
        <ol className="max-h-[560px] flex-1 divide-y divide-slate-100 overflow-y-auto">
          {rows.map((r) => (
            <Row key={r.managerId} row={r} mode={mode} />
          ))}
        </ol>
      )}
    </Card>
  );
}
