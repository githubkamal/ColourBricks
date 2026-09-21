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
import {
  createBankStatementProfile,
  detectStatementColumns,
  listBankStatementProfiles,
  updateBankStatementProfile,
  uploadStatement,
  type BankStatementProfile,
  type DetectedColumns,
} from "./api";

type Field = "dateColumn" | "narrationColumn" | "referenceColumn" | "balanceColumn";

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
    queryFn: () => listAccounts(),
  });
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
                  Detected: {detected.headers.map((h, i) => `[${i}] ${h}`).join("  ·  ")}
                </p>

                {(
                  ["dateColumn", "narrationColumn", "referenceColumn", "balanceColumn"] as Field[]
                ).map((field) => (
                  <label key={field} className="flex items-center gap-2 text-sm">
                    <span className="w-40">{field.replace("Column", "")}</span>
                    <Select
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
                    </Select>
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
                    <Select
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
                    </Select>
                  </label>
                ) : (
                  (["debitColumn", "creditColumn"] as const).map((field) => (
                    <label key={field} className="flex items-center gap-2 text-sm">
                      <span className="w-40">{field.replace("Column", "")}</span>
                      <Select
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
                      </Select>
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
