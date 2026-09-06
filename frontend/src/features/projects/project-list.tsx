"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { ArrowDownAZ, ArrowUpAZ, CalendarRange, Plus } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useDebouncedValue } from "@/lib/use-debounced-value";
import { formatDate, formatINR } from "@/lib/format";
import { cn } from "@/lib/utils";
import { listProjects } from "./api";
import {
  PROJECT_STATUSES,
  PROJECT_STATUS_LABELS,
  type ProjectListItem,
  type ProjectStatus,
} from "./types";

type SortKey = "code" | "name" | "startDate" | "contractValue" | "status";

const SORT_LABELS: Record<SortKey, string> = {
  code: "Code",
  name: "Name",
  startDate: "Start date",
  contractValue: "Contract value",
  status: "Status",
};

const STATUS_BADGE: Record<ProjectStatus, "primary" | "positive" | "attention" | "negative"> = {
  Ongoing: "primary",
  Completed: "positive",
  OnHold: "attention",
  Cancelled: "negative",
};

const STATUS_BAR: Record<ProjectStatus, string> = {
  Ongoing: "bg-primary",
  Completed: "bg-positive",
  OnHold: "bg-attention",
  Cancelled: "bg-negative",
};

/** Share of the timeline elapsed between start and expected end, clamped to [0, 100]. */
function timelineProgress(project: ProjectListItem): number | null {
  if (project.status === "Completed") return 100;
  if (project.status === "Cancelled") return null;
  if (!project.expectedEndDate) return null;

  const start = Date.parse(project.startDate);
  const end = Date.parse(project.expectedEndDate);
  if (!Number.isFinite(start) || !Number.isFinite(end) || end <= start) return null;

  const pct = ((Date.now() - start) / (end - start)) * 100;
  return Math.min(100, Math.max(0, Math.round(pct)));
}

export function ProjectList() {
  const [status, setStatus] = useState<ProjectStatus | undefined>(undefined);
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search, 300);
  const [page, setPage] = useState(1);
  const [sortBy, setSortBy] = useState<SortKey>("code");
  const [sortDir, setSortDir] = useState<"asc" | "desc">("asc");

  const { data, isPending, isError } = useQuery({
    queryKey: ["projects", { status, search: debouncedSearch, page, sortBy, sortDir }],
    queryFn: () => listProjects({ status, search: debouncedSearch, page, sortBy, sortDir }),
    placeholderData: keepPreviousData,
  });

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-heading text-2xl font-semibold">Projects</h1>
          <p className="text-muted-foreground mt-0.5 text-sm">
            Contract value, timeline and status across every build on the books.
          </p>
        </div>
        <Link href="/projects/new" className={buttonVariants({ size: "sm" })}>
          <Plus aria-hidden="true" />
          New project
        </Link>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <StatusTab
          active={status === undefined}
          onClick={() => {
            setStatus(undefined);
            setPage(1);
          }}
        >
          All{data ? ` (${data.totalCount})` : ""}
        </StatusTab>
        {PROJECT_STATUSES.map((s) => (
          <StatusTab
            key={s}
            active={status === s}
            onClick={() => {
              setStatus(s);
              setPage(1);
            }}
          >
            {PROJECT_STATUS_LABELS[s]}
          </StatusTab>
        ))}

        <div className="ml-auto flex flex-wrap items-center gap-2">
          <label className="text-muted-foreground flex items-center gap-1.5 text-sm">
            Sort
            <select
              value={sortBy}
              onChange={(e) => setSortBy(e.target.value as SortKey)}
              className="border-input bg-background h-8 rounded-md border px-2 text-sm"
            >
              {Object.entries(SORT_LABELS).map(([key, label]) => (
                <option key={key} value={key}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            onClick={() => setSortDir((dir) => (dir === "asc" ? "desc" : "asc"))}
            aria-label={sortDir === "asc" ? "Sort ascending" : "Sort descending"}
            className="border-input bg-background hover:bg-muted flex size-8 items-center justify-center rounded-md border"
          >
            {sortDir === "asc" ? (
              <ArrowUpAZ className="size-4" aria-hidden="true" />
            ) : (
              <ArrowDownAZ className="size-4" aria-hidden="true" />
            )}
          </button>
          <Input
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            placeholder="Search code or name…"
            className="h-8 w-56"
          />
        </div>
      </div>

      {isPending && (
        <div className="text-muted-foreground rounded-lg border border-dashed p-10 text-center text-sm">
          Loading projects…
        </div>
      )}

      {isError && (
        <div className="text-negative border-negative/30 rounded-lg border border-dashed p-10 text-center text-sm">
          Could not load projects.
        </div>
      )}

      {data?.items.length === 0 && (
        <div className="text-muted-foreground rounded-lg border border-dashed p-10 text-center text-sm">
          No projects yet. Create the first one.
        </div>
      )}

      {data && data.items.length > 0 && (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          {data.items.map((project) => (
            <ProjectCard key={project.id} project={project} />
          ))}
        </div>
      )}

      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground">
            {data.totalCount} project{data.totalCount === 1 ? "" : "s"} · page {data.page} of{" "}
            {data.totalPages}
          </span>
          <div className="flex gap-2">
            <button
              type="button"
              disabled={data.page <= 1}
              onClick={() => setPage((p) => p - 1)}
              className={cn(buttonVariants({ variant: "outline", size: "sm" }))}
            >
              Previous
            </button>
            <button
              type="button"
              disabled={data.page >= data.totalPages}
              onClick={() => setPage((p) => p + 1)}
              className={cn(buttonVariants({ variant: "outline", size: "sm" }))}
            >
              Next
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function ProjectCard({ project }: { project: ProjectListItem }) {
  const progress = timelineProgress(project);

  return (
    <Link
      href={`/projects/${project.id}`}
      className="bg-card border-border group flex flex-col gap-3 rounded-xl border p-4 shadow-xs transition-shadow hover:shadow-md"
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="text-muted-foreground font-mono text-[11px]">{project.code}</p>
          <h2 className="font-heading line-clamp-2 text-base font-semibold group-hover:underline">
            {project.name}
          </h2>
        </div>
        <Badge variant={STATUS_BADGE[project.status]}>
          {PROJECT_STATUS_LABELS[project.status]}
        </Badge>
      </div>

      <div className="grid grid-cols-2 gap-3 text-sm">
        <div>
          <p className="text-muted-foreground text-xs">Contract value</p>
          <p className="num font-medium">{formatINR(project.contractValue)}</p>
        </div>
        <div>
          <p className="text-muted-foreground text-xs">Estimated cost</p>
          <p className="num font-medium">{formatINR(project.estimatedCost)}</p>
        </div>
      </div>

      {progress !== null ? (
        <div>
          <div className="text-muted-foreground mb-1 flex items-center justify-between text-xs">
            <span>Timeline</span>
            <span className="text-foreground font-medium">{progress}%</span>
          </div>
          <div className="bg-muted h-1.5 w-full overflow-hidden rounded-full">
            <div
              className={cn("h-full rounded-full", STATUS_BAR[project.status])}
              style={{ width: `${progress}%` }}
            />
          </div>
        </div>
      ) : (
        <div className="text-muted-foreground text-xs">No end date set</div>
      )}

      <div className="text-muted-foreground border-border flex items-center gap-1.5 border-t pt-3 text-xs">
        <CalendarRange className="size-3.5 shrink-0" aria-hidden="true" />
        <span>
          {formatDate(project.startDate)}
          {project.expectedEndDate ? ` – ${formatDate(project.expectedEndDate)}` : ""}
        </span>
      </div>
    </Link>
  );
}

function StatusTab({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "rounded-full px-3 py-1 text-sm transition-colors",
        active ? "bg-primary text-primary-foreground" : "hover:bg-secondary",
      )}
    >
      {children}
    </button>
  );
}
