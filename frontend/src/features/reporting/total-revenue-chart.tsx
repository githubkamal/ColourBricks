"use client";

import { TrendingDown, TrendingUp } from "lucide-react";
import { Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { formatINR } from "@/lib/format";
import type { MonthlyFlow } from "./api";

const INCOME_COLOR = "#10a75c";
const EXPENSE_COLOR = "#ff5b5b";

const TOOLTIP_STYLE = {
  background: "var(--popover)",
  border: "1px solid var(--border)",
  borderRadius: "0.75rem",
  boxShadow: "var(--shadow-md)",
  fontSize: 12,
  padding: "8px 12px",
};
const TOOLTIP_LABEL_STYLE = { color: "var(--popover-foreground)", fontWeight: 600 };

/**
 * The "Total Revenue" trend card (client request, 2026-09-07) — a smooth
 * income-vs-expense line chart plus a totals strip underneath, shared by the
 * Company and Project dashboards since both now carry the same monthly-flow
 * shape.
 */
export function TotalRevenueChart({ data }: { data: MonthlyFlow[] | undefined }) {
  if (!data || data.length === 0) return null;

  const totalIncome = data.reduce((s, m) => s + m.income, 0);
  const totalExpense = data.reduce((s, m) => s + m.expense, 0);

  return (
    <div className="bg-card border-border rounded-xl border shadow-xs">
      <div className="border-border flex items-center justify-between border-b p-4">
        <p className="text-sm font-semibold">Total Revenue</p>
      </div>

      <div className="h-72 w-full p-4">
        <ResponsiveContainer>
          <LineChart data={data}>
            <XAxis dataKey="month" fontSize={12} tick={{ fill: "var(--muted-foreground)" }} />
            <YAxis tickFormatter={(v: number) => formatINR(v)} fontSize={11} width={70} />
            <Tooltip
              formatter={(v) => formatINR(Number(v))}
              contentStyle={TOOLTIP_STYLE}
              labelStyle={TOOLTIP_LABEL_STYLE}
            />
            <Legend
              wrapperStyle={{ fontSize: 12 }}
              formatter={(value) => (value === "income" ? "Total Income" : "Total Expenses")}
            />
            <Line
              type="monotone"
              dataKey="income"
              name="income"
              stroke={INCOME_COLOR}
              strokeWidth={2.5}
              dot={{ r: 3, fill: INCOME_COLOR, strokeWidth: 0 }}
              activeDot={{ r: 5 }}
            />
            <Line
              type="monotone"
              dataKey="expense"
              name="expense"
              stroke={EXPENSE_COLOR}
              strokeWidth={2.5}
              dot={{ r: 3, fill: EXPENSE_COLOR, strokeWidth: 0 }}
              activeDot={{ r: 5 }}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>

      <div className="border-border grid grid-cols-2 divide-x border-t text-sm">
        <div className="flex items-center justify-center gap-2 p-4">
          <span className="text-negative border-negative/30 rounded-md border p-1.5">
            <TrendingDown className="size-4" aria-hidden="true" />
          </span>
          <div>
            <p className="text-muted-foreground text-xs">Expenses</p>
            <p className="font-semibold">{formatINR(totalExpense)}</p>
          </div>
        </div>
        <div className="flex items-center justify-center gap-2 p-4">
          <span className="text-positive border-positive/30 rounded-md border p-1.5">
            <TrendingUp className="size-4" aria-hidden="true" />
          </span>
          <div>
            <p className="text-muted-foreground text-xs">Revenue</p>
            <p className="font-semibold">{formatINR(totalIncome)}</p>
          </div>
        </div>
      </div>
    </div>
  );
}
