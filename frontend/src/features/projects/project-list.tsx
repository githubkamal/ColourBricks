"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useDebouncedValue } from "@/lib/use-debounced-value";
import { formatDate, formatINR } from "@/lib/format";
import { cn } from "@/lib/utils";
import { listProjects } from "./api";
import { PROJECT_STATUSES, PROJECT_STATUS_LABELS, type ProjectStatus } from "./types";

type SortKey = "code" | "name" | "startDate" | "contractValue" | "status";

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

  function toggleSort(key: SortKey) {
    if (sortBy === key) {
      setSortDir((dir) => (dir === "asc" ? "desc" : "asc"));
    } else {
      setSortBy(key);
      setSortDir("asc");
    }
    setPage(1);
  }

  const sortIndicator = (key: SortKey) => (sortBy === key ? (sortDir === "asc" ? " ▲" : " ▼") : "");

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-lg font-semibold">Projects</h1>
        <Link href="/projects/new" className={buttonVariants({ size: "sm" })}>
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
          All
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
        <Input
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
          placeholder="Search code or name…"
          className="ml-auto h-8 w-56"
        />
      </div>

      <div className="overflow-x-auto rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <Th onClick={() => toggleSort("code")}>Code{sortIndicator("code")}</Th>
              <Th onClick={() => toggleSort("name")}>Name{sortIndicator("name")}</Th>
              <Th onClick={() => toggleSort("status")}>Status{sortIndicator("status")}</Th>
              <Th onClick={() => toggleSort("startDate")}>Start{sortIndicator("startDate")}</Th>
              <Th className="num" onClick={() => toggleSort("contractValue")}>
                Contract value{sortIndicator("contractValue")}
              </Th>
              <Th className="num">Estimated cost</Th>
            </tr>
          </thead>
          <tbody>
            {isPending && (
              <tr>
                <td colSpan={6} className="text-muted-foreground p-4 text-center">
                  Loading…
                </td>
              </tr>
            )}
            {isError && (
              <tr>
                <td colSpan={6} className="text-negative p-4 text-center">
                  Could not load projects.
                </td>
              </tr>
            )}
            {data?.items.length === 0 && (
              <tr>
                <td colSpan={6} className="text-muted-foreground p-4 text-center">
                  No projects yet. Create the first one.
                </td>
              </tr>
            )}
            {data?.items.map((project) => (
              <tr key={project.id} className="hover:bg-secondary/40 border-b last:border-0">
                <td className="p-2 font-mono text-xs">
                  <Link href={`/projects/${project.id}`} className="hover:underline">
                    {project.code}
                  </Link>
                </td>
                <td className="p-2">
                  <Link href={`/projects/${project.id}`} className="hover:underline">
                    {project.name}
                  </Link>
                </td>
                <td className="p-2">{PROJECT_STATUS_LABELS[project.status]}</td>
                <td className="p-2">{formatDate(project.startDate)}</td>
                <td className="num p-2">{formatINR(project.contractValue)}</td>
                <td className="num p-2">{formatINR(project.estimatedCost)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground">
            {data.totalCount} project{data.totalCount === 1 ? "" : "s"} · page {data.page} of{" "}
            {data.totalPages}
          </span>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={data.page <= 1}
              onClick={() => setPage((p) => p - 1)}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={data.page >= data.totalPages}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </Button>
          </div>
        </div>
      )}
    </div>
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
        "rounded px-2.5 py-1 text-sm",
        active ? "bg-primary text-primary-foreground" : "hover:bg-secondary",
      )}
    >
      {children}
    </button>
  );
}

function Th({
  children,
  className,
  onClick,
}: {
  children: React.ReactNode;
  className?: string;
  onClick?: () => void;
}) {
  return (
    <th
      className={cn("p-2 font-medium", onClick && "cursor-pointer select-none", className)}
      onClick={onClick}
      onKeyDown={
        onClick
          ? (e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                onClick();
              }
            }
          : undefined
      }
      tabIndex={onClick ? 0 : undefined}
    >
      {children}
    </th>
  );
}
