"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useId, useRef, useState, type KeyboardEvent } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { cn } from "@/lib/utils";
import { createParty, searchParties } from "./api";
import type { NearDuplicate, PartySearchItem, PartyType } from "./types";

const RECENT_KEY = "cb.recentParties";

interface RecentParty {
  id: number;
  name: string;
}

function readRecent(): RecentParty[] {
  try {
    const raw = window.localStorage.getItem(RECENT_KEY);
    return raw ? (JSON.parse(raw) as RecentParty[]) : [];
  } catch {
    return [];
  }
}

function rememberRecent(party: RecentParty) {
  try {
    const next = [party, ...readRecent().filter((p) => p.id !== party.id)].slice(0, 5);
    window.localStorage.setItem(RECENT_KEY, JSON.stringify(next));
  } catch {
    /* storage unavailable — recents are a convenience only */
  }
}

export interface PartyPickerProps {
  type?: PartyType;
  /** The currently selected party, if any. */
  selected: PartySearchItem | null;
  onSelect: (party: PartySearchItem | null) => void;
  label?: string;
}

export function PartyPicker({ type, selected, onSelect, label }: PartyPickerProps) {
  const queryClient = useQueryClient();
  const [term, setTerm] = useState("");
  const [debounced, setDebounced] = useState("");
  const [open, setOpen] = useState(false);
  const [mode, setMode] = useState<"search" | "adding">("search");
  const [nearDuplicates, setNearDuplicates] = useState<NearDuplicate[]>([]);
  const [creating, setCreating] = useState(false);
  const [recent] = useState<RecentParty[]>(() =>
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
    queryKey: ["party-search", { type, q: debounced }],
    queryFn: () => searchParties(debounced, type),
    enabled: open && debounced.length >= 2,
  });

  function choose(party: PartySearchItem) {
    onSelect(party);
    rememberRecent({ id: party.id, name: party.name });
    setOpen(false);
    setTerm("");
    setMode("search");
    setNearDuplicates([]);
    setHighlightedIndex(-1);
  }

  async function submitNew(confirm: boolean) {
    const name = term.trim();
    if (!name) return;
    setCreating(true);
    try {
      const outcome = await createParty(
        { name, types: [type ?? "Vendor"] },
        confirm || nearDuplicates.length > 0 ? confirm : false,
      );

      if (outcome.kind === "created") {
        await queryClient.invalidateQueries({ queryKey: ["parties"] });
        choose({ ...outcome.party });
        toast.success(`${outcome.party.name} added`);
      } else if (outcome.kind === "needs-confirmation") {
        setNearDuplicates(outcome.nearDuplicates);
        setHighlightedIndex(-1);
      } else {
        toast.message(`"${name}" already exists — selecting it`);
        choose({ id: outcome.existingId, name, types: [type ?? "Vendor"], category: null, isActive: true });
      }
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not add the party");
    } finally {
      setCreating(false);
    }
  }

  const showRecent = debounced.length < 2 && recent.length > 0;
  const recentCount = showRecent ? recent.length : 0;
  const showAddNew = term.trim().length > 0;
  const addNewIndex = recentCount + results.length;
  const optionCount =
    nearDuplicates.length > 0 ? nearDuplicates.length : addNewIndex + (showAddNew ? 1 : 0);

  function selectByIndex(index: number) {
    if (nearDuplicates.length > 0) {
      const d = nearDuplicates[index];
      if (d) choose({ id: d.id, name: d.name, types: d.types, category: null, isActive: true });
      return;
    }
    if (index < recentCount) {
      const r = recent[index];
      if (r) choose({ id: r.id, name: r.name, types: [], category: null, isActive: true });
      return;
    }
    if (index < addNewIndex) {
      const party = results[index - recentCount];
      if (party) choose(party);
      return;
    }
    if (index === addNewIndex && showAddNew) {
      setMode("adding");
      void submitNew(false);
    }
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
            {selected.types.length > 0 && (
              <span className="text-muted-foreground"> · {selected.types.join(", ")}</span>
            )}
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
            setMode("search");
            setNearDuplicates([]);
            setHighlightedIndex(-1);
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={handleInputKeyDown}
          placeholder="Search or add…"
          aria-label={label ?? "Party"}
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
                  onClick={() => choose({ id: d.id, name: d.name, types: d.types, category: null, isActive: true })}
                >
                  {d.name} <span className="text-muted-foreground">· {d.types.join(", ")}</span>
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
                      onClick={() => choose({ id: r.id, name: r.name, types: [], category: null, isActive: true })}
                    >
                      {r.name}
                    </button>
                  ))}
                </div>
              )}

              {isFetching && <p className="text-muted-foreground p-2">Searching…</p>}

              {results.map((party, i) => (
                <button
                  key={party.id}
                  id={`${listboxId}-option-${recentCount + i}`}
                  type="button"
                  className={cn(
                    "hover:bg-secondary block w-full rounded px-2 py-1 text-left",
                    recentCount + i === highlightedIndex && "bg-secondary",
                  )}
                  onClick={() => choose(party)}
                >
                  {party.name}
                  {party.types.length > 0 && (
                    <span className="text-muted-foreground"> · {party.types.join(", ")}</span>
                  )}
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
                  onClick={() => {
                    setMode("adding");
                    void submitNew(false);
                  }}
                >
                  + Add &ldquo;{term.trim()}&rdquo;{type ? ` as a ${type.toLowerCase()}` : ""}
                </button>
              )}

              {debounced.length >= 2 &&
                !isFetching &&
                results.length === 0 &&
                mode === "search" && <p className="text-muted-foreground px-2 py-1">No matches.</p>}
            </>
          )}
        </div>
      )}
    </div>
  );
}
