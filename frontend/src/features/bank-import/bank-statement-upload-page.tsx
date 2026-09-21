"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import { cn } from "@/lib/utils";
import {
  createBankStatementProfile,
  detectStatementColumns,
  listBankStatementProfiles,
  updateBankStatementProfile,
  uploadStatement,
  type BankStatementProfile,
  type DetectedColumns,
} from "./api";

type MappedField =
  | "dateColumn"
  | "narrationColumn"
  | "referenceColumn"
  | "balanceColumn"
  | "amountColumn"
  | "debitColumn"
  | "creditColumn";

const FIELD_LABELS: Record<MappedField, string> = {
  dateColumn: "Date",
  narrationColumn: "Narration",
  referenceColumn: "Reference",
  balanceColumn: "Balance",
  amountColumn: "Amount",
  debitColumn: "Debit",
  creditColumn: "Credit",
};

export function BankStatementUploadPage() {
  const router = useRouter();
  const [accountId, setAccountId] = useState<number | "">("");
  const [file, setFile] = useState<File | null>(null);
  const [profileId, setProfileId] = useState<number | "">("");
  const [editingProfileId, setEditingProfileId] = useState<number | null>(null);

  const [headerRowIndex, setHeaderRowIndex] = useState("0");
  const [detected, setDetected] = useState<DetectedColumns | null>(null);
  const [name, setName] = useState("");
  const [singleAmount, setSingleAmount] = useState(false);
  const [dateFormats, setDateFormats] = useState("dd/MM/yyyy");
  const [map, setMap] = useState<Record<string, number>>({});

  const { data: accounts = [] } = useQuery({
    queryKey: ["accounts", "banks"],
    queryFn: () => listAccounts("Bank"),
  });
  const selectedAccount = accounts.find((a) => a.id === accountId);
  const { data: profiles = [] } = useQuery({
    queryKey: ["bank-statement-profiles", accountId],
    queryFn: () => listBankStatementProfiles(Number(accountId)),
    enabled: accountId !== "",
  });

  // Once an account has exactly one saved mapping, there's nothing to pick —
  // use it automatically rather than making the user click through a dropdown
  // every time. With several saved mappings (e.g. the bank changed its export
  // layout at some point), the dropdown below still lets them choose.
  const effectiveProfileId = profileId === "" && profiles.length === 1 ? profiles[0].id : profileId;

  const detect = useMutation({
    mutationFn: (headerRow: number) => detectStatementColumns(file!, headerRow),
    onSuccess: (result) => {
      setDetected(result);
      // Editing a saved profile: `map` already holds its real column indices —
      // don't clobber them just because we re-ran detection (e.g. after
      // tweaking the header row). Starting fresh, though, the previous
      // guesses are meaningless for this file's layout, so force every field
      // back to "select…" rather than leaving stale/hardcoded indices that
      // look chosen but point at the wrong columns.
      if (!editingProfileId) setMap({});
    },
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Could not read the file"),
  });

  const go = (batchId: number) => router.push(`/reconciliation/imports/${batchId}`);

  const uploadWithProfile = useMutation({
    mutationFn: () => uploadStatement(Number(accountId), Number(effectiveProfileId), file!),
    onSuccess: (b) => go(b.id),
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Upload failed"),
  });

  const saveAndUpload = useMutation({
    mutationFn: async () => {
      const payload = {
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
      };
      const profile = editingProfileId
        ? await updateBankStatementProfile(editingProfileId, payload)
        : await createBankStatementProfile({ accountId: Number(accountId), ...payload });
      return uploadStatement(Number(accountId), profile.id, file!);
    },
    onSuccess: (b) => go(b.id),
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Could not save the mapping"),
  });

  function startNewMapping() {
    setEditingProfileId(null);
    setName("");
    setHeaderRowIndex("0");
    setDetected(null);
    setMap({});
  }

  function startEditMapping(p: BankStatementProfile) {
    setEditingProfileId(p.id);
    setName(p.name);
    setHeaderRowIndex(String(p.headerRowIndex));
    setSingleAmount(p.singleAmountColumn);
    setDateFormats(p.dateFormats);
    setMap({
      dateColumn: p.dateColumn,
      narrationColumn: p.narrationColumn,
      referenceColumn: p.referenceColumn ?? undefined!,
      balanceColumn: p.balanceColumn ?? undefined!,
      amountColumn: p.amountColumn ?? undefined!,
      debitColumn: p.debitColumn ?? undefined!,
      creditColumn: p.creditColumn ?? undefined!,
    });
    setDetected(null);
    detect.mutate(p.headerRowIndex);
  }

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

  const fieldsForColumns: MappedField[] = singleAmount
    ? ["dateColumn", "narrationColumn", "referenceColumn", "balanceColumn", "amountColumn"]
    : [
        "dateColumn",
        "narrationColumn",
        "referenceColumn",
        "balanceColumn",
        "debitColumn",
        "creditColumn",
      ];

  const fieldForColumn = (i: number) =>
    (Object.keys(map) as MappedField[]).find((f) => map[f] === i);

  function assignColumn(colIndex: number, field: string) {
    setMap((cur) => {
      const next = { ...cur };
      for (const f of Object.keys(next)) {
        if (next[f] === colIndex) delete next[f];
      }
      if (field) next[field] = colIndex;
      return next;
    });
  }

  return (
    <div className="max-w-3xl space-y-6">
      <h1 className="text-lg font-semibold">Upload bank statement</h1>

      <div className="flex flex-wrap items-end gap-3">
        <label className="space-y-1">
          <span className="text-sm font-medium">Account</span>
          <Select
            aria-label="Account"
            value={accountId}
            onChange={(e) => {
              setAccountId(e.target.value ? Number(e.target.value) : "");
              setProfileId("");
              setDetected(null);
              setMap({});
            }}
          >
            <option value="">Select account…</option>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>
                {a.name}
              </option>
            ))}
          </Select>
        </label>

        {accountId !== "" && (
          <div className="space-y-1">
            <span className="text-sm font-medium">Account number</span>
            <p className="text-sm">{selectedAccount?.accountNumber ?? "—"}</p>
          </div>
        )}

        <label className="space-y-1">
          <span className="text-sm font-medium">Statement file (CSV, XLS or XLSX)</span>
          <Input
            type="file"
            accept=".csv,.xlsx,.xls"
            aria-label="Statement file"
            disabled={accountId === ""}
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          />
          {accountId === "" && (
            <span className="text-muted-foreground block text-xs">Select an account first</span>
          )}
        </label>
      </div>

      {accountId !== "" && file && (
        <>
          {profiles.length > 0 && (
            <div className="bg-card space-y-2 rounded border p-3">
              <p className="text-sm font-medium">Use a saved mapping</p>
              {profiles.length === 1 ? (
                <div className="flex flex-wrap items-center gap-3">
                  <span className="text-sm">{profiles[0].name}</span>
                  <Button
                    type="button"
                    disabled={uploadWithProfile.isPending}
                    onClick={() => uploadWithProfile.mutate()}
                  >
                    Upload &amp; review
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    onClick={() => startEditMapping(profiles[0])}
                  >
                    Edit mapping
                  </Button>
                </div>
              ) : (
                <div className="flex flex-wrap items-end gap-3">
                  <Select
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
                  </Select>
                  <Button
                    type="button"
                    disabled={profileId === "" || uploadWithProfile.isPending}
                    onClick={() => uploadWithProfile.mutate()}
                  >
                    Upload &amp; review
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    disabled={profileId === ""}
                    onClick={() => {
                      const p = profiles.find((x) => x.id === profileId);
                      if (p) startEditMapping(p);
                    }}
                  >
                    Edit mapping
                  </Button>
                </div>
              )}
            </div>
          )}

          <div className="bg-card space-y-3 rounded border p-3">
            <p className="text-sm font-medium">
              {editingProfileId
                ? `Editing mapping "${name}"`
                : profiles.length > 0
                  ? "…or create a new mapping"
                  : "Map the columns for this bank"}
            </p>
            {editingProfileId && (
              <Button type="button" variant="ghost" size="xs" onClick={startNewMapping}>
                Cancel — start a new mapping instead
              </Button>
            )}
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
                onClick={() => detect.mutate(Number(headerRowIndex))}
              >
                Detect columns
              </Button>
            </div>

            {detected && (
              <div className="space-y-3" data-testid="mapping-wizard">
                <p className="text-muted-foreground text-xs">
                  Pick which field each column holds. The sample rows below show what&apos;s
                  actually in the file.
                </p>

                <div className="overflow-x-auto rounded border">
                  <table className="w-full text-left text-sm">
                    <thead>
                      <tr className="bg-muted/50">
                        {detected.headers.map((h, i) => (
                          <th key={i} className="min-w-36 border-b p-2 align-top font-normal">
                            <div className="text-muted-foreground mb-1 text-xs">
                              [{i}] {h || "(blank)"}
                            </div>
                            <Select
                              aria-label={`Field for column ${i}`}
                              className="h-7 w-full text-xs"
                              value={fieldForColumn(i) ?? ""}
                              onChange={(e) => assignColumn(i, e.target.value)}
                            >
                              <option value="">Not used</option>
                              {fieldsForColumns.map((f) => (
                                <option key={f} value={f}>
                                  {FIELD_LABELS[f]}
                                </option>
                              ))}
                            </Select>
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {detected.sampleRows.map((row, r) => (
                        <tr key={r} className="odd:bg-card even:bg-muted/20">
                          {detected.headers.map((_, i) => (
                            <td
                              key={i}
                              className={cn(
                                "border-b p-2",
                                fieldForColumn(i) && "bg-primary/5 font-medium",
                              )}
                            >
                              {row[i] ?? ""}
                            </td>
                          ))}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={singleAmount}
                    onChange={(e) => setSingleAmount(e.target.checked)}
                  />
                  One signed amount column (debit is negative)
                </label>

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
                    {editingProfileId ? "Save changes & review" : "Save mapping & review"}
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
