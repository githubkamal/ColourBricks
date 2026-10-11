"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { commonExpenseSummary } from "@/features/common-expenses/api";
import { loanOutstandingSummary } from "@/features/loans/api";
import { reconciliationQueue } from "@/features/reconciliation/api";
import { useCurrentUser } from "@/features/shell/user-context";
import { listUsers } from "@/features/users/api";
import { dataState } from "@/components/ui/data-state";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { Skeleton } from "@/components/ui/skeleton";
import { StatTile, StatTileSkeleton } from "@/components/ui/stat-tile";
import { formatINR } from "@/lib/format";
import { hasPermission } from "@/lib/navigation";
import { usePagination } from "@/lib/use-pagination";
import { cn } from "@/lib/utils";
import { companyDashboard, type CompanyDashboard, type DashboardPeriod } from "./api";
import {
  ATTENTION,
  ChartCard,
  ChartEmpty,
  DonutChart,
  GroupedBars,
  NEGATIVE,
  POSITIVE,
  RankedBars,
  SERIES,
} from "./charts";
import { tileIcon } from "./tile-icon";
import { tileTone } from "./tile-tone";
import { TotalRevenueChart } from "./total-revenue-chart";

const COUNT_KEYS = new Set(["ongoingProjects", "completedProjects", "pendingReconciliation"]);
// "All time" first and by default (client request, 2026-10-11): the project
// dashboard is always all-time, so a company dashboard opening on "this month"
// read as disagreeing with its own projects.
const PERIODS: DashboardPeriod[] = ["Entire", "Weekly", "Monthly", "Yearly", "Custom"];
const PERIOD_LABELS: Partial<Record<DashboardPeriod, string>> = { Entire: "All time" };

function tileValue(data: CompanyDashboard, key: string): number {
  return data.tiles.find((t) => t.key === key)?.value ?? 0;
}

function PeriodToggle({
  value,
  onChange,
}: {
  value: DashboardPeriod;
  onChange: (p: DashboardPeriod) => void;
}) {
  return (
    <div className="inline-flex gap-1 rounded-full border p-1" role="group" aria-label="Period">
      {PERIODS.map((p) => (
        <button
          key={p}
          type="button"
          onClick={() => onChange(p)}
          aria-pressed={p === value}
          className={cn(
            "rounded-full px-3 py-1 text-xs font-medium transition-colors",
            p === value ? "bg-primary text-primary-foreground" : "hover:bg-secondary",
          )}
        >
          {PERIOD_LABELS[p] ?? p}
        </button>
      ))}
    </div>
  );
}

export function CompanyDashboardPage() {
  const user = useCurrentUser();
  const [period, setPeriod] = useState<DashboardPeriod>("Entire");
  const [customFrom, setCustomFrom] = useState("");
  const [customTo, setCustomTo] = useState("");
  const isCustom = period === "Custom";
  const { data, isLoading, isError } = useQuery({
    queryKey: [
      "company-dashboard",
      period,
      isCustom ? customFrom : null,
      isCustom ? customTo : null,
    ],
    queryFn: () => companyDashboard(period, customFrom || undefined, customTo || undefined),
    enabled: !isCustom || (customFrom !== "" && customTo !== ""),
  });
  const {
    pageRows: pagedProfitability,
    page: profitPage,
    setPage: setProfitPage,
    pageCount: profitPageCount,
    total: profitTotal,
  } = usePagination(data?.projectProfitability, 10);

  const header = (
    <div className="flex flex-wrap items-center justify-between gap-3">
      <PageHeader title="Company dashboard" />
      <div className="flex flex-wrap items-center gap-2">
        {isCustom && (
          <>
            <Input
              type="date"
              aria-label="From"
              value={customFrom}
              onChange={(e) => setCustomFrom(e.target.value)}
              className="h-8 w-36"
            />
            <span className="text-muted-foreground text-sm">to</span>
            <Input
              type="date"
              aria-label="To"
              value={customTo}
              onChange={(e) => setCustomTo(e.target.value)}
              className="h-8 w-36"
            />
          </>
        )}
        <PeriodToggle value={period} onChange={setPeriod} />
      </div>
    </div>
  );

  if (isCustom && (customFrom === "" || customTo === "")) {
    return (
      <div className="@container max-w-5xl space-y-6">
        {header}
        {dataState({
          isEmpty: true,
          emptyLabel: "Pick a start and end date to load the dashboard.",
        })}
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="@container max-w-5xl space-y-6">
        {header}
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {Array.from({ length: 6 }, (_, i) => (
            <StatTileSkeleton key={i} />
          ))}
        </div>
        <div className="grid gap-4 @4xl:grid-cols-3">
          <Skeleton className="h-80 w-full rounded-xl @4xl:col-span-2" />
          <Skeleton className="h-80 w-full rounded-xl" />
        </div>
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="@container max-w-5xl space-y-6">
        {header}
        {dataState({
          isError: true,
          errorLabel: "Could not load the dashboard.",
        })}
      </div>
    );
  }

  const vendorOut = tileValue(data, "vendorOutstanding");
  const subOut = tileValue(data, "subcontractorOutstanding");
  // Whatever Total Outstanding carries beyond the two named payables is custom
  // work and temple donations — the same split the project dashboard sums.
  const otherOut = Math.max(0, tileValue(data, "totalOutstanding") - vendorOut - subOut);
  const outstandingByType = [
    { name: "Vendors", value: vendorOut },
    { name: "Subcontractors", value: subOut },
    { name: "Custom work & donations", value: otherOut },
  ].filter((d) => d.value > 0);
  const profitByProject = [...data.projectProfitability]
    .filter((r) => r.profit !== 0)
    .sort((a, b) => b.profit - a.profit)
    .map((r) => ({ name: r.projectName, value: r.profit }));
  const incomeVsCost = data.projectProfitability.filter(
    (r) => r.revenue !== 0 || r.actualCost !== 0,
  );
  const barHeight = (n: number) => (n > 6 ? "h-80" : "h-64");

  return (
    <div className="@container max-w-5xl space-y-6">
      {header}

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3" data-testid="company-tiles">
        {data.tiles.map((t) => (
          <StatTile
            key={t.key}
            label={t.label}
            value={COUNT_KEYS.has(t.key) ? String(t.value) : formatINR(t.value)}
            tone={tileTone(t.key, t.value)}
            icon={tileIcon(t.key)}
          />
        ))}
      </div>

      <div className="grid gap-4 @4xl:grid-cols-3">
        <div className="min-w-0 @4xl:col-span-2">
          {data.monthlyFlow.length > 0 ? (
            <TotalRevenueChart data={data.monthlyFlow} />
          ) : (
            <ChartCard title="Income vs expenses" bodyClassName="h-72">
              <ChartEmpty />
            </ChartCard>
          )}
        </div>
        <ChartCard title="Outstanding by type" subtitle="Still to be paid" bodyClassName="h-auto">
          {outstandingByType.length > 0 ? (
            <DonutChart rows={outstandingByType} centerLabel="outstanding" />
          ) : (
            <div className="h-64">
              <ChartEmpty label="Nothing outstanding." />
            </div>
          )}
        </ChartCard>
      </div>

      <ProfitabilityTable data={data} rows={pagedProfitability} />

      <PaginationBar
        page={profitPage}
        pageCount={profitPageCount}
        total={profitTotal}
        onPageChange={setProfitPage}
        itemLabel="projects"
      />

      <div className="grid gap-4 @3xl:grid-cols-2">
        <ChartCard
          title="Income vs cost by project"
          subtitle="For the selected period"
          className="@3xl:col-span-2"
          bodyClassName="h-72"
        >
          {incomeVsCost.length > 0 ? (
            <GroupedBars
              data={incomeVsCost}
              categoryKey="projectName"
              series={[
                { key: "revenue", name: "Income", color: POSITIVE },
                { key: "actualCost", name: "Cost", color: NEGATIVE },
              ]}
            />
          ) : (
            <ChartEmpty />
          )}
        </ChartCard>

        <ChartCard
          title="Profit by project"
          subtitle="Income minus cost, selected period"
          bodyClassName={barHeight(profitByProject.length)}
        >
          {profitByProject.length > 0 ? (
            <RankedBars
              rows={profitByProject}
              name="Profit"
              colorFor={(r) => (r.value < 0 ? NEGATIVE : POSITIVE)}
            />
          ) : (
            <ChartEmpty />
          )}
        </ChartCard>

        {hasPermission(user.permissions, "users.view") && <UsersByRoleChart />}
        {hasPermission(user.permissions, "loans.view") && <LoansOutstandingChart />}
        {hasPermission(user.permissions, "common_expenses.view") && <CommonExpenseChart />}
        {hasPermission(user.permissions, "bank_reconciliation.view") && (
          <ReconciliationStatusChart />
        )}
      </div>
    </div>
  );
}

/**
 * Per-project income/cost/profit, with a footer that reconciles the rows to the
 * tiles above: the project rows sum to what each project dashboard shows, and
 * company-level spend not yet allocated to any project makes up the rest of
 * "Overall Expenses" (client request, 2026-10-11 — the two used to look unequal
 * with nothing on screen to say why).
 */
function ProfitabilityTable({
  data,
  rows,
}: {
  data: CompanyDashboard;
  rows: CompanyDashboard["projectProfitability"];
}) {
  const all = data.projectProfitability;
  const revenue = all.reduce((s, r) => s + r.revenue, 0);
  const cost = all.reduce((s, r) => s + r.actualCost, 0);
  const overallExpenses = tileValue(data, "expenses");
  const unallocated = overallExpenses - cost;
  const profitCell = (v: number) =>
    v < 0 ? "text-negative p-2 tabular-nums" : "text-positive p-2 tabular-nums";

  return (
    <div
      data-table-scroll
      className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs"
    >
      <table className="w-full text-sm">
        <thead className="bg-secondary/60 text-muted-foreground">
          <tr className="border-b text-left">
            <th className="p-2 font-medium">Project</th>
            <th className="p-2 font-medium">Revenue</th>
            <th className="p-2 font-medium">Actual cost</th>
            <th className="p-2 font-medium">Profit</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.projectId} className="border-b last:border-0">
              <td className="p-2">{r.projectName}</td>
              <td className="p-2 tabular-nums">{formatINR(r.revenue)}</td>
              <td className="p-2 tabular-nums">{formatINR(r.actualCost)}</td>
              <td className={profitCell(r.profit)}>{formatINR(r.profit)}</td>
            </tr>
          ))}
        </tbody>
        {all.length > 0 && (
          <tfoot className="border-t">
            <tr className="font-medium">
              <td className="p-2">All projects</td>
              <td className="p-2 tabular-nums">{formatINR(revenue)}</td>
              <td className="p-2 tabular-nums">{formatINR(cost)}</td>
              <td className={profitCell(revenue - cost)}>{formatINR(revenue - cost)}</td>
            </tr>
            {Math.abs(unallocated) >= 0.01 && (
              <>
                <tr className="text-muted-foreground">
                  <td className="p-2">Company expenses not allocated to a project</td>
                  <td className="p-2" />
                  <td className="p-2 tabular-nums">{formatINR(unallocated)}</td>
                  <td className="text-negative p-2 tabular-nums">{formatINR(-unallocated)}</td>
                </tr>
                <tr className="border-t font-semibold" data-testid="profitability-overall">
                  <td className="p-2">Overall</td>
                  <td className="p-2 tabular-nums">{formatINR(tileValue(data, "income"))}</td>
                  <td className="p-2 tabular-nums">{formatINR(overallExpenses)}</td>
                  <td className={profitCell(tileValue(data, "profitLoss"))}>
                    {formatINR(tileValue(data, "profitLoss"))}
                  </td>
                </tr>
              </>
            )}
          </tfoot>
        )}
      </table>
    </div>
  );
}

/** Admin-only (client request, 2026-09-07): how the user base splits across roles. */
function UsersByRoleChart() {
  const { data: users } = useQuery({
    queryKey: ["users", { includeInactive: false, forDashboard: true }],
    queryFn: () => listUsers(false),
  });

  if (!users || users.length === 0) return null;

  const byRole = new Map<string, number>();
  for (const u of users) {
    const label = u.roleName ?? "No role";
    byRole.set(label, (byRole.get(label) ?? 0) + 1);
  }
  const rows = [...byRole.entries()].map(([name, value]) => ({ name, value }));

  return (
    <ChartCard title="Active users by role" bodyClassName="h-auto">
      <DonutChart
        rows={rows}
        centerLabel="users"
        format={(v) => String(v)}
        tooltipFormat={(v) => String(v)}
      />
    </ChartCard>
  );
}

/** Visible to anyone with loans.view — permission-driven, not role-hardcoded. */
function LoansOutstandingChart() {
  const { data } = useQuery({
    queryKey: ["loan-outstanding-summary", { forDashboard: true }],
    queryFn: loanOutstandingSummary,
  });

  const rows = (data?.byProject ?? [])
    .filter((r) => r.principalOutstanding > 0)
    .map((r) => ({ name: r.projectName, value: r.principalOutstanding }))
    .sort((a, b) => b.value - a.value);
  if (rows.length === 0) return null;

  return (
    <ChartCard
      title="Loan principal outstanding"
      subtitle="By project"
      bodyClassName={rows.length > 6 ? "h-80" : "h-64"}
    >
      <RankedBars rows={rows} name="Outstanding" color={SERIES[6]} />
    </ChartCard>
  );
}

function CommonExpenseChart() {
  const { data } = useQuery({
    queryKey: ["common-expense-summary", { forDashboard: true }],
    queryFn: () => commonExpenseSummary(),
  });

  const rows = [
    { name: "Personal", value: data?.personal ?? 0 },
    { name: "Office", value: data?.office ?? 0 },
    { name: "Savings", value: data?.savings ?? 0 },
  ].filter((r) => r.value > 0);
  if (rows.length === 0) return null;

  return (
    <ChartCard title="Personal / office / savings" bodyClassName="h-auto">
      <DonutChart rows={rows} centerLabel="total" />
    </ChartCard>
  );
}

function ReconciliationStatusChart() {
  const { data: pending } = useQuery({
    queryKey: ["reconciliation", { status: "Pending", forDashboard: true }],
    queryFn: () => reconciliationQueue({ status: "Pending", page: 1 }),
  });
  const { data: reconciled } = useQuery({
    queryKey: ["reconciliation", { status: "Reconciled", forDashboard: true }],
    queryFn: () => reconciliationQueue({ status: "Reconciled", page: 1 }),
  });

  const pendingCount = pending?.totalCount ?? 0;
  const reconciledCount = reconciled?.totalCount ?? 0;
  const total = pendingCount + reconciledCount;
  if (total === 0) return null;

  const pct = (n: number) => `${Math.round((n / total) * 100)}%`;

  return (
    <ChartCard title="Bank reconciliation" subtitle="Imported transactions" bodyClassName="h-auto">
      <div className="space-y-4 py-2">
        <div className="bg-muted flex h-3 w-full overflow-hidden rounded-full">
          <div style={{ width: pct(reconciledCount), background: POSITIVE }} />
          <div style={{ width: pct(pendingCount), background: ATTENTION }} />
        </div>
        <dl className="grid grid-cols-2 gap-3 text-sm">
          <div className="flex items-center gap-2">
            <span className="size-2.5 rounded-[3px]" style={{ background: POSITIVE }} />
            <dt className="text-muted-foreground">Reconciled</dt>
            <dd className="ml-auto font-semibold tabular-nums">{reconciledCount}</dd>
          </div>
          <div className="flex items-center gap-2">
            <span className="size-2.5 rounded-[3px]" style={{ background: ATTENTION }} />
            <dt className="text-muted-foreground">Pending</dt>
            <dd className="ml-auto font-semibold tabular-nums">{pendingCount}</dd>
          </div>
        </dl>
        <p className="text-muted-foreground text-xs">
          {pct(reconciledCount)} of imported bank lines are matched to a recorded entry.
        </p>
      </div>
    </ChartCard>
  );
}
