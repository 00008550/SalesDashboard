import { afterEach, describe, expect, it, vi } from 'vitest';
import { DashboardApiError, fetchDashboard } from '../api/client';
import { shouldRetryDashboardRequest } from '../hooks/useDashboard';
import { DEFAULT_PERIOD } from '../state/period';

afterEach(() => vi.unstubAllGlobals());

describe('dashboard API failures', () => {
  it('preserves the HTTP status and ProblemDetails message', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      status: 422,
      json: async () => ({ detail: 'That period cannot be processed.' }),
    } as Response));

    const error = await fetchDashboard(DEFAULT_PERIOD).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(DashboardApiError);
    expect(error).toMatchObject({
      name: 'DashboardApiError',
      message: 'That period cannot be processed.',
      status: 422,
    });
  });

  it('retries transient failures once but never retries an ordinary 4xx', () => {
    expect(shouldRetryDashboardRequest(0, new DashboardApiError('bad request', 400))).toBe(false);
    expect(shouldRetryDashboardRequest(0, new DashboardApiError('busy', 429))).toBe(true);
    expect(shouldRetryDashboardRequest(1, new DashboardApiError('busy', 429))).toBe(false);
    expect(shouldRetryDashboardRequest(0, new DashboardApiError('server error', 500))).toBe(true);
    expect(shouldRetryDashboardRequest(1, new DashboardApiError('server error', 500))).toBe(false);
    expect(shouldRetryDashboardRequest(0, new TypeError('network failed'))).toBe(true);
  });
});
