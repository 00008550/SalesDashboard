// Mirrors the backend DTOs in SalesDashboard.Contracts.Dashboard (System.Text.Json web defaults =
// camelCase). Money is a JSON number; ratios/averages/deltas are null when undefined (never NaN).

export interface PeriodWindow { start: string; end: string }
export interface PeriodDto { preset: string; current: PeriodWindow; previous: PeriodWindow; timezone: string; granularity: string }

export interface MoneyKpi { current: number; previous: number; changePercent: number | null }
export interface CountKpi { current: number; previous: number; changePercent: number | null }
export interface AverageCheckKpi { current: number | null; previous: number | null; changePercent: number | null }
/** Margin as a fraction; deltaPp is already in percentage points (do not multiply again). */
export interface MarginKpi { current: number | null; previous: number | null; deltaPp: number | null }

export interface BestManagerDto { managerId: string; name: string; initials: string; avatarColor: string; grossProfit: number }

export interface SummaryDto {
  revenue: MoneyKpi;
  cost: MoneyKpi;
  grossProfit: MoneyKpi;
  margin: MarginKpi;
  paidSales: CountKpi;
  averageCheck: AverageCheckKpi;
  bestManager: BestManagerDto | null;
}

export interface ManagerRankRow {
  rank: number;
  managerId: string;
  name: string;
  initials: string;
  avatarColor: string;
  active: boolean;
  paidSales: number;
  revenue: number;
  grossProfit: number;
  averageCheck: number | null;
  margin: number | null;
  grossProfitPrevious: number;
  grossProfitChangePercent: number | null;
  averageCheckPrevious: number | null;
  averageCheckChangePercent: number | null;
}

export interface RankingsDto { grossProfit: ManagerRankRow[]; averageCheck: ManagerRankRow[] }

export interface TrendPoint { bucketStart: string; revenue: number; grossProfit: number; paidSales: number }
export interface CategorySlice { categoryId: string; name: string; revenue: number; grossProfit: number; share: number }
export interface ProductRow { productId: string; name: string; category: string; revenue: number; grossProfit: number; unitsSold: number }
export interface RecentSaleRow {
  saleId: string;
  occurredAt: string;
  manager: string;
  customer: string;
  company: string;
  products: string;
  itemCount: number;
  status: string;
  amount: number;
  cost: number;
  grossProfit: number;
}

export interface DashboardResponse {
  period: PeriodDto;
  summary: SummaryDto;
  rankings: RankingsDto;
  trend: TrendPoint[];
  categories: CategorySlice[];
  topProducts: ProductRow[];
  recentSales: RecentSaleRow[];
}
