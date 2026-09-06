"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { listTemples } from "@/features/temples/api";
import { ApiError } from "@/lib/api";
import { formatINR } from "@/lib/format";
import { getProjectDonation, upsertProjectDonation } from "./api";
import type { DonationBasis, ProjectDonation } from "./types";

const round3 = (value: number) => Math.round((value + Number.EPSILON) * 1000) / 1000;

function newRowId(): string {
  return typeof crypto !== "undefined" && "randomUUID" in crypto
    ? crypto.randomUUID()
    : `row-${Math.random().toString(36).slice(2)}`;
}

interface SplitRow {
  id: string;
  templeId: number | "";
  amount: string;
}

interface FormProps {
  projectId: number;
  projectContractValue: number;
}

export function ProjectDonationForm({ projectId, projectContractValue }: FormProps) {
  const { data: existing, isPending } = useQuery({
    queryKey: ["project-donation", projectId],
    queryFn: () => getProjectDonation(projectId),
  });

  if (isPending) return <p className="text-muted-foreground text-sm">Loading donation…</p>;

  return (
    <DonationEditor
      key={projectId}
      projectId={projectId}
      projectContractValue={projectContractValue}
      existing={existing ?? null}
    />
  );
}

function DonationEditor({
  projectId,
  projectContractValue,
  existing,
}: FormProps & { existing: ProjectDonation | null }) {
  const queryClient = useQueryClient();
  const [basis, setBasis] = useState<DonationBasis>(existing?.basis ?? "Percentage");
  const [percentage, setPercentage] = useState(
    existing?.percentage != null ? String(existing.percentage) : "",
  );
  const [fixedAmount, setFixedAmount] = useState(
    existing?.fixedAmount != null ? String(existing.fixedAmount) : "",
  );
  const [rows, setRows] = useState<SplitRow[]>(
    existing && existing.temples.length > 0
      ? existing.temples.map((t) => ({
          id: newRowId(),
          templeId: t.templeId,
          amount: String(t.amount),
        }))
      : [{ id: newRowId(), templeId: "", amount: "" }],
  );

  const { data: temples = [] } = useQuery({ queryKey: ["temples"], queryFn: () => listTemples() });

  const [initialSnapshot] = useState(() =>
    JSON.stringify({ basis, percentage, fixedAmount, rows }),
  );
  const [submitted, setSubmitted] = useState(false);
  const isDirty = JSON.stringify({ basis, percentage, fixedAmount, rows }) !== initialSnapshot;

  useEffect(() => {
    if (!isDirty || submitted) return;
    function handler(e: BeforeUnloadEvent) {
      e.preventDefault();
    }
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [isDirty, submitted]);

  const donationAmount = useMemo(() => {
    if (basis === "Percentage") {
      const pct = Number(percentage);
      return Number.isFinite(pct) ? round3((projectContractValue * pct) / 100) : 0;
    }
    const flat = Number(fixedAmount);
    return Number.isFinite(flat) ? round3(flat) : 0;
  }, [basis, percentage, fixedAmount, projectContractValue]);

  const splitTotal = round3(rows.reduce((sum, r) => sum + (Number(r.amount) || 0), 0));
  const balanced = donationAmount > 0 && splitTotal === donationAmount;
  const templesChosen = rows.every((r) => r.templeId !== "");

  const save = useMutation({
    mutationFn: () =>
      upsertProjectDonation(projectId, {
        basis,
        percentage: basis === "Percentage" ? Number(percentage) : null,
        fixedAmount: basis === "Fixed" ? Number(fixedAmount) : null,
        temples: rows.map((r) => ({ templeId: Number(r.templeId), amount: Number(r.amount) })),
      }),
    onSuccess: (donation) => {
      toast.success(`Donation set: ${formatINR(donation.donationAmount)}`);
      setSubmitted(true);
      void queryClient.invalidateQueries({ queryKey: ["project-donation", projectId] });
      void queryClient.invalidateQueries({ queryKey: ["donation-outstanding", projectId] });
    },
    onError: (error) => {
      const message =
        error instanceof ApiError
          ? (error.fieldErrors?.temples?.[0] ?? error.message)
          : "Could not save the donation";
      toast.error(message);
    },
  });

  return (
    <form
      className="max-w-xl space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        save.mutate();
      }}
    >
      {existing?.needsReview && (
        <p className="text-attention rounded border px-3 py-2 text-sm">
          The contract value changed. The donation is now {formatINR(existing.donationAmount)} but
          the temple split still totals{" "}
          {formatINR(existing.temples.reduce((s, t) => s + t.amount, 0))} — please review.
        </p>
      )}

      <div className="flex gap-4 text-sm">
        <label className="flex items-center gap-1">
          <input
            type="radio"
            name="basis"
            checked={basis === "Percentage"}
            onChange={() => setBasis("Percentage")}
          />
          Percentage
        </label>
        <label className="flex items-center gap-1">
          <input
            type="radio"
            name="basis"
            checked={basis === "Fixed"}
            onChange={() => setBasis("Fixed")}
          />
          Fixed
        </label>
      </div>

      {basis === "Percentage" ? (
        <label className="block space-y-1">
          <span className="text-sm font-medium">Percentage of contract value</span>
          <AmountInput
            value={percentage}
            onChange={setPercentage}
            aria-label="Percentage of contract value"
            placeholder="e.g. 2"
          />
        </label>
      ) : (
        <label className="block space-y-1">
          <span className="text-sm font-medium">Fixed donation amount</span>
          <AmountInput
            value={fixedAmount}
            onChange={setFixedAmount}
            aria-label="Fixed donation amount"
          />
        </label>
      )}

      <div className="bg-card rounded border p-3">
        <p className="text-muted-foreground text-xs uppercase">Donation amount</p>
        <p className="text-xl font-semibold" data-testid="donation-amount">
          {formatINR(donationAmount)}
        </p>
        <p className="text-muted-foreground text-xs">
          on a contract value of {formatINR(projectContractValue)}
        </p>
      </div>

      <div className="space-y-2">
        <p className="text-sm font-medium">Split across temples</p>
        {rows.map((row, index) => (
          <div key={row.id} className="flex gap-2">
            <select
              className="bg-card flex-1 rounded border px-2 py-1.5 text-sm"
              value={row.templeId}
              aria-label={`Temple ${index + 1}`}
              onChange={(e) =>
                setRows((rs) =>
                  rs.map((r) =>
                    r.id === row.id
                      ? { ...r, templeId: e.target.value ? Number(e.target.value) : "" }
                      : r,
                  ),
                )
              }
            >
              <option value="">Select temple…</option>
              {temples.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
            </select>
            <AmountInput
              className="w-40"
              value={row.amount}
              aria-label={`Amount ${index + 1}`}
              onChange={(v) =>
                setRows((rs) => rs.map((r) => (r.id === row.id ? { ...r, amount: v } : r)))
              }
            />
            {rows.length > 1 && (
              <Button
                type="button"
                variant="ghost"
                size="xs"
                onClick={() => setRows((rs) => rs.filter((r) => r.id !== row.id))}
              >
                Remove
              </Button>
            )}
          </div>
        ))}
        <Button
          type="button"
          variant="ghost"
          size="xs"
          onClick={() => setRows((rs) => [...rs, { id: newRowId(), templeId: "", amount: "" }])}
        >
          + Add temple
        </Button>
        <p className={balanced ? "text-muted-foreground text-xs" : "text-attention text-xs"}>
          Split totals {formatINR(splitTotal)} of {formatINR(donationAmount)}
        </p>
      </div>

      <Button type="submit" disabled={save.isPending || !balanced || !templesChosen}>
        Save donation
      </Button>
    </form>
  );
}
