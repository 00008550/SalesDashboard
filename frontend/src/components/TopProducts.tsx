import type { ProductRow } from '../api/types';
import { count, moneyCompact } from '../lib/format';
import { Card, CardHeader, EmptyState } from './ui';

export function TopProducts({ products }: { products: ProductRow[] }) {
  return (
    <Card className="flex h-full flex-col">
      <CardHeader title="Top products" subtitle="By revenue (paid)" />
      {products.length === 0 ? (
        <EmptyState message="No product sales in this period" />
      ) : (
        <ul className="flex-1 divide-y divide-slate-100">
          {products.map((p, i) => (
            <li key={p.productId} className="flex items-center gap-3 px-5 py-2.5">
              <span className="w-5 shrink-0 text-right text-sm font-semibold text-slate-400 tabular">{i + 1}</span>
              <div className="min-w-0 flex-1">
                <div className="truncate text-sm font-medium text-slate-800">{p.name}</div>
                <div className="truncate text-xs text-slate-400">
                  {p.category} · {count(p.unitsSold)} units
                </div>
              </div>
              <div className="shrink-0 text-right">
                <div className="text-sm font-semibold text-slate-900 tabular">{moneyCompact(p.revenue)}</div>
                <div className="text-xs text-slate-400 tabular">{moneyCompact(p.grossProfit)} GP</div>
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
