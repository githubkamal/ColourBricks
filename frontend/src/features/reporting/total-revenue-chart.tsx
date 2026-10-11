"use client";

import { Scale, TrendingDown, TrendingUp } from "lucide-react";
import {
  Area,
  AreaChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { formatINR, formatINRCompact } from "@/lib/format";
import { cn } from "@/lib/utils";
import type { MonthlyFlow } from "./api";
import {
  AXIS_TICK,
  legendLabel,
  money,
  NEGATIVE,
  POSITIVE,
  TOOLTIP_ITEM_STYLE,
  TOOLTIP_LABEL_STYLE,
  TOOLTIP_STYLE,
} from "./charts";

const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

/** "2026-09" → "Sep 26". */
function monthLabel(key: string): string {
  const [y, m] = key.split("-");
  const name = MONTHS[Number(m) - 1];
  return name ? `${name} ${y.slice(2)}` : key;
}

/**
 * Income vs expenses, month by month — two soft-filled areas on one money axis,
 * with totals and the net underneath. Shared by the Company and Project dashboards,
 * which carry the same monthly-flow shape.
 */
export function TotalRevenueChart({ data }: { data: MonthlyFlow[] | undefined }) {
  if (!data || data.length === 0) return null;

  const totalIncome = data.reduce((s, m) => s + m.income, 0);
  const totalExpense = data.reduce((s, m) => s + m.expense, 0);
  const net = totalIncome - totalExpense;
  const rows = data.map((m) => ({ ...m, label: monthLabel(m.month) }));

  return (
    <section
      aria-label="Income vs expenses"
      className="bg-card border-border rounded-xl border shadow-xs"
    >
      <header className="border-border flex items-center justify-between border-b p-4">
        <div>
          <h2 className="text-sm font-semibold">Income vs expenses</h2>
          <p className="text-muted-foreground mt-0.5 text-xs">Month by month</p>
        </div>
      </header>

      <div className="h-72 w-full p-4">
        <ResponsiveContainer>
          <AreaChart data={rows} margin={{ top: 8, right: 8 }}>
            <defs>
              <linearGradient id="flow-income" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor={POSITIVE} stopOpacity={0.28} />
                <stop offset="100%" stopColor={POSITIVE} stopOpacity={0.02} />
              </linearGradient>
              <linearGradient id="flow-expense" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor={NEGATIVE} stopOpacity={0.14} />
                <stop offset="100%" stopColor={NEGATIVE} stopOpacity={0.02} />
              </linearGradient>
            </defs>
            <CartesianGrid vertical={false} stroke="var(--border)" />
            <XAxis
              dataKey="label"
              tick={AXIS_TICK}
              axisLine={{ stroke: "var(--border)" }}
              tickLine={false}
            />
            <YAxis
              tickFormatter={(v: number) => formatINRCompact(v)}
              tick={AXIS_TICK}
              width={64}
              axisLine={false}
              tickLine={false}
            />
            <Tooltip
              formatter={money}
              contentStyle={TOOLTIP_STYLE}
              labelStyle={TOOLTIP_LABEL_STYLE}
              itemStyle={TOOLTIP_ITEM_STYLE}
              cursor={{ stroke: "var(--muted-foreground)", strokeDasharray: "3 3" }}
            />
            <Legend
              iconType="circle"
              iconSize={8}
              wrapperStyle={{ fontSize: 12 }}
              formatter={legendLabel}
            />
            <Area
              type="monotone"
              dataKey="income"
              name="Income"
              stroke={POSITIVE}
              strokeWidth={2}
              fill="url(#flow-income)"
              dot={rows.length <= 12 ? { r: 3, fill: POSITIVE, strokeWidth: 0 } : false}
              activeDot={{ r: 5, stroke: "var(--card)", strokeWidth: 2 }}
            />
            <Area
              type="monotone"
              dataKey="expense"
              name="Expenses"
              stroke={NEGATIVE}
              strokeWidth={2}
              fill="url(#flow-expense)"
              dot={rows.length <= 12 ? { r: 3, fill: NEGATIVE, strokeWidth: 0 } : false}
              activeDot={{ r: 5, stroke: "var(--card)", strokeWidth: 2 }}
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>

      <div className="border-border divide-border grid grid-cols-1 divide-y border-t text-sm sm:grid-cols-3 sm:divide-x sm:divide-y-0">
        <Total icon={TrendingUp} tone="positive" label="Income" value={totalIncome} />
        <Total icon={TrendingDown} tone="negative" label="Expenses" value={totalExpense} />
        <Total icon={Scale} tone={net < 0 ? "negative" : "positive"} label="Net" value={net} />
      </div>
    </section>
  );
}

function Total({
  icon: Icon,
  tone,
  label,
  value,
}: {
  icon: typeof TrendingUp;
  tone: "positive" | "negative";
  label: string;
  value: number;
}) {
  return (
    <div className="flex items-center justify-center gap-2 p-3 sm:p-4">
      <span
        className={cn(
          "hidden rounded-md border p-1.5 sm:inline-flex",
          tone === "positive"
            ? "text-positive border-positive/30"
            : "text-negative border-negative/30",
        )}
      >
        <Icon className="size-4" aria-hidden="true" />
      </span>
      <div className="min-w-0">
        <p className="text-muted-foreground text-xs">{label}</p>
        <p className="truncate font-semibold tabular-nums">{formatINR(value)}</p>
      </div>
    </div>
  );
}
