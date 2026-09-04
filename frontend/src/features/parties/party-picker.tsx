"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
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
  const containerRef = useRef<HTMLDivElement>(null);

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
      } else {
        toast.message(`"${name}" already exists — selecting it`);
        choose({ id: outcome.existingId, name, types: [type ?? "Vendor"], category: null });
      }
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not add the party");
    } finally {
      setCreating(false);
    }
  }

  return (
    <div ref={containerRef} className="relative">
      {label && <label className="mb-1 block text-sm font-medium">{label}</label>}

      {selected ? (
        <div className="flex items-center justify-between rounded border px-3 py-1.5 text-sm">
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
          }}
          onFocus={() => setOpen(true)}
          placeholder="Search or add…"
          aria-label={label ?? "Party"}
        />
      )}

      {open && !selected && (
        <div className="bg-popover absolute z-20 mt-1 w-full rounded border p-1 text-sm shadow-md">
          {nearDuplicates.length > 0 ? (
            <div className="space-y-2 p-2">
              <p className="text-attention text-xs">Did you mean one of these?</p>
              {nearDuplicates.map((d) => (
                <button
                  key={d.id}
                  type="button"
                  className="hover:bg-secondary block w-full rounded px-2 py-1 text-left"
                  onClick={() => choose({ id: d.id, name: d.name, types: d.types, category: null })}
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
                  onClick={() => setNearDuplicates([])}
                >
                  Back
                </Button>
              </div>
            </div>
          ) : (
            <>
              {debounced.length < 2 && recent.length > 0 && (
                <div className="p-1">
                  <p className="text-muted-foreground px-2 pb-1 text-[11px] uppercase">Recent</p>
                  {recent.map((r) => (
                    <button
                      key={r.id}
                      type="button"
                      className="hover:bg-secondary block w-full rounded px-2 py-1 text-left"
                      onClick={() => choose({ id: r.id, name: r.name, types: [], category: null })}
                    >
                      {r.name}
                    </button>
                  ))}
                </div>
              )}

              {isFetching && <p className="text-muted-foreground p-2">Searching…</p>}

              {results.map((party) => (
                <button
                  key={party.id}
                  type="button"
                  className="hover:bg-secondary block w-full rounded px-2 py-1 text-left"
                  onClick={() => choose(party)}
                >
                  {party.name}
                  {party.types.length > 0 && (
                    <span className="text-muted-foreground"> · {party.types.join(", ")}</span>
                  )}
                </button>
              ))}

              {term.trim().length > 0 && (
                <button
                  type="button"
                  disabled={creating}
                  className={cn(
                    "text-primary block w-full rounded px-2 py-1.5 text-left font-medium",
                    "hover:bg-secondary",
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
