import { Area, CartesianGrid, ComposedChart, Legend, Line, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type { TrendPoint } from '../api/types';
import { count, dateTime, money, moneyCompact, shortDate } from '../lib/format';
import { Card, CardHeader, EmptyState } from './ui';

export function TrendChart({ data, granularity }: { data: TrendPoint[]; granularity: string }) {
  const hasData = data.some((d) => d.paidSales > 0 || d.revenue > 0);
  const tick = (iso: string) => (granularity === 'hour' ? dateTime(iso) : shortDate(iso));

  return (
    <Card className="h-full">
      <CardHeader title="Sales trend" subtitle="Revenue, gross profit and paid sales over time" />
      <div className="px-2 py-4">
        {!hasData ? (
          <EmptyState message="No paid sales in this period" />
        ) : (
          <ResponsiveContainer width="100%" height={288}>
            <ComposedChart data={data} margin={{ top: 8, right: 12, bottom: 4, left: 4 }}>
              <defs>
                <linearGradient id="rev" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#2563eb" stopOpacity={0.25} />
                  <stop offset="100%" stopColor="#2563eb" stopOpacity={0} />
                </linearGradient>
                <linearGradient id="gp" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#10b981" stopOpacity={0.25} />
                  <stop offset="100%" stopColor="#10b981" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="#eef2f7" vertical={false} />
              <XAxis dataKey="bucketStart" tickFormatter={tick} tick={{ fontSize: 11, fill: '#64748b' }} tickLine={false} axisLine={{ stroke: '#e2e8f0' }} minTickGap={24} />
              <YAxis yAxisId="money" tickFormatter={(v) => moneyCompact(v as number)} tick={{ fontSize: 11, fill: '#64748b' }} tickLine={false} axisLine={false} width={64} />
              <YAxis yAxisId="count" orientation="right" tick={{ fontSize: 11, fill: '#64748b' }} tickLine={false} axisLine={false} width={36} allowDecimals={false} />
              <Tooltip
                labelFormatter={(l) => (granularity === 'hour' ? dateTime(l as string) : shortDate(l as string))}
                formatter={(value, name) =>
                  name === 'Paid sales'
                    ? [count(value as number), name]
                    : [money(value as number), name]
                }
                contentStyle={{ borderRadius: 8, border: '1px solid #e2e8f0', fontSize: 12 }}
              />
              <Legend wrapperStyle={{ fontSize: 12 }} />
              <Area yAxisId="money" type="monotone" dataKey="revenue" name="Revenue" stroke="#2563eb" strokeWidth={2} fill="url(#rev)" />
              <Area yAxisId="money" type="monotone" dataKey="grossProfit" name="Gross profit" stroke="#10b981" strokeWidth={2} fill="url(#gp)" />
              <Line yAxisId="count" type="monotone" dataKey="paidSales" name="Paid sales" stroke="#f59e0b" strokeWidth={2} dot={false} />
            </ComposedChart>
          </ResponsiveContainer>
        )}
      </div>
    </Card>
  );
}
