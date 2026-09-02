import type { DashboardResponse } from './types';
import { type Period, periodToQuery } from '../state/period';

/**
 * Fetches the single composed dashboard snapshot. On a non-2xx it reads the ProblemDetails body for
 * a human message so the UI can show something meaningful (not just a status code).
 */
export async function fetchDashboard(period: Period, signal?: AbortSignal): Promise<DashboardResponse> {
  const res = await fetch(`/api/dashboard?${periodToQuery(period)}`, { signal, headers: { Accept: 'application/json' } });
  if (!res.ok) {
    let detail = `The dashboard request failed (HTTP ${res.status}).`;
    try {
      const body = await res.json();
      if (body && typeof body.detail === 'string') detail = body.detail;
    } catch {
      /* non-JSON error body — keep the default message */
    }
    throw new Error(detail);
  }
  return (await res.json()) as DashboardResponse;
}
