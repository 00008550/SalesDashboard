import { useId } from 'react';
import { Area, CartesianGrid, ComposedChart, Legend, Line, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type { TrendPoint } from '../api/types';
import { count, dateTime, money, moneyCompact, shortDate } from '../lib/format';
import { Card, CardHeader, EmptyState } from './ui';

export function TrendChart({ data, granularity }: { data: TrendPoint[]; granularity: string }) {
  const descriptionId = useId();
  const hasData = data.some((d) => d.paidSales > 0 || d.revenue > 0);
  const tick = (iso: string) => (granularity === 'hour' ? dateTime(iso) : shortDate(iso));

  return (
    <Card className="h-full">
      <CardHeader title="Sales trend" subtitle="Revenue, gross profit and paid sales over time" />
      <div className="px-2 py-4">
        {!hasData ? (
          <EmptyState message="No paid sales in this period" />
        ) : (
          <>
            <p id={descriptionId} className="sr-only">
              {`Sales trend with ${data.length} ${granularity} ${data.length === 1 ? 'bucket' : 'buckets'}. Exact values are listed in the accessible table after the chart.`}
            </p>
            <div
              role="img"
              tabIndex={0}
              aria-label="Sales trend chart for revenue, gross profit, and paid sales"
              aria-describedby={descriptionId}
              className="rounded-lg focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-600"
            >
              <div aria-hidden="true">
                <ResponsiveContainer width="100%" height={288}>
                  <ComposedChart data={data} margin={{ top: 8, right: 12, bottom: 4, left: 4 }}>
                    {/* Stroke/legend colors all meet WCAG AA (>=4.5:1 on white) so legend text is readable
                        and the lines clear the 3:1 non-text minimum: revenue blue-700, gross profit
                        emerald-700, paid sales amber-700. */}
                    <defs>
                      <linearGradient id="rev" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor="#1d4ed8" stopOpacity={0.25} />
                        <stop offset="100%" stopColor="#1d4ed8" stopOpacity={0} />
                      </linearGradient>
                      <linearGradient id="gp" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor="#047857" stopOpacity={0.25} />
                        <stop offset="100%" stopColor="#047857" stopOpacity={0} />
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
                    <Area yAxisId="money" type="monotone" dataKey="revenue" name="Revenue" stroke="#1d4ed8" strokeWidth={2} fill="url(#rev)" />
                    <Area yAxisId="money" type="monotone" dataKey="grossProfit" name="Gross profit" stroke="#047857" strokeWidth={2} fill="url(#gp)" />
                    <Line yAxisId="count" type="monotone" dataKey="paidSales" name="Paid sales" stroke="#b45309" strokeWidth={2} dot={false} />
                  </ComposedChart>
                </ResponsiveContainer>
              </div>
            </div>
            <table className="sr-only">
              <caption>Sales trend exact values</caption>
              <thead>
                <tr>
                  <th scope="col">Bucket</th>
                  <th scope="col">Revenue amount</th>
                  <th scope="col">Gross profit</th>
                  <th scope="col">Paid sales</th>
                </tr>
              </thead>
              <tbody>
                {data.map((point) => (
                  <tr key={point.bucketStart}>
                    <th scope="row"><time dateTime={point.bucketStart}>{tick(point.bucketStart)}</time></th>
                    <td>{money(point.revenue)}</td>
                    <td>{money(point.grossProfit)}</td>
                    <td>{count(point.paidSales)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
      </div>
    </Card>
  );
}
