"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { listExpenseCategories } from "@/features/direct-expenses/api";
import { Button } from "@/components/ui/button";
import { AmountInput } from "@/components/ui/amount-input";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { formatINR } from "@/lib/format";
import { getProjectBudget, listBudgetRevisions, saveProjectBudget } from "./api";

export function ProjectBudgetPage({ projectId }: { projectId: number }) {
  const queryClient = useQueryClient();
  const { data: budget, isLoading } = useQuery({
    queryKey: ["project-budget", projectId],
    queryFn: () => getProjectBudget(projectId),
  });
  const { data: revisions = [] } = useQuery({
    queryKey: ["project-budget-revisions", projectId],
    queryFn: () => listBudgetRevisions(projectId),
  });
  const { data: categories = [] } = useQuery({
    queryKey: ["expense-categories", "cost"],
    queryFn: listExpenseCategories,
  });

  return (
    <div className="max-w-3xl space-y-6">
      <h1 className="text-lg font-semibold">Project budget</h1>
      {isLoading ? (
        <p className="text-sm">Loading…</p>
      ) : (
        <Editor
          key={budget?.revisionNumber ?? 0}
          projectId={projectId}
          budget={budget ?? null}
          costCategories={categories.filter((c) => c.isCost)}
          revisions={revisions}
          onSaved={() => {
            void queryClient.invalidateQueries({ queryKey: ["project-budget", projectId] });
            void queryClient.invalidateQueries({
              queryKey: ["project-budget-revisions", projectId],
            });
          }}
        />
      )}
    </div>
  );
}

const round3 = (n: number) => Math.round((n + Number.EPSILON) * 1000) / 1000;

function Editor({
  projectId,
  budget,
  costCategories,
  revisions,
  onSaved,
}: {
  projectId: number;
  budget: import("./api").ProjectBudget | null;
  costCategories: { id: number; name: string }[];
  revisions: import("./api").BudgetRevisionSummary[];
  onSaved: () => void;
}) {
  const [rows, setRows] = useState<Record<number, string>>(() =>
    Object.fromEntries((budget?.lines ?? []).map((l) => [l.categoryId, String(l.amount)])),
  );
  const [threshold, setThreshold] = useState(String(budget?.approachingThresholdPercent ?? 90));
  const [note, setNote] = useState("");

  const total = round3(Object.values(rows).reduce((s, v) => s + (Number(v) || 0), 0));
  const variance = round3(total - (budget?.estimatedCost ?? 0));

  const save = useMutation({
    mutationFn: () =>
      saveProjectBudget(projectId, {
        lines: Object.entries(rows)
          .filter(([, v]) => Number(v) > 0)
          .map(([id, v]) => ({ categoryId: Number(id), amount: Number(v) })),
        approachingThresholdPercent: Number(threshold),
        note: note.trim() || null,
      }),
    onSuccess: () => {
      toast.success("Budget revision saved");
      onSaved();
    },
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Could not save the budget"),
  });

  return (
    <div className="space-y-4">
      <p className="text-muted-foreground text-sm" data-testid="budget-heading">
        {budget ? `Current revision ${budget.revisionNumber}` : "No budget yet"} · Estimated cost{" "}
        {formatINR(budget?.estimatedCost ?? 0)}
      </p>

      <table className="w-full text-sm">
        <thead className="text-muted-foreground text-left">
          <tr>
            <th className="py-1 font-medium">Category</th>
            <th className="py-1 font-medium">Budget</th>
          </tr>
        </thead>
        <tbody>
          {costCategories.map((c) => (
            <tr key={c.id} className="border-b last:border-0">
              <td className="py-1">{c.name}</td>
              <td className="py-1">
                <AmountInput
                  className="w-40"
                  aria-label={`Budget ${c.name}`}
                  value={rows[c.id] ?? ""}
                  onChange={(v) => setRows((r) => ({ ...r, [c.id]: v }))}
                />
              </td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <td className="py-1 font-medium">Total</td>
            <td className="py-1 font-semibold" data-testid="budget-total">
              {formatINR(total)}
            </td>
          </tr>
        </tfoot>
      </table>

      <p
        className={
          Math.abs(variance) < 0.0005 ? "text-muted-foreground text-sm" : "text-attention text-sm"
        }
        data-testid="budget-variance"
      >
        Variance vs estimated cost: {formatINR(variance)}
        {variance !== 0 && " (shown, not blocked)"}
      </p>

      <div className="flex flex-wrap items-end gap-3">
        <label className="space-y-1">
          <span className="text-sm font-medium">Approaching threshold %</span>
          <Input
            className="w-24"
            inputMode="numeric"
            aria-label="Approaching threshold percent"
            value={threshold}
            onChange={(e) => setThreshold(e.target.value)}
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">Revision note</span>
          <Input
            aria-label="Revision note"
            value={note}
            onChange={(e) => setNote(e.target.value)}
          />
        </label>
        <Button type="button" disabled={total <= 0 || save.isPending} onClick={() => save.mutate()}>
          Save as revision {(budget?.revisionNumber ?? 0) + 1}
        </Button>
      </div>

      {revisions.length > 0 && (
        <div className="text-muted-foreground text-xs">
          Revisions:{" "}
          {revisions.map((r) => `#${r.revisionNumber} (${formatINR(r.budgetTotal)})`).join(", ")}
        </div>
      )}
    </div>
  );
}
