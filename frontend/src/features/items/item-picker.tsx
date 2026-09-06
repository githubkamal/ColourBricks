"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useId, useRef, useState, type KeyboardEvent } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { formatINR } from "@/lib/format";
import { cn } from "@/lib/utils";
import { createItem, searchItems } from "./api";
import type { ItemNearDuplicate, ItemSearchItem } from "./types";

const RECENT_KEY = "cb.recentItems";

interface RecentItem {
  id: number;
  name: string;
}

function readRecent(): RecentItem[] {
  try {
    const raw = window.localStorage.getItem(RECENT_KEY);
    return raw ? (JSON.parse(raw) as RecentItem[]) : [];
  } catch {
    return [];
  }
}

function rememberRecent(item: RecentItem) {
  try {
    const next = [item, ...readRecent().filter((i) => i.id !== item.id)].slice(0, 5);
    window.localStorage.setItem(RECENT_KEY, JSON.stringify(next));
  } catch {
    /* storage unavailable — recents are a convenience only */
  }
}

export interface ItemPickerProps {
  selected: ItemSearchItem | null;
  onSelect: (item: ItemSearchItem | null) => void;
  /** Unit an inline-created item is given until it is edited in the master (BRD §15). */
  defaultUnit?: string;
  label?: string;
  /**
   * Accessible name when no visible `label` is wanted — e.g. one picker per row
   * in a table that already has an "Item" column header. Falls back to `label`,
   * then "Item".
   */
  ariaLabel?: string;
}

export function ItemPicker({
  selected,
  onSelect,
  defaultUnit = "Nos",
  label,
  ariaLabel,
}: ItemPickerProps) {
  const queryClient = useQueryClient();
  const [term, setTerm] = useState("");
  const [debounced, setDebounced] = useState("");
  const [open, setOpen] = useState(false);
  const [nearDuplicates, setNearDuplicates] = useState<ItemNearDuplicate[]>([]);
  const [creating, setCreating] = useState(false);
  const [recent] = useState<RecentItem[]>(() =>
    typeof window === "undefined" ? [] : readRecent(),
  );
  const [highlightedIndex, setHighlightedIndex] = useState(-1);
  const containerRef = useRef<HTMLDivElement>(null);
  const listboxId = useId();

  useEffect(() => {
    const id = window.setTimeout(() => setDebounced(term.trim()), 200);
    return () => window.clearTimeout(id);
  }, [term]);

  useEffect(() => {
    function onClickAway(event: MouseEvent) {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", onClickAway);
    return () => document.removeEventListener("mousedown", onClickAway);
  }, []);

  const { data: results = [], isFetching } = useQuery({
    queryKey: ["item-search", { q: debounced }],
    queryFn: () => searchItems(debounced),
    enabled: open && debounced.length >= 2,
  });

  function choose(item: ItemSearchItem) {
    onSelect(item);
    rememberRecent({ id: item.id, name: item.name });
    setOpen(false);
    setTerm("");
    setNearDuplicates([]);
    setHighlightedIndex(-1);
  }

  async function submitNew(confirm: boolean) {
    const name = term.trim();
    if (!name) return;
    setCreating(true);
    try {
      const outcome = await createItem(
        { name, unit: defaultUnit, defaultRate: 0, taxRate: 0 },
        confirm,
      );

      if (outcome.kind === "created") {
        await queryClient.invalidateQueries({ queryKey: ["items"] });
        choose(outcome.item);
        toast.success(`${outcome.item.name} added`);
      } else if (outcome.kind === "needs-confirmation") {
        setNearDuplicates(outcome.nearDuplicates);
        setHighlightedIndex(-1);
      } else {
        toast.message(`"${name}" already exists — selecting it`);
        choose({
          id: outcome.existingId,
          name,
          categoryName: null,
          unit: defaultUnit,
          defaultRate: 0,
          taxRate: 0,
          isActive: true,
        });
      }
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not add the item");
    } finally {
      setCreating(false);
    }
  }

  const optionLabel = (item: ItemSearchItem) =>
    `${item.name} · ${item.unit} · ${formatINR(item.defaultRate)}`;

  const showRecent = debounced.length < 2 && recent.length > 0;
  const recentCount = showRecent ? recent.length : 0;
  const showAddNew = term.trim().length > 0;
  const addNewIndex = recentCount + results.length;
  const optionCount =
    nearDuplicates.length > 0 ? nearDuplicates.length : addNewIndex + (showAddNew ? 1 : 0);

  function selectByIndex(index: number) {
    if (nearDuplicates.length > 0) {
      const d = nearDuplicates[index];
      if (d) {
        choose({
          id: d.id,
          name: d.name,
          categoryName: d.categoryName,
          unit: defaultUnit,
          defaultRate: 0,
          taxRate: 0,
          isActive: true,
        });
      }
      return;
    }
    if (index < recentCount) {
      const r = recent[index];
      if (r)
        choose({
          id: r.id,
          name: r.name,
          categoryName: null,
          unit: defaultUnit,
          defaultRate: 0,
          taxRate: 0,
          isActive: true,
        });
      return;
    }
    if (index < addNewIndex) {
      const item = results[index - recentCount];
      if (item) choose(item);
      return;
    }
    if (index === addNewIndex && showAddNew) void submitNew(false);
  }

  function handleInputKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === "Escape") {
      setOpen(false);
      setHighlightedIndex(-1);
      return;
    }
    if (!open || optionCount === 0) return;
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setHighlightedIndex((i) => Math.min(i + 1, optionCount - 1));
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setHighlightedIndex((i) => Math.max(i - 1, 0));
    } else if (event.key === "Enter" && highlightedIndex >= 0) {
      event.preventDefault();
      selectByIndex(highlightedIndex);
    }
  }

  return (
    <div ref={containerRef} className="relative">
      {label && <label className="mb-1 block text-sm font-medium">{label}</label>}

      {selected ? (
        <div className="bg-card flex items-center justify-between rounded border px-3 py-1.5 text-sm">
          <span>
            {selected.name}
            <span className="text-muted-foreground">
              {" "}
              · {selected.unit} · {formatINR(selected.defaultRate)}
            </span>
          </span>
          <Button
            type="button"
            variant="ghost"
            size="xs"
            onClick={() => {
              onSelect(null);
              setOpen(true);
            }}
          >
            Change
          </Button>
        </div>
      ) : (
        <Input
          value={term}
          onChange={(e) => {
            setTerm(e.target.value);
            setOpen(true);
            setNearDuplicates([]);
            setHighlightedIndex(-1);
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={handleInputKeyDown}
          placeholder="Search or add…"
          aria-label={ariaLabel ?? label ?? "Item"}
          role="combobox"
          aria-expanded={open}
          aria-controls={listboxId}
          aria-autocomplete="list"
          aria-activedescendant={
            highlightedIndex >= 0 ? `${listboxId}-option-${highlightedIndex}` : undefined
          }
        />
      )}

      {open && !selected && (
        <div
          id={listboxId}
          role="listbox"
          className="bg-popover absolute z-20 mt-1 w-full rounded border p-1 text-sm shadow-md"
        >
          {nearDuplicates.length > 0 ? (
            <div className="space-y-2 p-2">
              <p className="text-attention text-xs">Did you mean one of these?</p>
              {nearDuplicates.map((d, i) => (
                <button
                  key={d.id}
                  id={`${listboxId}-option-${i}`}
                  type="button"
                  className={cn(
                    "hover:bg-secondary block w-full rounded px-2 py-1 text-left",
                    i === highlightedIndex && "bg-secondary",
                  )}
                  onClick={() =>
                    choose({
                      id: d.id,
                      name: d.name,
                      categoryName: d.categoryName,
                      unit: defaultUnit,
                      defaultRate: 0,
                      taxRate: 0,
                      isActive: true,
                    })
                  }
                >
                  {d.name}
                  {d.categoryName && (
                    <span className="text-muted-foreground"> · {d.categoryName}</span>
                  )}
                </button>
              ))}
              <div className="flex gap-2 pt-1">
                <Button type="button" size="xs" disabled={creating} onClick={() => submitNew(true)}>
                  Create &ldquo;{term.trim()}&rdquo; anyway
                </Button>
                <Button
                  type="button"
                  size="xs"
                  variant="ghost"
                  onClick={() => {
                    setNearDuplicates([]);
                    setHighlightedIndex(-1);
                  }}
                >
                  Back
                </Button>
              </div>
            </div>
          ) : (
            <>
              {showRecent && (
                <div className="p-1">
                  <p className="text-muted-foreground px-2 pb-1 text-[11px] uppercase">Recent</p>
                  {recent.map((r, i) => (
                    <button
                      key={r.id}
                      id={`${listboxId}-option-${i}`}
                      type="button"
                      className={cn(
                        "hover:bg-secondary block w-full rounded px-2 py-1 text-left",
                        i === highlightedIndex && "bg-secondary",
                      )}
                      onClick={() =>
                        choose({
                          id: r.id,
                          name: r.name,
                          categoryName: null,
                          unit: defaultUnit,
                          defaultRate: 0,
                          taxRate: 0,
                          isActive: true,
                        })
                      }
                    >
                      {r.name}
                    </button>
                  ))}
                </div>
              )}

              {isFetching && <p className="text-muted-foreground p-2">Searching…</p>}

              {results.map((item, i) => (
                <button
                  key={item.id}
                  id={`${listboxId}-option-${recentCount + i}`}
                  type="button"
                  className={cn(
                    "hover:bg-secondary block w-full rounded px-2 py-1 text-left",
                    recentCount + i === highlightedIndex && "bg-secondary",
                  )}
                  onClick={() => choose(item)}
                >
                  {optionLabel(item)}
                </button>
              ))}

              {showAddNew && (
                <button
                  id={`${listboxId}-option-${addNewIndex}`}
                  type="button"
                  disabled={creating}
                  className={cn(
                    "text-primary block w-full rounded px-2 py-1.5 text-left font-medium",
                    "hover:bg-secondary",
                    addNewIndex === highlightedIndex && "bg-secondary",
                  )}
                  onClick={() => void submitNew(false)}
                >
                  + Add &ldquo;{term.trim()}&rdquo; as an item
                </button>
              )}

              {debounced.length >= 2 && !isFetching && results.length === 0 && (
                <p className="text-muted-foreground px-2 py-1">No matches.</p>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
}
