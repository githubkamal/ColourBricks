"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import {
  Bar,
  BarChart,
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { commonExpenseSummary } from "@/features/common-expenses/api";
import { loanOutstandingSummary } from "@/features/loans/api";
import { reconciliationQueue } from "@/features/reconciliation/api";
import { useCurrentUser } from "@/features/shell/user-context";
import { listUsers } from "@/features/users/api";
import { dataState } from "@/components/ui/data-state";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { StatTile } from "@/components/ui/stat-tile";
import { formatINR } from "@/lib/format";
import { hasPermission } from "@/lib/navigation";
import { usePagination } from "@/lib/use-pagination";
import { cn } from "@/lib/utils";
import { companyDashboard, type CompanyDashboard, type DashboardPeriod } from "./api";
import { tileIcon } from "./tile-icon";
import { tileTone } from "./tile-tone";
import { TotalRevenueChart } from "./total-revenue-chart";

const COUNT_KEYS = new Set(["ongoingProjects", "completedProjects", "pendingReconciliation"]);
const PERIODS: DashboardPeriod[] = ["Weekly", "Monthly", "Yearly", "Custom"];

// Fixed, theme-independent series colours (same rationale as StatusBadge) so a
// chart's segments read consistently regardless of the accent picker.
const SERIES = ["#188ae2", "#10a75c", "#f0b429", "#e11d48", "#7c3aed", "#0d9488", "#ea580c"];
const POSITIVE = "#10a75c";
const NEGATIVE = "#ff5b5b";

// A premium "glass" tooltip — rounded, bordered, matches the card surface in
// both light and dark instead of recharts' plain white default.
const TOOLTIP_STYLE = {
  background: "var(--popover)",
  border: "1px solid var(--border)",
  borderRadius: "0.75rem",
  boxShadow: "var(--shadow-md)",
  fontSize: 12,
  padding: "8px 12px",
};
const TOOLTIP_LABEL_STYLE = { color: "var(--popover-foreground)", fontWeight: 600 };

function tileValue(data: CompanyDashboard, key: string): number {
  return data.tiles.find((t) => t.key === key)?.value ?? 0;
}

/**
 * Shared gradient defs (client request, 2026-09-07 — "premium design charts")
 * so every bar/pie fill reads as a soft vertical gradient instead of a flat
 * colour, without each chart re-declaring its own `<defs>`.
 */
function ChartGradients() {
  const stops: [string, string][] = [
    ["positive", POSITIVE],
    ["negative", NEGATIVE],
    ...SERIES.map((c, i): [string, string] => [`series${i}`, c]),
  ];
  return (
    <defs>
      {stops.map(([id, color]) => (
        <linearGradient key={id} id={`grad-${id}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity={0.95} />
          <stop offset="100%" stopColor={color} stopOpacity={0.55} />
        </linearGradient>
      ))}
    </defs>
  );
}

function ChartCard({
  title,
  right,
  children,
}: {
  title: string;
  right?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <div className="bg-card border-border rounded-xl border p-4 shadow-xs transition-shadow hover:shadow-md">
      <div className="mb-3 flex items-center justify-between gap-2">
        <p className="text-sm font-medium">{title}</p>
        {right}
      </div>
      <div className="h-60 w-full">{children}</div>
    </div>
  );
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
          className={cn(
            "rounded-full px-3 py-1 text-xs font-medium transition-colors",
            p === value ? "bg-primary text-primary-foreground" : "hover:bg-secondary",
          )}
        >
          {p}
        </button>
      ))}
    </div>
  );
}

export function CompanyDashboardPage() {
  const user = useCurrentUser();
  const [period, setPeriod] = useState<DashboardPeriod>("Monthly");
  const [customFrom, setCustomFrom] = useState("");
  const [customTo, setCustomTo] = useState("");
  const isCustom = period === "Custom";
  const { data, isLoading, isError } = useQuery({
    queryKey: ["company-dashboard", period, isCustom ? customFrom : null, isCustom ? customTo : null],
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
      <div className="max-w-5xl space-y-6">
        {header}
        {dataState({ isEmpty: true, emptyLabel: "Pick a start and end date to load the dashboard." })}
      </div>
    );
  }

  if (isLoading || isError || !data) {
    return (
      <div className="max-w-5xl space-y-6">
        {header}
        {dataState({
          isPending: isLoading,
          isError: isError || !data,
          errorLabel: "Could not load the dashboard.",
        })}
      </div>
    );
  }

  const outstandingByType = [
    { name: "Vendor", value: tileValue(data, "vendorOutstanding") },
    { name: "Subcontractor", value: tileValue(data, "subcontractorOutstanding") },
  ].filter((d) => d.value > 0);

  return (
    <div className="max-w-5xl space-y-6">
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

      <TotalRevenueChart data={data.monthlyFlow} />

      <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
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
            {pagedProfitability.map((r) => (
              <tr key={r.projectId} className="border-b last:border-0">
                <td className="p-2">{r.projectName}</td>
                <td className="p-2 tabular-nums">{formatINR(r.revenue)}</td>
                <td className="p-2 tabular-nums">{formatINR(r.actualCost)}</td>
                <td
                  className={
                    r.profit < 0
                      ? "text-negative p-2 tabular-nums"
                      : "text-positive p-2 tabular-nums"
                  }
                >
                  {formatINR(r.profit)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <PaginationBar
        page={profitPage}
        pageCount={profitPageCount}
        total={profitTotal}
        onPageChange={setProfitPage}
        itemLabel="projects"
      />

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        {data.projectProfitability.length > 0 && (
          <ChartCard title="Profit by project">
            <ResponsiveContainer>
              <BarChart data={data.projectProfitability} layout="vertical" margin={{ left: 24 }}>
                <ChartGradients />
                <XAxis type="number" tickFormatter={(v: number) => formatINR(v)} fontSize={11} />
                <YAxis
                  type="category"
                  dataKey="projectName"
                  width={100}
                  fontSize={11}
                  tick={{ fill: "var(--muted-foreground)" }}
                />
                <Tooltip
                  formatter={(v) => formatINR(Number(v))}
                  contentStyle={TOOLTIP_STYLE}
                  labelStyle={TOOLTIP_LABEL_STYLE}
                  cursor={{ fill: "var(--muted)" }}
                />
                <Bar dataKey="profit" name="Profit" radius={[0, 4, 4, 0]}>
                  {data.projectProfitability.map((r) => (
                    <Cell
                      key={r.projectId}
                      fill={r.profit < 0 ? "url(#grad-negative)" : "url(#grad-positive)"}
                    />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </ChartCard>
        )}

        {outstandingByType.length > 0 && (
          <ChartCard title="Outstanding by type">
            <ResponsiveContainer>
              <BarChart data={outstandingByType}>
                <ChartGradients />
                <XAxis dataKey="name" fontSize={12} tick={{ fill: "var(--muted-foreground)" }} />
                <YAxis tickFormatter={(v: number) => formatINR(v)} fontSize={11} width={70} />
                <Tooltip
                  formatter={(v) => formatINR(Number(v))}
                  contentStyle={TOOLTIP_STYLE}
                  labelStyle={TOOLTIP_LABEL_STYLE}
                  cursor={{ fill: "var(--muted)" }}
                />
                <Bar
                  dataKey="value"
                  name="Outstanding"
                  fill="url(#grad-series0)"
                  radius={[4, 4, 0, 0]}
                />
              </BarChart>
            </ResponsiveContainer>
          </ChartCard>
        )}

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
    <ChartCard title="Active users by role">
      <ResponsiveContainer>
        <PieChart>
          <ChartGradients />
          <Pie data={rows} dataKey="value" nameKey="name" outerRadius={80} label>
            {rows.map((r, i) => (
              <Cell key={r.name} fill={`url(#grad-series${i % SERIES.length})`} />
            ))}
          </Pie>
          <Tooltip contentStyle={TOOLTIP_STYLE} labelStyle={TOOLTIP_LABEL_STYLE} />
          <Legend wrapperStyle={{ fontSize: 12 }} />
        </PieChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}

/** Visible to anyone with loans.view — permission-driven, not role-hardcoded. */
function LoansOutstandingChart() {
  const { data } = useQuery({
    queryKey: ["loan-outstanding-summary", { forDashboard: true }],
    queryFn: loanOutstandingSummary,
  });

  const rows = (data?.byProject ?? []).filter((r) => r.principalOutstanding > 0);
  if (rows.length === 0) return null;

  return (
    <ChartCard title="Loan principal outstanding by project">
      <ResponsiveContainer>
        <BarChart data={rows}>
          <ChartGradients />
          <XAxis
            dataKey="projectName"
            fontSize={11}
            tick={{ fill: "var(--muted-foreground)" }}
            tickFormatter={(v: string) => (v.length > 10 ? `${v.slice(0, 10)}…` : v)}
          />
          <YAxis tickFormatter={(v: number) => formatINR(v)} fontSize={11} width={70} />
          <Tooltip
            formatter={(v) => formatINR(Number(v))}
            contentStyle={TOOLTIP_STYLE}
            labelStyle={TOOLTIP_LABEL_STYLE}
            cursor={{ fill: "var(--muted)" }}
          />
          <Bar
            dataKey="principalOutstanding"
            name="Outstanding"
            fill="url(#grad-series4)"
            radius={[4, 4, 0, 0]}
          />
        </BarChart>
      </ResponsiveContainer>
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
    <ChartCard title="Personal / office / savings">
      <ResponsiveContainer>
        <PieChart>
          <ChartGradients />
          <Pie data={rows} dataKey="value" nameKey="name" outerRadius={80} label>
            {rows.map((r, i) => (
              <Cell key={r.name} fill={`url(#grad-series${i % SERIES.length})`} />
            ))}
          </Pie>
          <Tooltip
            formatter={(v) => formatINR(Number(v))}
            contentStyle={TOOLTIP_STYLE}
            labelStyle={TOOLTIP_LABEL_STYLE}
          />
          <Legend wrapperStyle={{ fontSize: 12 }} />
        </PieChart>
      </ResponsiveContainer>
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

  const rows = [
    { name: "Pending", value: pending?.totalCount ?? 0 },
    { name: "Reconciled", value: reconciled?.totalCount ?? 0 },
  ].filter((r) => r.value > 0);
  if (rows.length === 0) return null;

  return (
    <ChartCard title="Bank reconciliation status">
      <ResponsiveContainer>
        <BarChart data={rows}>
          <ChartGradients />
          <XAxis dataKey="name" fontSize={12} tick={{ fill: "var(--muted-foreground)" }} />
          <YAxis allowDecimals={false} fontSize={11} width={40} />
          <Tooltip contentStyle={TOOLTIP_STYLE} labelStyle={TOOLTIP_LABEL_STYLE} />
          <Bar dataKey="value" name="Transactions" radius={[4, 4, 0, 0]}>
            {rows.map((r) => (
              <Cell
                key={r.name}
                fill={r.name === "Pending" ? "url(#grad-series2)" : "url(#grad-positive)"}
              />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </ChartCard>
  );
}
