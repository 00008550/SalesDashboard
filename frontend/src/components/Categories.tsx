import type { CategorySlice } from '../api/types';
import { moneyCompact, percentFromFraction } from '../lib/format';
import { Card, CardHeader, EmptyState } from './ui';

export function Categories({ categories }: { categories: CategorySlice[] }) {
  return (
    <Card className="flex h-full flex-col">
      <CardHeader title="Sales by category" subtitle="Revenue share (paid)" />
      {categories.length === 0 ? (
        <EmptyState message="No category sales in this period" />
      ) : (
        <ul className="flex-1 space-y-3 px-5 py-4">
          {categories.map((c) => (
            <li key={c.categoryId}>
              <div className="flex items-baseline justify-between gap-2 text-sm">
                <span className="truncate font-medium text-slate-700">{c.name}</span>
                <span className="shrink-0 text-slate-900 tabular">{moneyCompact(c.revenue)}</span>
              </div>
              <div className="mt-1 flex items-center gap-2">
                <div className="h-2 flex-1 overflow-hidden rounded-full bg-slate-100">
                  <div
                    className="h-full rounded-full bg-blue-500 transition-[width] duration-500 ease-out"
                    style={{ width: `${Math.max(2, c.share * 100)}%` }}
                  />
                </div>
                <span className="w-10 shrink-0 text-right text-xs text-slate-500 tabular">{percentFromFraction(c.share)}</span>
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
