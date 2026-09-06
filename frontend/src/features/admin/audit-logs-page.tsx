"use client";

import { useQuery } from "@tanstack/react-query";
import { Fragment, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useQueryParam, useQueryParamNumber } from "@/lib/use-query-param";
import { listAuditLogs, type AuditLogQuery } from "./audit-logs-api";

const timestampFormatter = new Intl.DateTimeFormat("en-IN", {
  dateStyle: "medium",
  timeStyle: "short",
});

/** Admin > Audit Logs (BRD §65): every create/update/delete/reverse, who did it, and why. */
export function AuditLogsPage() {
  const [userId, setUserId] = useQueryParam("userId", "");
  const [module, setModule] = useQueryParam("module", "");
  const [action, setAction] = useQueryParam("action", "");
  const [recordId, setRecordId] = useQueryParam("recordId", "");
  const [dateFrom, setDateFrom] = useQueryParam("dateFrom", "");
  const [dateTo, setDateTo] = useQueryParam("dateTo", "");
  const [page, setPage] = useQueryParamNumber("page", 1);
  const [expanded, setExpanded] = useState<number | null>(null);

  const filter: AuditLogQuery = {
    userId: userId ? Number(userId) : undefined,
    module: module || undefined,
    action: action || undefined,
    recordId: recordId || undefined,
    dateFrom: dateFrom || undefined,
    dateTo: dateTo || undefined,
    page,
    pageSize: 50,
  };

  const { data, isPending } = useQuery({
    queryKey: ["audit-logs", filter],
    queryFn: () => listAuditLogs(filter),
  });

  function resetFilters() {
    setUserId("");
    setModule("");
    setAction("");
    setRecordId("");
    setDateFrom("");
    setDateTo("");
    setPage(1);
  }

  return (
    <div className="space-y-4">
      <h1 className="text-lg font-semibold">Audit Logs</h1>

      <div className="bg-card flex flex-wrap items-end gap-3 rounded border p-3">
        <label className="space-y-1">
          <span className="text-sm font-medium">User id</span>
          <Input
            className="w-24"
            inputMode="numeric"
            aria-label="User id"
            value={userId}
            onChange={(e) => {
              setUserId(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">Module</span>
          <Input
            className="w-36"
            aria-label="Module"
            value={module}
            onChange={(e) => {
              setModule(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">Action</span>
          <Input
            className="w-32"
            aria-label="Action"
            value={action}
            onChange={(e) => {
              setAction(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">Record id</span>
          <Input
            className="w-28"
            aria-label="Record id"
            value={recordId}
            onChange={(e) => {
              setRecordId(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">From</span>
          <Input
            type="date"
            aria-label="From"
            value={dateFrom}
            onChange={(e) => {
              setDateFrom(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">To</span>
          <Input
            type="date"
            aria-label="To"
            value={dateTo}
            onChange={(e) => {
              setDateTo(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <Button type="button" variant="reset" onClick={resetFilters}>
          Reset filters
        </Button>
      </div>

      <div className="bg-card overflow-x-auto rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Time</th>
              <th className="p-2 font-medium">User</th>
              <th className="p-2 font-medium">Module</th>
              <th className="p-2 font-medium">Action</th>
              <th className="p-2 font-medium">Entity</th>
              <th className="p-2 font-medium">Record</th>
              <th className="p-2 font-medium">Details</th>
            </tr>
          </thead>
          <tbody>
            {isPending && (
              <tr>
                <td colSpan={7} className="text-muted-foreground p-3 text-center">
                  Loading…
                </td>
              </tr>
            )}
            {!isPending && (data?.items.length ?? 0) === 0 && (
              <tr>
                <td colSpan={7} className="text-muted-foreground p-3 text-center">
                  No audit rows match these filters.
                </td>
              </tr>
            )}
            {data?.items.map((row) => (
              <Fragment key={row.id}>
                <tr className="border-b last:border-0">
                  <td className="p-2 whitespace-nowrap">
                    {timestampFormatter.format(new Date(row.timestampUtc))}
                  </td>
                  <td className="p-2">{row.userId ?? "—"}</td>
                  <td className="p-2">{row.module}</td>
                  <td className="p-2">{row.action}</td>
                  <td className="p-2">{row.entityType ?? "—"}</td>
                  <td className="p-2">{row.recordId}</td>
                  <td className="p-2">
                    {row.details ? (
                      <span>{row.details}</span>
                    ) : row.oldValues || row.newValues ? (
                      <button
                        type="button"
                        className="text-primary underline"
                        onClick={() => setExpanded(expanded === row.id ? null : row.id)}
                      >
                        {expanded === row.id ? "Hide" : "View changes"}
                      </button>
                    ) : (
                      "—"
                    )}
                  </td>
                </tr>
                {expanded === row.id && (row.oldValues || row.newValues) && (
                  <tr className="bg-secondary/30 border-b last:border-0">
                    <td colSpan={7} className="space-y-2 p-3">
                      {row.oldValues && (
                        <div>
                          <p className="text-muted-foreground text-xs font-medium">Old values</p>
                          <pre className="overflow-x-auto text-xs">{row.oldValues}</pre>
                        </div>
                      )}
                      {row.newValues && (
                        <div>
                          <p className="text-muted-foreground text-xs font-medium">New values</p>
                          <pre className="overflow-x-auto text-xs">{row.newValues}</pre>
                        </div>
                      )}
                    </td>
                  </tr>
                )}
              </Fragment>
            ))}
          </tbody>
        </table>
      </div>

      {data && (
        <div className="flex items-center gap-3 text-sm">
          <span className="text-muted-foreground">{data.totalCount} rows</span>
          <button
            type="button"
            className="rounded border px-3 py-1 disabled:opacity-50"
            disabled={data.page <= 1}
            onClick={() => setPage(page - 1)}
          >
            Prev
          </button>
          <span>
            Page {data.page} of {Math.max(data.totalPages, 1)}
          </span>
          <button
            type="button"
            className="rounded border px-3 py-1 disabled:opacity-50"
            disabled={data.page >= data.totalPages}
            onClick={() => setPage(page + 1)}
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
}
