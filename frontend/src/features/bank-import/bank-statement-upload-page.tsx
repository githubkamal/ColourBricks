"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import {
  createBankStatementProfile,
  detectStatementColumns,
  listBankStatementProfiles,
  uploadStatement,
  type DetectedColumns,
} from "./api";

type Field = "dateColumn" | "narrationColumn" | "referenceColumn" | "balanceColumn";

export function BankStatementUploadPage() {
  const router = useRouter();
  const [accountId, setAccountId] = useState<number | "">("");
  const [file, setFile] = useState<File | null>(null);
  const [profileId, setProfileId] = useState<number | "">("");

  const [headerRowIndex, setHeaderRowIndex] = useState("0");
  const [detected, setDetected] = useState<DetectedColumns | null>(null);
  const [name, setName] = useState("");
  const [singleAmount, setSingleAmount] = useState(false);
  const [dateFormats, setDateFormats] = useState("dd/MM/yyyy");
  const [map, setMap] = useState<Record<string, number>>({
    dateColumn: 0,
    narrationColumn: 1,
    amountColumn: 2,
    debitColumn: 2,
    creditColumn: 3,
  });

  const { data: accounts = [] } = useQuery({
    queryKey: ["accounts", "banks"],
    queryFn: () => listAccounts(),
  });
  const { data: profiles = [] } = useQuery({
    queryKey: ["bank-statement-profiles", accountId],
    queryFn: () => listBankStatementProfiles(Number(accountId)),
    enabled: accountId !== "",
  });

  const detect = useMutation({
    mutationFn: () => detectStatementColumns(file!, Number(headerRowIndex)),
    onSuccess: setDetected,
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Could not read the file"),
  });

  const go = (batchId: number) => router.push(`/reconciliation/imports/${batchId}`);

  const uploadWithProfile = useMutation({
    mutationFn: () => uploadStatement(Number(accountId), Number(profileId), file!),
    onSuccess: (b) => go(b.id),
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Upload failed"),
  });

  const saveAndUpload = useMutation({
    mutationFn: async () => {
      const profile = await createBankStatementProfile({
        accountId: Number(accountId),
        name: name.trim(),
        headerRowIndex: Number(headerRowIndex),
        delimiter: ",",
        dateColumn: map.dateColumn,
        narrationColumn: map.narrationColumn,
        referenceColumn: map.referenceColumn ?? null,
        balanceColumn: map.balanceColumn ?? null,
        singleAmountColumn: singleAmount,
        amountColumn: singleAmount ? map.amountColumn : null,
        debitColumn: singleAmount ? null : map.debitColumn,
        creditColumn: singleAmount ? null : map.creditColumn,
        dateFormats: dateFormats.trim(),
      });
      return uploadStatement(Number(accountId), profile.id, file!);
    },
    onSuccess: (b) => go(b.id),
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Could not save the mapping"),
  });

  // Mid-wizard means a file has been picked and the transaction hasn't yet
  // reached the reconciliation-review page — losing that progress means
  // re-uploading and re-mapping columns from scratch.
  const midWizard = file !== null && !uploadWithProfile.isSuccess && !saveAndUpload.isSuccess;

  useEffect(() => {
    if (!midWizard) return;
    function handler(e: BeforeUnloadEvent) {
      e.preventDefault();
    }
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [midWizard]);

  const columnOptions =
    detected?.headers.map((h, i) => ({ i, label: `[${i}] ${h || "(blank)"}` })) ?? [];
  const setCol = (field: string, value: string) =>
    setMap((cur) => ({ ...cur, [field]: value === "" ? undefined! : Number(value) }));

  return (
    <div className="max-w-3xl space-y-6">
      <h1 className="text-lg font-semibold">Upload bank statement</h1>

      <div className="flex flex-wrap items-end gap-3">
        <label className="space-y-1">
          <span className="text-sm font-medium">Account</span>
          <select
            className="block rounded border bg-background text-foreground px-3 py-1.5 text-sm"
            aria-label="Account"
            value={accountId}
            onChange={(e) => {
              setAccountId(e.target.value ? Number(e.target.value) : "");
              setProfileId("");
              setDetected(null);
            }}
          >
            <option value="">Select account…</option>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>
                {a.name}
              </option>
            ))}
          </select>
        </label>

        <label className="space-y-1">
          <span className="text-sm font-medium">Statement file (CSV or XLSX)</span>
          <Input
            type="file"
            accept=".csv,.xlsx"
            aria-label="Statement file"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          />
        </label>
      </div>

      {accountId !== "" && file && (
        <>
          {profiles.length > 0 && (
            <div className="space-y-2 rounded border p-3">
              <p className="text-sm font-medium">Use a saved mapping</p>
              <div className="flex flex-wrap items-end gap-3">
                <select
                  className="rounded border bg-background text-foreground px-3 py-1.5 text-sm"
                  aria-label="Saved profile"
                  value={profileId}
                  onChange={(e) => setProfileId(e.target.value ? Number(e.target.value) : "")}
                >
                  <option value="">Select mapping…</option>
                  {profiles.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
                </select>
                <Button
                  type="button"
                  disabled={profileId === "" || uploadWithProfile.isPending}
                  onClick={() => uploadWithProfile.mutate()}
                >
                  Upload &amp; review
                </Button>
              </div>
            </div>
          )}

          <div className="space-y-3 rounded border p-3">
            <p className="text-sm font-medium">
              {profiles.length > 0 ? "…or create a new mapping" : "Map the columns for this bank"}
            </p>
            <div className="flex flex-wrap items-end gap-3">
              <label className="space-y-1">
                <span className="text-sm">Header row (0-based)</span>
                <Input
                  className="w-24"
                  inputMode="numeric"
                  aria-label="Header row index"
                  value={headerRowIndex}
                  onChange={(e) => setHeaderRowIndex(e.target.value)}
                />
              </label>
              <Button
                type="button"
                variant="secondary"
                disabled={detect.isPending}
                onClick={() => detect.mutate()}
              >
                Detect columns
              </Button>
            </div>

            {detected && (
              <div className="space-y-3" data-testid="mapping-wizard">
                <p className="text-muted-foreground text-xs">
                  Detected: {detected.headers.map((h, i) => `[${i}] ${h}`).join("  ·  ")}
                </p>

                {(
                  ["dateColumn", "narrationColumn", "referenceColumn", "balanceColumn"] as Field[]
                ).map((field) => (
                  <label key={field} className="flex items-center gap-2 text-sm">
                    <span className="w-40">{field.replace("Column", "")}</span>
                    <select
                      className="rounded border bg-background text-foreground px-2 py-1 text-sm"
                      aria-label={field}
                      value={map[field] ?? ""}
                      onChange={(e) => setCol(field, e.target.value)}
                    >
                      <option value="">
                        {field === "referenceColumn" || field === "balanceColumn"
                          ? "none"
                          : "select…"}
                      </option>
                      {columnOptions.map((o) => (
                        <option key={o.i} value={o.i}>
                          {o.label}
                        </option>
                      ))}
                    </select>
                  </label>
                ))}

                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={singleAmount}
                    onChange={(e) => setSingleAmount(e.target.checked)}
                  />
                  One signed amount column (debit is negative)
                </label>

                {singleAmount ? (
                  <label className="flex items-center gap-2 text-sm">
                    <span className="w-40">amount</span>
                    <select
                      className="rounded border bg-background text-foreground px-2 py-1 text-sm"
                      aria-label="amountColumn"
                      value={map.amountColumn ?? ""}
                      onChange={(e) => setCol("amountColumn", e.target.value)}
                    >
                      <option value="">select…</option>
                      {columnOptions.map((o) => (
                        <option key={o.i} value={o.i}>
                          {o.label}
                        </option>
                      ))}
                    </select>
                  </label>
                ) : (
                  (["debitColumn", "creditColumn"] as const).map((field) => (
                    <label key={field} className="flex items-center gap-2 text-sm">
                      <span className="w-40">{field.replace("Column", "")}</span>
                      <select
                        className="rounded border bg-background text-foreground px-2 py-1 text-sm"
                        aria-label={field}
                        value={map[field] ?? ""}
                        onChange={(e) => setCol(field, e.target.value)}
                      >
                        <option value="">select…</option>
                        {columnOptions.map((o) => (
                          <option key={o.i} value={o.i}>
                            {o.label}
                          </option>
                        ))}
                      </select>
                    </label>
                  ))
                )}

                <div className="flex flex-wrap items-end gap-3">
                  <label className="space-y-1">
                    <span className="text-sm">Date format(s)</span>
                    <Input
                      className="w-48"
                      aria-label="Date formats"
                      value={dateFormats}
                      onChange={(e) => setDateFormats(e.target.value)}
                    />
                  </label>
                  <label className="space-y-1">
                    <span className="text-sm">Mapping name</span>
                    <Input
                      aria-label="Mapping name"
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                    />
                  </label>
                  <Button
                    type="button"
                    disabled={name.trim() === "" || saveAndUpload.isPending}
                    onClick={() => saveAndUpload.mutate()}
                  >
                    Save mapping &amp; review
                  </Button>
                </div>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}
