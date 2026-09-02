import type { ReactNode } from 'react';
import type { SummaryDto } from '../api/types';
import { changeDisplay, count, money, moneyCompact, percentFromFraction, pointsDisplay } from '../lib/format';
import { Card, Delta } from './ui';

function Kpi({ label, value, delta, footer }: { label: string; value: string; delta?: ReactNode; footer?: string }) {
  return (
    <Card className="px-5 py-4">
      <div className="flex items-start justify-between gap-2">
        <span className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</span>
        {delta}
      </div>
      <div className="mt-2 text-2xl font-semibold tracking-tight text-slate-900 tabular">{value}</div>
      {footer && <div className="mt-1 text-xs text-slate-500 tabular">{footer}</div>}
    </Card>
  );
}

export function KpiCards({ summary }: { summary: SummaryDto }) {
  const { revenue, grossProfit, margin, paidSales, averageCheck, bestManager } = summary;

  return (
    <div className="grid grid-cols-2 gap-4 md:grid-cols-3 xl:grid-cols-6">
      <Kpi
        label="Revenue"
        value={moneyCompact(revenue.current)}
        delta={<Delta display={changeDisplay(revenue.changePercent)} title={`Previous: ${money(revenue.previous)}`} />}
        footer={`prev ${moneyCompact(revenue.previous)}`}
      />
      <Kpi
        label="Gross Profit"
        value={moneyCompact(grossProfit.current)}
        delta={<Delta display={changeDisplay(grossProfit.changePercent)} title={`Previous: ${money(grossProfit.previous)}`} />}
        footer={`prev ${moneyCompact(grossProfit.previous)}`}
      />
      <Kpi
        label="Margin"
        value={percentFromFraction(margin.current)}
        delta={<Delta display={pointsDisplay(margin.deltaPp)} title="Change in percentage points" />}
        footer={`prev ${percentFromFraction(margin.previous)}`}
      />
      <Kpi
        label="Paid Sales"
        value={count(paidSales.current)}
        delta={<Delta display={changeDisplay(paidSales.changePercent)} title={`Previous: ${count(paidSales.previous)}`} />}
        footer={`prev ${count(paidSales.previous)}`}
      />
      <Kpi
        label="Average Check"
        value={averageCheck.current == null ? '—' : moneyCompact(averageCheck.current)}
        delta={<Delta display={changeDisplay(averageCheck.changePercent)} />}
        footer={averageCheck.previous == null ? 'prev —' : `prev ${moneyCompact(averageCheck.previous)}`}
      />
      <Card className="px-5 py-4">
        <span className="text-xs font-medium uppercase tracking-wide text-slate-500">Best Manager</span>
        {bestManager ? (
          <div className="mt-2 flex items-center gap-2.5">
            <span
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-semibold text-white"
              style={{ backgroundColor: bestManager.avatarColor }}
              aria-hidden
            >
              {bestManager.initials}
            </span>
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold text-slate-900">{bestManager.name}</div>
              <div className="text-xs text-slate-500 tabular">{moneyCompact(bestManager.grossProfit)} GP</div>
            </div>
          </div>
        ) : (
          <div className="mt-2 text-2xl font-semibold text-slate-500">—</div>
        )}
      </Card>
    </div>
  );
}
