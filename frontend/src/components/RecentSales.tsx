import type { RecentSaleRow } from '../api/types';
import { count, dateTime, money } from '../lib/format';
import { Card, CardHeader, EmptyState, StatusBadge } from './ui';

export function RecentSales({ sales }: { sales: RecentSaleRow[] }) {
  return (
    <Card>
      <CardHeader
        title="Recent sales"
        subtitle="Original transaction amounts — Cancelled and Refunded rows do not net into the KPIs above"
      />
      {sales.length === 0 ? (
        <EmptyState message="No sales in this period" />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-100 text-left text-xs uppercase tracking-wide text-slate-400">
                <th className="px-5 py-2.5 font-medium">Date</th>
                <th className="px-3 py-2.5 font-medium">Manager</th>
                <th className="px-3 py-2.5 font-medium">Customer</th>
                <th className="px-3 py-2.5 font-medium">Products</th>
                <th className="px-3 py-2.5 font-medium">Status</th>
                <th className="px-3 py-2.5 text-right font-medium">Amount</th>
                <th className="px-5 py-2.5 text-right font-medium">Gross Profit</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-50">
              {sales.map((s) => (
                <tr key={s.saleId} className="transition-colors hover:bg-slate-50">
                  <td className="whitespace-nowrap px-5 py-2.5 text-slate-500 tabular">{dateTime(s.occurredAt)}</td>
                  <td className="whitespace-nowrap px-3 py-2.5 text-slate-700">{s.manager}</td>
                  <td className="px-3 py-2.5">
                    <div className="max-w-[180px] truncate text-slate-700">{s.company}</div>
                    <div className="max-w-[180px] truncate text-xs text-slate-400">{s.customer}</div>
                  </td>
                  <td className="px-3 py-2.5">
                    <div className="max-w-[220px] truncate text-slate-600" title={s.products}>
                      {s.products || '—'}
                    </div>
                    <div className="text-xs text-slate-400">{count(s.itemCount)} item{s.itemCount === 1 ? '' : 's'}</div>
                  </td>
                  <td className="px-3 py-2.5"><StatusBadge status={s.status} /></td>
                  <td className="whitespace-nowrap px-3 py-2.5 text-right font-medium text-slate-900 tabular">{money(s.amount)}</td>
                  <td className="whitespace-nowrap px-5 py-2.5 text-right text-slate-700 tabular">{money(s.grossProfit)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}
