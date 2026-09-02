import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { fetchDashboard } from '../api/client';
import { type Period, periodKey } from '../state/period';

/**
 * One query drives the whole dashboard, keyed by the period. keepPreviousData holds the last snapshot
 * visible while a new period loads, so the UI dims and shows "Updating…" instead of blanking.
 */
export function useDashboard(period: Period) {
  return useQuery({
    queryKey: ['dashboard', periodKey(period)],
    queryFn: ({ signal }) => fetchDashboard(period, signal),
    placeholderData: keepPreviousData,
    staleTime: 30_000,
    retry: 1,
  });
}
