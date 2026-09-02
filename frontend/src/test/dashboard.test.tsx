import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Dashboard } from '../components/Dashboard';
import type { DashboardResponse, ManagerRankRow } from '../api/types';

function mgr(name: string, over: Partial<ManagerRankRow> = {}): ManagerRankRow {
  return {
    rank: 1, managerId: name, name, initials: name.slice(0, 2), avatarColor: '#000', active: true,
    paidSales: 10, revenue: 1000, grossProfit: 400, averageCheck: 100, margin: 0.4,
    grossProfitPrevious: 300, grossProfitChangePercent: 0.33, averageCheckPrevious: 90, averageCheckChangePercent: 0.11,
    ...over,
  };
}

function makeResponse(over: Partial<DashboardResponse> = {}): DashboardResponse {
  return {
    period: {
      preset: 'last30',
      current: { start: '2026-08-03T21:00:00Z', end: '2026-09-02T06:00:00Z' },
      previous: { start: '2026-07-04T21:00:00Z', end: '2026-08-03T06:00:00Z' },
      timezone: 'UTC+03:00 (MSK)', granularity: 'day',
    },
    summary: {
      revenue: { current: 1000, previous: 800, changePercent: 0.25 },
      cost: { current: 600, previous: 450, changePercent: 0.33 },
      grossProfit: { current: 400, previous: 350, changePercent: 0.14 },
      margin: { current: 0.4, previous: 0.43, deltaPp: -3 },
      paidSales: { current: 20, previous: 16, changePercent: 0.25 },
      averageCheck: { current: 50, previous: 50, changePercent: 0 },
      bestManager: { managerId: 'GP Leader', name: 'GP Leader', initials: 'GP', avatarColor: '#111', grossProfit: 400 },
    },
    rankings: { grossProfit: [mgr('GP Leader')], averageCheck: [mgr('AC Leader')] },
    trend: [{ bucketStart: '2026-08-03T21:00:00Z', revenue: 1000, grossProfit: 400, paidSales: 20 }],
    categories: [{ categoryId: 'c1', name: 'Drones', revenue: 1000, grossProfit: 400, share: 1 }],
    topProducts: [{ productId: 'p1', name: 'Drone One', category: 'Drones', revenue: 1000, grossProfit: 400, unitsSold: 5 }],
    recentSales: [{ saleId: 's1', occurredAt: '2026-09-01T10:00:00Z', manager: 'GP Leader', customer: 'Contact', company: 'Acme', products: 'Drone One', itemCount: 1, status: 'Paid', amount: 1000, cost: 600, grossProfit: 400 }],
    ...over,
  };
}

function renderDashboard() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: 1, retryDelay: 0, gcTime: 0 } } });
  return render(
    <QueryClientProvider client={qc}>
      <Dashboard />
    </QueryClientProvider>,
  );
}

const fetchMock = vi.fn();

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
});
afterEach(() => vi.unstubAllGlobals());

function respondWith(body: DashboardResponse) {
  fetchMock.mockResolvedValue({ ok: true, status: 200, json: async () => body } as Response);
}

describe('Dashboard', () => {
  it('shows a loading state first, then renders the data', async () => {
    // A fetch that never resolves keeps the dashboard in its loading state.
    fetchMock.mockReturnValue(new Promise(() => {}));
    renderDashboard();
    // Skeleton phase: the KPI label "Revenue" is not rendered yet.
    expect(screen.queryByText('Revenue')).toBeNull();
  });

  const rankingSection = () => screen.getByText('Manager ranking').closest('section')!;

  it('renders KPIs and blocks once data loads', async () => {
    respondWith(makeResponse());
    renderDashboard();
    expect(await screen.findByText('Revenue')).toBeInTheDocument();
    expect(screen.getByText('Manager ranking')).toBeInTheDocument();
    expect(within(rankingSection()).getByText('GP Leader')).toBeInTheDocument();
    const rankingList = within(rankingSection()).getByRole('list', { name: /manager ranking by gross profit \(scrollable\)/i });
    expect(rankingList).toHaveAttribute('tabindex', '0');
    expect(rankingList).toHaveClass('overflow-y-auto');
  });

  it('names the trend visualization and exposes its exact values in an accessible table', async () => {
    respondWith(makeResponse());
    renderDashboard();
    await screen.findByText('Revenue');

    const chart = screen.getByRole('img', {
      name: /sales trend chart for revenue, gross profit, and paid sales/i,
    });
    expect(chart).toHaveAttribute('tabindex', '0');
    const descriptionId = chart.getAttribute('aria-describedby');
    expect(descriptionId).not.toBeNull();
    expect(document.getElementById(descriptionId!)).toHaveTextContent(/1 day bucket/i);

    const table = screen.getByRole('table', { name: /sales trend exact values/i });
    expect(within(table).getByRole('columnheader', { name: 'Revenue amount' })).toBeInTheDocument();
    expect(within(table).getByRole('columnheader', { name: 'Gross profit' })).toBeInTheDocument();
    expect(within(table).getByText('$1,000.00')).toBeInTheDocument();
    expect(within(table).getByText('20')).toBeInTheDocument();
  });

  it('switches the server-ranked collection when the ranking mode is toggled', async () => {
    respondWith(makeResponse());
    renderDashboard();
    await screen.findByText('Revenue');
    expect(within(rankingSection()).getByText('GP Leader')).toBeInTheDocument();
    expect(within(rankingSection()).queryByText('AC Leader')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: /avg check/i }));

    expect(await within(rankingSection()).findByText('AC Leader')).toBeInTheDocument();
    expect(within(rankingSection()).queryByText('GP Leader')).toBeNull();
  });

  it('refetches with the new preset when the period changes', async () => {
    respondWith(makeResponse());
    renderDashboard();
    await screen.findByText('Revenue');

    await userEvent.click(screen.getByRole('button', { name: 'Today' }));

    await waitFor(() => {
      const urls = fetchMock.mock.calls.map((c) => String(c[0]));
      expect(urls.some((u) => u.includes('preset=today'))).toBe(true);
    });
  });

  it('marks an inverted custom range invalid and makes no request for it', async () => {
    respondWith(makeResponse());
    renderDashboard();
    await screen.findByText('Revenue');
    const callsBeforeEditing = fetchMock.mock.calls.length;
    const from = screen.getByLabelText('From date');
    const to = screen.getByLabelText('To date');

    fireEvent.change(from, { target: { value: '2026-09-02' } });
    fireEvent.change(to, { target: { value: '2026-09-01' } });

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('“From” must be on or before “To”.');
    expect(from).toHaveAttribute('aria-invalid', 'true');
    expect(to).toHaveAttribute('aria-invalid', 'true');
    expect(from).toHaveAttribute('aria-describedby', alert.id);
    expect(to).toHaveAttribute('aria-describedby', alert.id);
    expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled();
    expect(fetchMock).toHaveBeenCalledTimes(callsBeforeEditing);
  });

  it('rejects a 732-day custom range client-side but accepts the 731-day boundary', async () => {
    respondWith(makeResponse());
    renderDashboard();
    await screen.findByText('Revenue');
    const callsBeforeEditing = fetchMock.mock.calls.length;
    const from = screen.getByLabelText('From date');
    const to = screen.getByLabelText('To date');
    const apply = screen.getByRole('button', { name: 'Apply' });

    fireEvent.change(from, { target: { value: '2024-01-01' } });
    fireEvent.change(to, { target: { value: '2026-01-01' } });

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Date ranges can include at most 731 days.');
    expect(from).toHaveAttribute('aria-invalid', 'true');
    expect(to).toHaveAttribute('aria-describedby', alert.id);
    expect(apply).toBeDisabled();
    expect(fetchMock).toHaveBeenCalledTimes(callsBeforeEditing);

    // 2024 is a leap year: 2024-01-01 through 2025-12-31 is exactly 731 inclusive days.
    fireEvent.change(to, { target: { value: '2025-12-31' } });
    expect(screen.queryByRole('alert')).toBeNull();
    expect(from).not.toHaveAttribute('aria-invalid');
    expect(to).not.toHaveAttribute('aria-describedby');
    expect(apply).toBeEnabled();

    await userEvent.click(apply);
    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([url]) => String(url).includes('from=2024-01-01&to=2025-12-31'))).toBe(true);
    });
  });

  it('does not automatically retry a non-transient 4xx response', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => ({ detail: 'The range is invalid.' }),
    } as Response);

    renderDashboard();

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('The range is invalid.')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('recovers when Retry is clicked: it issues a fresh request and renders the data', async () => {
    const failure = { ok: false, status: 500, json: async () => ({ detail: 'Boom' }) } as Response;
    // Initial load makes two attempts (retry: 1); both fail. Everything after recovers.
    fetchMock
      .mockResolvedValueOnce(failure)
      .mockResolvedValueOnce(failure)
      .mockResolvedValue({ ok: true, status: 200, json: async () => makeResponse() } as Response);

    renderDashboard();

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText(/couldn.t load the dashboard/i)).toBeInTheDocument();
    expect(within(alert).getByText('Boom')).toBeInTheDocument();
    const callsBeforeRetry = fetchMock.mock.calls.length;
    expect(callsBeforeRetry).toBe(2);

    await userEvent.click(screen.getByRole('button', { name: 'Retry' }));

    // Recovery: the data now renders and the error card is gone.
    expect(await screen.findByText('Revenue')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();
    // Retry issued at least one more request than the failed initial load.
    expect(fetchMock.mock.calls.length).toBeGreaterThan(callsBeforeRetry);
  });

  it('keeps the previous data and shows Updating while a period change is in flight', async () => {
    // First load resolves immediately; the second request (after a period change) is deferred so we
    // can inspect the in-flight state.
    const firstLoad = makeResponse({
      summary: { ...makeResponse().summary, revenue: { current: 1_234_000, previous: 800, changePercent: 0.25 } },
    });
    let resolveSecond: (r: Response) => void = () => {};
    fetchMock
      .mockResolvedValueOnce({ ok: true, status: 200, json: async () => firstLoad } as Response)
      .mockImplementationOnce(() => new Promise<Response>((resolve) => { resolveSecond = resolve; }));

    renderDashboard();
    await screen.findByText('Revenue');
    expect(screen.getByText('$1.23M')).toBeInTheDocument(); // current revenue KPI from the first load

    await userEvent.click(screen.getByRole('button', { name: 'Today' }));

    // While the second request is pending: prior data is retained, Updating is announced, and the
    // content region is marked busy.
    const updating = await screen.findByText(/updating/i);
    expect(updating).toBeInTheDocument();
    expect(screen.getByText('$1.23M')).toBeInTheDocument(); // still the old snapshot
    expect(document.querySelector('[aria-busy="true"]')).not.toBeNull();

    // Complete the second request with new figures.
    resolveSecond({
      ok: true,
      status: 200,
      json: async () => makeResponse({
        summary: { ...makeResponse().summary, revenue: { current: 5_678_000, previous: 800, changePercent: 1.5 } },
      }),
    } as Response);

    // Updating clears and the busy flag is removed once the request completes.
    await waitFor(() => expect(screen.queryByText(/updating/i)).toBeNull());
    expect(document.querySelector('[aria-busy="true"]')).toBeNull();
    expect(screen.getByText('$5.68M')).toBeInTheDocument();
  });

  it('shows an empty-period banner when there are no paid sales', async () => {
    respondWith(makeResponse({
      summary: { ...makeResponse().summary, paidSales: { current: 0, previous: 0, changePercent: null } },
      rankings: { grossProfit: [], averageCheck: [] },
      categories: [], topProducts: [], recentSales: [],
    }));
    renderDashboard();
    expect(await screen.findByText(/no paid sales in the selected period/i)).toBeInTheDocument();
    // Block-level empty states render too.
    expect(within(screen.getByText('Manager ranking').closest('section')!).getByText(/no ranked managers/i)).toBeInTheDocument();
  });
});
