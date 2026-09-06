"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import {
  commitAllocation,
  listAllocationRuns,
  previewAllocation,
  reverseAllocationRun,
  type AllocationMethod,
  type AllocationPreview,
  type CommonExpenseType,
} from "./api";

const ALL_TYPES: CommonExpenseType[] = ["Personal", "Office", "Savings"];

export function AllocationRunPage() {
  const queryClient = useQueryClient();
  const { confirm, dialog } = useConfirmDialog();
  const [periodFrom, setPeriodFrom] = useState("");
  const [periodTo, setPeriodTo] = useState("");
  const [types, setTypes] = useState<CommonExpenseType[]>(["Personal", "Office"]);
  const [method, setMethod] = useState<AllocationMethod>("Equal");
  const [preview, setPreview] = useState<AllocationPreview | null>(null);

  const { data: runs = [] } = useQuery({
    queryKey: ["allocation-runs"],
    queryFn: listAllocationRuns,
  });

  const req = () => ({ periodFrom, periodTo, types, method });

  const previewMut = useMutation({
    mutationFn: () => previewAllocation(req()),
    onSuccess: setPreview,
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Could not preview"),
  });

  const commitMut = useMutation({
    mutationFn: () => commitAllocation(req()),
    onSuccess: () => {
      toast.success("Allocation committed");
      setPreview(null);
      void queryClient.invalidateQueries({ queryKey: ["allocation-runs"] });
    },
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Commit failed"),
  });

  const reverseMut = useMutation({
    mutationFn: (id: number) => reverseAllocationRun(id),
    onSuccess: () => {
      toast.success("Run reversed");
      void queryClient.invalidateQueries({ queryKey: ["allocation-runs"] });
    },
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Could not reverse"),
  });

  const canPreview = periodFrom !== "" && periodTo !== "" && types.length > 0;

  return (
    <div className="max-w-4xl space-y-6">
      {dialog}
      <h1 className="text-lg font-semibold">Common expense allocation</h1>

      <div className="flex flex-wrap items-end gap-3 rounded border p-3">
        <label className="space-y-1">
          <span className="text-sm font-medium">From</span>
          <Input
            type="date"
            aria-label="From"
            value={periodFrom}
            onChange={(e) => setPeriodFrom(e.target.value)}
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">To</span>
          <Input
            type="date"
            aria-label="To"
            value={periodTo}
            onChange={(e) => setPeriodTo(e.target.value)}
          />
        </label>
        <fieldset className="space-y-1">
          <span className="text-sm font-medium">Types</span>
          <div className="flex gap-3 text-sm">
            {ALL_TYPES.map((t) => (
              <label key={t} className="flex items-center gap-1">
                <input
                  type="checkbox"
                  checked={types.includes(t)}
                  onChange={(e) =>
                    setTypes((cur) => (e.target.checked ? [...cur, t] : cur.filter((x) => x !== t)))
                  }
                />
                {t}
              </label>
            ))}
          </div>
        </fieldset>
        <label className="space-y-1">
          <span className="text-sm font-medium">Method</span>
          <select
            className="bg-background text-foreground block rounded border px-3 py-1.5 text-sm"
            aria-label="Method"
            value={method}
            onChange={(e) => setMethod(e.target.value as AllocationMethod)}
          >
            <option value="Equal">Equal</option>
            <option value="Percentage">Percentage</option>
            <option value="Manual">Manual</option>
          </select>
        </label>
        <Button
          type="button"
          variant="secondary"
          disabled={!canPreview || previewMut.isPending}
          onClick={() => previewMut.mutate()}
        >
          Preview
        </Button>
      </div>

      {preview && (
        <div className="space-y-2 rounded border p-3">
          <p className="text-sm font-medium tabular-nums">
            Pool {formatINR(preview.poolAmount)} · allocated {formatINR(preview.totalAllocated)} ·{" "}
            <span
              className={preview.balances ? "text-positive" : "text-negative"}
              data-testid="preview-balance"
            >
              {preview.balances ? "balances" : "does not balance"}
            </span>
          </p>
          <table className="w-full text-sm">
            <thead className="text-muted-foreground text-left">
              <tr>
                <th className="py-1 font-medium">Project</th>
                <th className="py-1 font-medium">Before</th>
                <th className="py-1 font-medium">Allocated</th>
                <th className="py-1 font-medium">After</th>
              </tr>
            </thead>
            <tbody>
              {preview.lines.map((l) => (
                <tr key={l.projectId} className="border-b last:border-0">
                  <td className="py-1">{l.projectName}</td>
                  <td className="py-1 tabular-nums">{formatINR(l.before)}</td>
                  <td className="py-1 tabular-nums">{formatINR(l.allocated)}</td>
                  <td className="py-1 tabular-nums">{formatINR(l.after)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <Button
            type="button"
            disabled={!preview.balances || commitMut.isPending}
            onClick={() => commitMut.mutate()}
          >
            Commit allocation
          </Button>
        </div>
      )}

      <div className="rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Period</th>
              <th className="p-2 font-medium">Types</th>
              <th className="p-2 font-medium">Method</th>
              <th className="p-2 font-medium">Pool</th>
              <th className="p-2 font-medium">Status</th>
              <th className="p-2 font-medium" />
            </tr>
          </thead>
          <tbody>
            {runs.map((r) => (
              <tr key={r.id} className="border-b last:border-0">
                <td className="p-2">
                  {formatDate(r.periodFrom)} – {formatDate(r.periodTo)}
                </td>
                <td className="p-2">{r.types}</td>
                <td className="p-2">{r.method}</td>
                <td className="p-2 tabular-nums">{formatINR(r.poolAmount)}</td>
                <td className="p-2">{r.status}</td>
                <td className="p-2">
                  {r.status === "Active" && (
                    <button
                      type="button"
                      className="text-negative text-xs"
                      onClick={async () => {
                        const { confirmed } = await confirm({
                          title: "Reverse this allocation run?",
                          description:
                            "This will reverse the allocations made against these projects. This cannot be undone.",
                          destructive: true,
                          confirmLabel: "Reverse",
                        });
                        if (confirmed) reverseMut.mutate(r.id);
                      }}
                    >
                      Reverse
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
