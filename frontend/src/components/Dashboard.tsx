import { useState } from 'react';
import { useDashboard } from '../hooks/useDashboard';
import { DEFAULT_PERIOD, type Period } from '../state/period';
import type { DashboardResponse } from '../api/types';
import { Header } from './Header';
import { KpiCards } from './KpiCards';
import { TrendChart } from './TrendChart';
import { ManagerRankings } from './ManagerRankings';
import { Categories } from './Categories';
import { TopProducts } from './TopProducts';
import { RecentSales } from './RecentSales';
import { Card, ErrorCard, Skeleton } from './ui';

export function Dashboard() {
  const [period, setPeriod] = useState<Period>(DEFAULT_PERIOD);
  const query = useDashboard(period);
  const updating = query.isFetching && !query.isLoading;

  return (
    <div className="min-h-screen">
      <Header period={period} onChange={setPeriod} updating={updating} timezone={query.data?.period.timezone} />
      <main className="mx-auto max-w-[1440px] px-6 py-6">
        {query.isError ? (
          <ErrorCard message={(query.error as Error).message} onRetry={() => query.refetch()} />
        ) : query.isLoading || !query.data ? (
          <DashboardSkeleton />
        ) : (
          <div className={updating ? 'opacity-60 transition-opacity duration-200' : 'transition-opacity duration-200'} aria-busy={updating}>
            <DashboardContent data={query.data} />
          </div>
        )}
      </main>
    </div>
  );
}

function DashboardContent({ data }: { data: DashboardResponse }) {
  const empty = data.summary.paidSales.current === 0;
  return (
    <div className="space-y-4">
      {empty && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-2.5 text-sm text-amber-800">
          No paid sales in the selected period. Figures below read zero; operational rows (if any) still appear in Recent sales.
        </div>
      )}
      <KpiCards summary={data.summary} />
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <TrendChart data={data.trend} granularity={data.period.granularity} />
        </div>
        <div className="lg:row-span-2">
          <ManagerRankings rankings={data.rankings} />
        </div>
        <Categories categories={data.categories} />
        <TopProducts products={data.topProducts} />
        <div className="lg:col-span-3">
          <RecentSales sales={data.recentSales} />
        </div>
      </div>
    </div>
  );
}

function DashboardSkeleton() {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4 md:grid-cols-3 xl:grid-cols-6">
        {Array.from({ length: 6 }).map((_, i) => (
          <Card key={i} className="px-5 py-4">
            <Skeleton className="h-3 w-20" />
            <Skeleton className="mt-3 h-7 w-28" />
            <Skeleton className="mt-2 h-3 w-16" />
          </Card>
        ))}
      </div>
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2 p-5"><Skeleton className="h-72 w-full" /></Card>
        <Card className="lg:row-span-2 p-5"><Skeleton className="h-[560px] w-full" /></Card>
        <Card className="p-5"><Skeleton className="h-40 w-full" /></Card>
        <Card className="p-5"><Skeleton className="h-40 w-full" /></Card>
        <Card className="lg:col-span-3 p-5"><Skeleton className="h-64 w-full" /></Card>
      </div>
    </div>
  );
}
