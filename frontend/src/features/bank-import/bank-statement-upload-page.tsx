"use client";

import {
  AllCommunityModule,
  colorSchemeDark,
  colorSchemeLight,
  ModuleRegistry,
  themeQuartz,
  type ColDef,
  type ICellRendererParams,
} from "ag-grid-community";
import { AgGridReact } from "ag-grid-react";
import { FileSpreadsheet, UploadCloud, X } from "lucide-react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { useTheme } from "@/features/shell/theme-context";
import { SubmitButton } from "@/components/ui/submit-button";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import { cn } from "@/lib/utils";
import {
  createBankImportFromRows,
  createBankStatementProfile,
  detectStatementColumns,
  listBankStatementProfiles,
  previewBankImport,
  updateBankStatementProfile,
  type BankStatementProfile,
  type DetectedColumns,
  type PreviewBankImportRow,
} from "./api";

ModuleRegistry.registerModules([AllCommunityModule]);

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

type GridRow = PreviewBankImportRow & { _checked: boolean };

function FileDropzone({
  file,
  disabled,
  onFileChange,
}: {
  file: File | null;
  disabled: boolean;
  onFileChange: (file: File | null) => void;
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);

  if (file) {
    return (
      <div className="bg-card flex w-full max-w-md items-center gap-3 rounded-lg border p-3">
        <FileSpreadsheet className="text-primary size-8 shrink-0" aria-hidden="true" />
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium">{file.name}</p>
          <p className="text-muted-foreground text-xs">{(file.size / 1024).toFixed(1)} KB</p>
        </div>
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          aria-label="Remove file"
          onClick={() => {
            onFileChange(null);
            if (inputRef.current) inputRef.current.value = "";
          }}
        >
          <X className="size-4" aria-hidden="true" />
        </Button>
      </div>
    );
  }

  return (
    <label
      className={cn(
        "flex w-full max-w-md cursor-pointer flex-col items-center justify-center gap-1.5 rounded-lg border-2 border-dashed p-6 text-center transition-colors",
        dragOver ? "border-primary bg-primary/5" : "border-border hover:bg-muted/40",
        disabled && "pointer-events-none cursor-not-allowed opacity-50",
      )}
      onDragOver={(e) => {
        e.preventDefault();
        setDragOver(true);
      }}
      onDragLeave={() => setDragOver(false)}
      onDrop={(e) => {
        e.preventDefault();
        setDragOver(false);
        onFileChange(e.dataTransfer.files?.[0] ?? null);
      }}
    >
      <UploadCloud className="text-muted-foreground size-7" aria-hidden="true" />
      <p className="text-sm font-medium">Drop a statement file here, or click to browse</p>
      <p className="text-muted-foreground text-xs">CSV, XLS or XLSX</p>
      <input
        ref={inputRef}
        type="file"
        accept=".csv,.xlsx,.xls"
        aria-label="Statement file"
        disabled={disabled}
        className="sr-only"
        onChange={(e) => onFileChange(e.target.files?.[0] ?? null)}
      />
    </label>
  );
}

export function BankStatementUploadPage() {
  const router = useRouter();
  const { mode } = useTheme();
  const gridTheme = useMemo(
    () => themeQuartz.withPart(mode === "dark" ? colorSchemeDark : colorSchemeLight),
    [mode],
  );

  const [accountId, setAccountId] = useState<number | "">("");
  const [file, setFile] = useState<File | null>(null);

  const [profileId, setProfileId] = useState<number | "">("");
  const [editingProfileId, setEditingProfileId] = useState<number | null>(null);
  // Set when a saved mapping is being reused as-is (no edits) — Upload skips saving a
  // profile in that case, unlike editingProfileId which means "save changes to this id".
  const [usingProfileId, setUsingProfileId] = useState<number | null>(null);

  const [headerRowIndex, setHeaderRowIndex] = useState("0");
  const [detected, setDetected] = useState<DetectedColumns | null>(null);
  const [name, setName] = useState("");
  const [singleAmount, setSingleAmount] = useState(false);
  const [dateFormats, setDateFormats] = useState("dd/MM/yyyy");
  const [map, setMap] = useState<Record<string, number>>({});

  const [selection, setSelection] = useState<{ key: string | null; ids: Set<number> }>({
    key: null,
    ids: new Set(),
  });
  const [validated, setValidated] = useState<{ key: string; rows: PreviewBankImportRow[] } | null>(
    null,
  );

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

  function applyProfileMapping(p: BankStatementProfile, editing: boolean) {
    setEditingProfileId(editing ? p.id : null);
    setUsingProfileId(editing ? null : p.id);
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
    if (editing) detect.mutate(p.headerRowIndex);
  }

  function startNewMapping() {
    setEditingProfileId(null);
    setUsingProfileId(null);
    setName("");
    setHeaderRowIndex("0");
    setDetected(null);
    setMap({});
  }

  // ── preview / validate / upload ──────────────────────────────────────────────

  function mappingFields() {
    return {
      headerRowIndex: Number(headerRowIndex),
      dateColumn: map.dateColumn,
      narrationColumn: map.narrationColumn,
      referenceColumn: map.referenceColumn,
      balanceColumn: map.balanceColumn,
      singleAmountColumn: singleAmount,
      amountColumn: singleAmount ? map.amountColumn : undefined,
      debitColumn: singleAmount ? undefined : map.debitColumn,
      creditColumn: singleAmount ? undefined : map.creditColumn,
      dateFormats: dateFormats.trim(),
    };
  }

  const mappingReady = map.dateColumn !== undefined && map.narrationColumn !== undefined;
  const fileKey = file ? `${file.name}:${file.size}:${file.lastModified}` : null;
  const mappingSignature = mappingReady
    ? JSON.stringify({ headerRowIndex, map, singleAmount, dateFormats: dateFormats.trim() })
    : null;
  const previewKey =
    accountId !== "" && fileKey && mappingSignature
      ? `${accountId}|${fileKey}|${mappingSignature}`
      : null;

  const preview = useQuery({
    queryKey: ["bank-import-preview", previewKey],
    queryFn: () =>
      previewBankImport({
        file: file!,
        accountId: Number(accountId),
        checkExisting: false,
        ...mappingFields(),
      }),
    enabled: previewKey !== null,
  });

  const validate = useMutation({
    mutationFn: () =>
      previewBankImport({
        file: file!,
        accountId: Number(accountId),
        checkExisting: true,
        ...mappingFields(),
      }),
    onSuccess: (result) => {
      setValidated({ key: previewKey!, rows: result });
      // Rows already in the ledger default to unchecked once we know about them —
      // the user must deliberately re-check to re-import.
      setSelection((cur) => {
        const ids = new Set(cur.ids);
        for (const r of result) {
          if (r.existsInDb) ids.delete(r.sourceLineNo);
        }
        return { key: cur.key, ids };
      });
    },
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Validation failed"),
  });

  const isValidated = validated !== null && validated.key === previewKey;
  const rows: PreviewBankImportRow[] = isValidated ? validated.rows : (preview.data ?? []);

  // Default selection (everything parseable, checked) whenever a genuinely new dataset
  // shows up — keyed on the mapping+file signature alone, not on validated/draft, so
  // that validating (or a checkbox toggle reverting validation) never wipes out the
  // user's choices. Derived during render, same pattern Sidebar uses for its own
  // "track a computed value" state, rather than an extra render-cycle effect.
  if (rows.length > 0 && selection.key !== previewKey) {
    setSelection({
      key: previewKey,
      ids: new Set(rows.filter((r) => !r.parseError).map((r) => r.sourceLineNo)),
    });
  }

  function toggleRow(lineNo: number) {
    setSelection((cur) => {
      const ids = new Set(cur.ids);
      if (ids.has(lineNo)) ids.delete(lineNo);
      else ids.add(lineNo);
      return { key: cur.key, ids };
    });
    if (isValidated) setValidated(null);
  }

  function selectAll() {
    setSelection((cur) => ({
      key: cur.key,
      ids: new Set(rows.filter((r) => !r.parseError).map((r) => r.sourceLineNo)),
    }));
    if (isValidated) setValidated(null);
  }

  function selectNone() {
    setSelection((cur) => ({ key: cur.key, ids: new Set() }));
    if (isValidated) setValidated(null);
  }

  const go = (batchId: number) => router.push(`/reconciliation/imports/${batchId}`);

  const upload = useMutation({
    mutationFn: async () => {
      const checked = rows.filter((r) => selection.ids.has(r.sourceLineNo));
      const profilePayload = {
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
      if (editingProfileId) {
        await updateBankStatementProfile(editingProfileId, profilePayload);
      } else if (!usingProfileId && name.trim() !== "") {
        await createBankStatementProfile({ accountId: Number(accountId), ...profilePayload });
      }
      return createBankImportFromRows(Number(accountId), file!.name, checked);
    },
    onSuccess: (b) => go(b.id),
    onError: (e) =>
      toast.error(
        e instanceof ApiError ? (e.fieldErrors?.rows?.[0] ?? e.message) : "Upload failed",
      ),
  });

  // Mid-wizard means a file has been picked and the transaction hasn't yet
  // reached the reconciliation-review page — losing that progress means
  // re-uploading and re-mapping columns from scratch.
  const midWizard = file !== null && !upload.isSuccess;

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

  const gridRows: GridRow[] = rows.map((r) => ({
    ...r,
    _checked: selection.ids.has(r.sourceLineNo),
  }));
  const parsedDates = rows
    .map((r) => r.valueDate)
    .filter((d): d is string => d !== null)
    .sort();
  const dateRangeLabel =
    parsedDates.length > 0
      ? `${formatDate(parsedDates[0])} → ${formatDate(parsedDates[parsedDates.length - 1])}`
      : null;
  const errorCount = rows.filter((r) => r.parseError).length;

  const columnDefs: ColDef<GridRow>[] = [
    {
      headerName: "",
      colId: "select",
      width: 44,
      sortable: false,
      filter: false,
      resizable: false,
      cellRenderer: (p: ICellRendererParams<GridRow>) =>
        p.data ? (
          <input
            type="checkbox"
            aria-label={`Select row ${p.data.sourceLineNo}`}
            checked={p.data._checked}
            disabled={!!p.data.parseError}
            onChange={() => toggleRow(p.data!.sourceLineNo)}
          />
        ) : null,
    },
    { headerName: "#", field: "sourceLineNo", width: 64 },
    {
      headerName: "Date",
      field: "valueDate",
      width: 120,
      valueFormatter: (p) => (p.value ? formatDate(p.value) : "—"),
    },
    { headerName: "Narration", field: "narration", flex: 1, minWidth: 180 },
    {
      headerName: "Reference",
      field: "bankReference",
      width: 120,
      valueFormatter: (p) => p.value ?? "—",
    },
    {
      headerName: "Debit",
      field: "debit",
      width: 120,
      type: "rightAligned",
      valueFormatter: (p) => (p.value > 0 ? formatINR(p.value) : "—"),
    },
    {
      headerName: "Credit",
      field: "credit",
      width: 120,
      type: "rightAligned",
      valueFormatter: (p) => (p.value > 0 ? formatINR(p.value) : "—"),
    },
    {
      headerName: "Balance",
      field: "balance",
      width: 120,
      type: "rightAligned",
      valueFormatter: (p) => (p.value != null ? formatINR(p.value) : "—"),
    },
    {
      headerName: "Status",
      colId: "status",
      width: 160,
      valueGetter: (p) =>
        p.data?.parseError
          ? `Parse error: ${p.data.parseError}`
          : p.data?.existsInDb
            ? "Already in ledger"
            : "New",
    },
  ];

  const showMappingCard = usingProfileId === null || editingProfileId !== null;
  // Reusing a saved profile untouched skips the save call entirely, so it needs no
  // name; a fresh mapping or an edit always saves a profile, so it always needs one —
  // same requirement the mapping-name field has always carried.
  const canUpload =
    selection.ids.size > 0 && !upload.isPending && (usingProfileId !== null || name.trim() !== "");

  return (
    <div className="max-w-5xl space-y-6">
      <h1 className="text-lg font-semibold">Upload bank statement</h1>

      <div className="grid gap-4 sm:grid-cols-[minmax(12rem,16rem)_1fr] sm:items-start">
        <label className="space-y-1">
          <span className="text-sm font-medium">Account</span>
          <Select
            className="w-full"
            aria-label="Account"
            value={accountId}
            onChange={(e) => {
              setAccountId(e.target.value ? Number(e.target.value) : "");
              setProfileId("");
              startNewMapping();
            }}
          >
            <option value="">Select account…</option>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>
                {a.name}
              </option>
            ))}
          </Select>
          {accountId !== "" && (
            <span className="text-muted-foreground block text-xs">
              Account number {selectedAccount?.accountNumber ?? "—"}
            </span>
          )}
        </label>

        <div className="min-w-0 space-y-1">
          <span className="text-sm font-medium">Statement file</span>
          <FileDropzone
            file={file}
            disabled={accountId === ""}
            onFileChange={(f) => {
              setFile(f);
              setProfileId("");
              startNewMapping();
            }}
          />
          {accountId === "" && (
            <span className="text-muted-foreground block text-xs">Select an account first</span>
          )}
        </div>
      </div>

      {accountId !== "" && file && (
        <>
          {profiles.length > 0 && usingProfileId === null && editingProfileId === null && (
            <div className="bg-card space-y-3 rounded border p-3">
              <p className="text-sm font-medium">Use a saved mapping</p>
              <div className="flex flex-wrap items-center justify-between gap-3">
                {profiles.length === 1 ? (
                  <span className="text-sm">{profiles[0].name}</span>
                ) : (
                  <Select
                    className="min-w-48"
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
                )}
                <div className="flex flex-wrap items-center gap-2">
                  <Button
                    type="button"
                    disabled={profiles.length > 1 && profileId === ""}
                    onClick={() => {
                      const p =
                        profiles.length === 1
                          ? profiles[0]
                          : profiles.find((x) => x.id === profileId);
                      if (p) applyProfileMapping(p, false);
                    }}
                  >
                    Use this mapping
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    disabled={profiles.length > 1 && profileId === ""}
                    onClick={() => {
                      const p =
                        profiles.length === 1
                          ? profiles[0]
                          : profiles.find((x) => x.id === profileId);
                      if (p) applyProfileMapping(p, true);
                    }}
                  >
                    Edit mapping
                  </Button>
                </div>
              </div>
            </div>
          )}

          {usingProfileId !== null && (
            <div className="bg-card flex flex-wrap items-center justify-between gap-3 rounded border p-3">
              <p className="text-sm">
                Using saved mapping <span className="font-medium">{name}</span>
              </p>
              <Button type="button" variant="outline" size="sm" onClick={startNewMapping}>
                Map columns manually instead
              </Button>
            </div>
          )}

          {showMappingCard && (
            <div className="bg-card space-y-3 rounded border p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="text-sm font-medium">
                  {editingProfileId
                    ? `Editing mapping "${name}"`
                    : profiles.length > 0
                      ? "…or create a new mapping"
                      : "Map the columns for this bank"}
                </p>
                {editingProfileId && (
                  <Button type="button" variant="outline" size="sm" onClick={startNewMapping}>
                    Start a new mapping instead
                  </Button>
                )}
              </div>
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
                <SubmitButton
                  type="button"
                  variant="secondary"
                  mutation={detect}
                  pendingLabel="Reading the file…"
                  successLabel="Columns detected"
                  errorLabel="Could not read the file"
                  onClick={() => detect.mutate(Number(headerRowIndex))}
                >
                  Detect columns
                </SubmitButton>
              </div>

              {detected && (
                <div className="space-y-3" data-testid="mapping-wizard">
                  <p className="text-muted-foreground text-xs">
                    Pick which field each column holds. The sample rows below fill in once a column
                    is mapped to a field.
                  </p>

                  <div data-table-scroll className="overflow-x-auto rounded border">
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
                                {fieldForColumn(i) ? (row[i] ?? "") : ""}
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

                  <div className="grid gap-3 sm:grid-cols-2 sm:items-end">
                    <label className="space-y-1">
                      <span className="text-sm">Date format(s)</span>
                      <Input
                        className="w-full"
                        aria-label="Date formats"
                        value={dateFormats}
                        onChange={(e) => setDateFormats(e.target.value)}
                      />
                    </label>
                    <label className="space-y-1">
                      <span className="text-sm">Mapping name</span>
                      <Input
                        className="w-full"
                        aria-label="Mapping name"
                        value={name}
                        onChange={(e) => setName(e.target.value)}
                      />
                    </label>
                  </div>
                </div>
              )}
            </div>
          )}

          {previewKey !== null && (
            <div className="bg-card space-y-3 rounded border p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <p className="text-sm font-medium">Preview &amp; validate</p>
                  {rows.length > 0 && (
                    <p className="text-muted-foreground text-xs">
                      {rows.length} row(s)
                      {dateRangeLabel ? ` · ${dateRangeLabel}` : ""}
                      {errorCount > 0 ? ` · ${errorCount} parse error(s)` : ""}
                      {isValidated ? ` · ${selection.ids.size} selected` : ""}
                    </p>
                  )}
                </div>
                {rows.length > 0 && (
                  <div className="flex shrink-0 items-center gap-2">
                    <Button type="button" variant="outline" size="sm" onClick={selectAll}>
                      Select all
                    </Button>
                    <Button type="button" variant="outline" size="sm" onClick={selectNone}>
                      Select none
                    </Button>
                  </div>
                )}
              </div>

              {preview.isLoading && (
                <p className="text-muted-foreground text-sm">Reading the file…</p>
              )}
              {preview.isError && (
                <p className="text-destructive text-sm">
                  {preview.error instanceof ApiError
                    ? preview.error.message
                    : "Could not read the file"}
                </p>
              )}

              {rows.length > 0 && (
                <div className="h-96">
                  <AgGridReact<GridRow>
                    theme={gridTheme}
                    rowData={gridRows}
                    columnDefs={columnDefs}
                    getRowId={(p) => String(p.data.sourceLineNo)}
                    rowClassRules={{
                      "bg-attention/10": (p) => p.data?.existsInDb === true,
                    }}
                  />
                </div>
              )}

              <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-3">
                <p className="text-muted-foreground text-xs">
                  {isValidated
                    ? "Checked against the ledger — uncheck anything you don't want imported."
                    : "Validate first: it flags rows already in the ledger before anything is imported."}
                </p>
                <div className="flex flex-wrap items-center gap-3">
                  {isValidated && !canUpload && name.trim() === "" && usingProfileId === null && (
                    <span className="text-attention text-sm">Name this mapping to save it</span>
                  )}
                  {!isValidated ? (
                    <SubmitButton
                      type="button"
                      disabled={rows.length === 0}
                      mutation={validate}
                      pendingLabel="Validating…"
                      successLabel="Validated"
                      errorLabel="Validation failed"
                      onClick={() => validate.mutate()}
                    >
                      Validate
                    </SubmitButton>
                  ) : (
                    <SubmitButton
                      type="button"
                      disabled={!canUpload}
                      mutation={upload}
                      pendingLabel="Uploading…"
                      successLabel="Uploaded"
                      errorLabel="Upload failed"
                      onClick={() => upload.mutate()}
                    >
                      Upload {selection.ids.size} transaction(s)
                    </SubmitButton>
                  )}
                </div>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
