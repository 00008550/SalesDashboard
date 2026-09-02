import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { DashboardApiError, fetchDashboard } from '../api/client';
import { type Period, periodKey } from '../state/period';

const TRANSIENT_CLIENT_STATUSES = new Set([408, 425, 429]);

export function shouldRetryDashboardRequest(failureCount: number, error: Error): boolean {
  if (error instanceof DashboardApiError && error.status >= 400 && error.status < 500) {
    return TRANSIENT_CLIENT_STATUSES.has(error.status) && failureCount < 1;
  }

  return failureCount < 1;
}

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
    retry: shouldRetryDashboardRequest,
  });
}
