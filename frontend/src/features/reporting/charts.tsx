"use client";

import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Pie,
  PieChart,
  PolarAngleAxis,
  RadialBar,
  RadialBarChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { formatINR, formatINRCompact } from "@/lib/format";
import { cn } from "@/lib/utils";

/**
 * The dashboards' shared chart kit, ported from Ledgerstone (client request,
 * 2026-10-11 — same charts, ColourBricks' own white theme). Colours come from the
 * validated `--chart-*` tokens in globals.css, assigned in fixed order; money in/out
 * keeps the app's own
 * positive/negative tokens so a chart and a tile never disagree on what green means.
 */

export const SERIES = [
  "var(--chart-1)",
  "var(--chart-2)",
  "var(--chart-3)",
  "var(--chart-4)",
  "var(--chart-5)",
  "var(--chart-6)",
  "var(--chart-7)",
  "var(--chart-8)",
];
export const OTHER = "var(--chart-other)";
export const POSITIVE = "var(--positive)";
export const NEGATIVE = "var(--negative)";
export const ATTENTION = "var(--attention)";

export const TOOLTIP_STYLE = {
  background: "var(--popover)",
  border: "1px solid var(--border)",
  borderRadius: "0.75rem",
  boxShadow: "var(--shadow-md)",
  color: "var(--popover-foreground)",
  fontSize: 12,
  padding: "8px 12px",
};
export const TOOLTIP_LABEL_STYLE = { color: "var(--popover-foreground)", fontWeight: 600 };
export const TOOLTIP_ITEM_STYLE = { color: "var(--popover-foreground)" };
export const AXIS_TICK = { fill: "var(--muted-foreground)", fontSize: 11 };
const CURSOR = { fill: "var(--muted)", opacity: 0.6 };

export const money = (v: unknown) => formatINR(Number(v));

/** Legend labels stay in text ink — the swatch beside them carries the colour. */
export function legendLabel(value: string) {
  return <span style={{ color: "var(--muted-foreground)" }}>{value}</span>;
}

/** A single-line category tick, truncated, so long names never wrap onto two lines. */
function CategoryTick({
  x,
  y,
  payload,
  max,
  anchor = "end",
  dy = 4,
}: {
  x?: number;
  y?: number;
  payload?: { value: string };
  max: number;
  anchor?: "end" | "middle";
  dy?: number;
}) {
  const value = String(payload?.value ?? "");
  return (
    <text x={x} y={y} dy={dy} textAnchor={anchor} fill="var(--muted-foreground)" fontSize={11}>
      <title>{value}</title>
      {truncate(value, max)}
    </text>
  );
}

export function truncate(label: string, max = 14): string {
  return label.length > max ? `${label.slice(0, max - 1)}…` : label;
}

export interface NamedValue {
  name: string;
  value: number;
}

/** Keeps the largest `keep` slices and folds the rest into one "Other" slice. */
export function foldOther(rows: NamedValue[], keep = 7): (NamedValue & { color: string })[] {
  const sorted = [...rows].filter((r) => r.value > 0).sort((a, b) => b.value - a.value);
  const head = sorted.slice(0, keep).map((r, i) => ({ ...r, color: SERIES[i] }));
  const rest = sorted.slice(keep).reduce((sum, r) => sum + r.value, 0);
  return rest > 0 ? [...head, { name: "Other", value: rest, color: OTHER }] : head;
}

export function ChartCard({
  title,
  subtitle,
  right,
  className,
  bodyClassName = "h-64",
  children,
}: {
  title: string;
  subtitle?: string;
  right?: React.ReactNode;
  className?: string;
  bodyClassName?: string;
  children: React.ReactNode;
}) {
  return (
    <section
      aria-label={title}
      className={cn(
        "bg-card border-border flex min-w-0 flex-col rounded-xl border p-4 shadow-xs transition-shadow hover:shadow-md",
        className,
      )}
    >
      <header className="mb-3 flex items-start justify-between gap-2">
        <div className="min-w-0">
          <h2 className="text-sm font-semibold">{title}</h2>
          {subtitle && <p className="text-muted-foreground mt-0.5 text-xs">{subtitle}</p>}
        </div>
        {right}
      </header>
      <div className={cn("w-full min-w-0", bodyClassName)}>{children}</div>
    </section>
  );
}

/** A small swatch + label + value legend, so no slice relies on colour alone. */
function ValueLegend({
  rows,
  format,
  total,
}: {
  rows: (NamedValue & { color: string })[];
  format: (v: number) => string;
  total: number;
}) {
  return (
    <ul className="w-full min-w-0 flex-1 space-y-1.5 text-xs">
      {rows.map((r) => (
        <li key={r.name} className="flex items-center gap-2">
          <span
            className="size-2.5 shrink-0 rounded-[3px]"
            style={{ background: r.color }}
            aria-hidden="true"
          />
          <span className="text-muted-foreground min-w-0 flex-1 truncate" title={r.name}>
            {r.name}
          </span>
          <span className="font-medium tabular-nums">{format(r.value)}</span>
          <span className="text-muted-foreground w-9 text-right tabular-nums">
            {total > 0 ? `${Math.round((r.value / total) * 100)}%` : ""}
          </span>
        </li>
      ))}
    </ul>
  );
}

/** A donut with its total in the hole and a value legend beside it. */
export function DonutChart({
  rows,
  centerLabel,
  format = formatINRCompact,
  tooltipFormat = money,
  keep = 7,
}: {
  rows: NamedValue[];
  centerLabel: string;
  format?: (v: number) => string;
  tooltipFormat?: (v: unknown) => string;
  keep?: number;
}) {
  const data = foldOther(rows, keep);
  const total = data.reduce((s, r) => s + r.value, 0);
  return (
    <div className="@container">
      <div className="flex flex-col items-center gap-4 py-1 @md:flex-row">
        <div className="relative size-44 shrink-0">
          <ResponsiveContainer>
            <PieChart>
              <Pie
                data={data}
                dataKey="value"
                nameKey="name"
                innerRadius="64%"
                outerRadius="100%"
                paddingAngle={data.length > 1 ? 1.5 : 0}
                cornerRadius={4}
                // The separator stroke only reads as a gap between slices; on a
                // single full ring it would draw a stray seam.
                stroke={data.length > 1 ? "var(--card)" : "none"}
                strokeWidth={2}
                isAnimationActive
              >
                {data.map((r) => (
                  <Cell key={r.name} fill={r.color} />
                ))}
              </Pie>
              <Tooltip
                formatter={tooltipFormat}
                contentStyle={TOOLTIP_STYLE}
                labelStyle={TOOLTIP_LABEL_STYLE}
                itemStyle={TOOLTIP_ITEM_STYLE}
              />
            </PieChart>
          </ResponsiveContainer>
          <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
            <span className="font-heading text-lg font-semibold tabular-nums">{format(total)}</span>
            <span className="text-muted-foreground text-[11px]">{centerLabel}</span>
          </div>
        </div>
        <ValueLegend rows={data} format={format} total={total} />
      </div>
    </div>
  );
}

/** Ranked horizontal bars — names on the left, one colour, values in the tooltip. */
export function RankedBars({
  rows,
  color = SERIES[0],
  name = "Amount",
  colorFor,
}: {
  rows: NamedValue[];
  color?: string;
  name?: string;
  colorFor?: (row: NamedValue) => string;
}) {
  return (
    <ResponsiveContainer>
      <BarChart data={rows} layout="vertical" margin={{ left: 4, right: 12 }} barCategoryGap="28%">
        <CartesianGrid horizontal={false} stroke="var(--border)" />
        <XAxis
          type="number"
          tickFormatter={(v: number) => formatINRCompact(v)}
          tick={AXIS_TICK}
          axisLine={false}
          tickLine={false}
        />
        <YAxis
          type="category"
          dataKey="name"
          // Project names here are often all caps, which run wider per character.
          width={150}
          tick={<CategoryTick max={18} />}
          axisLine={false}
          tickLine={false}
        />
        <Tooltip
          formatter={money}
          contentStyle={TOOLTIP_STYLE}
          labelStyle={TOOLTIP_LABEL_STYLE}
          itemStyle={TOOLTIP_ITEM_STYLE}
          cursor={CURSOR}
        />
        <Bar dataKey="value" name={name} fill={color} radius={[0, 4, 4, 0]} maxBarSize={22}>
          {colorFor && rows.map((r) => <Cell key={r.name} fill={colorFor(r)} />)}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}

export interface SeriesSpec {
  key: string;
  name: string;
  color: string;
  stack?: string;
}

/** Grouped (or, with `stack`, stacked) vertical bars over a category axis. */
export function GroupedBars({
  data,
  categoryKey,
  series,
}: {
  data: object[];
  categoryKey: string;
  series: SeriesSpec[];
}) {
  return (
    <ResponsiveContainer>
      <BarChart data={data} barCategoryGap="24%" barGap={2} margin={{ right: 8 }}>
        <CartesianGrid vertical={false} stroke="var(--border)" />
        <XAxis
          dataKey={categoryKey}
          tick={<CategoryTick max={data.length > 5 ? 10 : 16} anchor="middle" dy={14} />}
          axisLine={{ stroke: "var(--border)" }}
          tickLine={false}
          interval={0}
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
          cursor={CURSOR}
        />
        <Legend
          iconType="circle"
          iconSize={8}
          wrapperStyle={{ fontSize: 12 }}
          formatter={legendLabel}
        />
        {series.map((s, i) => {
          // Only the top segment of a stack gets the rounded data-end.
          const topOfStack = !s.stack || !series.slice(i + 1).some((o) => o.stack === s.stack);
          return (
            <Bar
              key={s.key}
              dataKey={s.key}
              name={s.name}
              fill={s.color}
              stackId={s.stack}
              stroke={s.stack ? "var(--card)" : undefined}
              strokeWidth={s.stack ? 1 : 0}
              radius={topOfStack ? [4, 4, 0, 0] : 0}
              maxBarSize={36}
            />
          );
        })}
      </BarChart>
    </ResponsiveContainer>
  );
}

/** A half-ring gauge for one percentage (e.g. budget used, profit margin). */
export function Gauge({
  value,
  label,
  color,
  caption,
}: {
  value: number;
  label: string;
  color: string;
  caption?: string;
}) {
  const clamped = Math.max(0, Math.min(100, value));
  return (
    <div className="relative h-full">
      <ResponsiveContainer>
        <RadialBarChart
          data={[{ name: label, value: clamped }]}
          startAngle={180}
          endAngle={0}
          innerRadius="78%"
          outerRadius="100%"
          cy="72%"
        >
          <PolarAngleAxis type="number" domain={[0, 100]} tick={false} />
          <RadialBar
            dataKey="value"
            cornerRadius={8}
            fill={color}
            background={{ fill: "var(--muted)" }}
            isAnimationActive
          />
        </RadialBarChart>
      </ResponsiveContainer>
      <div className="pointer-events-none absolute inset-x-0 top-[48%] flex flex-col items-center">
        <span className="font-heading text-3xl font-semibold tabular-nums">
          {value.toFixed(1)}%
        </span>
        <span className="text-muted-foreground text-xs">{label}</span>
        {caption && <span className="text-muted-foreground mt-1 text-[11px]">{caption}</span>}
      </div>
    </div>
  );
}

/** Shown in a chart card when its dataset is empty for the selected period. */
export function ChartEmpty({ label = "Nothing to chart for this period." }: { label?: string }) {
  return (
    <div className="text-muted-foreground flex h-full items-center justify-center rounded-lg border border-dashed text-xs">
      {label}
    </div>
  );
}
