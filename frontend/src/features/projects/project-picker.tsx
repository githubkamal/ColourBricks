"use client";

import { useQuery } from "@tanstack/react-query";
import { useEffect, useId, useRef, useState } from "react";
import { Input } from "@/components/ui/input";
import { listProjects } from "./api";
import type { ProjectListItem, ProjectStatus } from "./types";

/**
 * What's needed to display the current selection — deliberately looser than
 * `ProjectListItem` so a caller that only knows a project's id/name (e.g. a
 * purchase-order line loaded from the server, which doesn't carry the
 * project's code) can still show a selected chip without an extra fetch.
 * `code` is optional and, when absent, is just left out of the display.
 */
export interface ProjectPickerSelection {
  id: number;
  name: string;
  code?: string;
}

export interface ProjectPickerProps {
  selected: ProjectPickerSelection | null;
  onSelect: (project: ProjectListItem | null) => void;
  /** Restricts the search to one status, e.g. "Ongoing" for purchase/expense entry. */
  status?: ProjectStatus;
  label?: string;
  /** Accessible name when no visible `label` is wanted. Falls back to `label`, then "Project". */
  ariaLabel?: string;
}

/**
 * Search-as-you-type project picker (client request, 2026-09-07) — replaces the
 * "fetch up to 100-200 projects and render them all as `<option>`" pattern that
 * was scattered across the app, which breaks down entirely once a company has
 * hundreds/thousands of projects (the dropdown becomes unusably long, and
 * anything past the fetch limit is invisible). Mirrors ItemPicker/PartyPicker's
 * combobox shape for a consistent feel.
 */
export function ProjectPicker({
  selected,
  onSelect,
  status,
  label,
  ariaLabel,
}: ProjectPickerProps) {
  const [term, setTerm] = useState("");
  const [debounced, setDebounced] = useState("");
  const [open, setOpen] = useState(false);
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

  const { data, isFetching } = useQuery({
    queryKey: ["project-picker", { status, q: debounced }],
    queryFn: () => listProjects({ status, search: debounced || undefined, pageSize: 20 }),
    enabled: open,
  });
  const results = data?.items ?? [];

  function choose(project: ProjectListItem) {
    onSelect(project);
    setOpen(false);
    setTerm("");
    setHighlightedIndex(-1);
  }

  function handleInputKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === "Escape") {
      setOpen(false);
      setHighlightedIndex(-1);
      return;
    }
    if (!open || results.length === 0) return;
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setHighlightedIndex((i) => Math.min(i + 1, results.length - 1));
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setHighlightedIndex((i) => Math.max(i - 1, 0));
    } else if (event.key === "Enter" && highlightedIndex >= 0) {
      event.preventDefault();
      const project = results[highlightedIndex];
      if (project) choose(project);
    }
  }

  return (
    <div ref={containerRef} className="relative">
      {label && <label className="mb-1 block text-sm font-medium">{label}</label>}

      {selected ? (
        <div className="bg-card flex items-center justify-between rounded border px-3 py-1.5 text-sm">
          <span className="truncate">
            {selected.code ? `${selected.code} — ` : ""}
            {selected.name}
          </span>
          <button
            type="button"
            className="text-primary shrink-0 text-xs hover:underline"
            onClick={() => {
              onSelect(null);
              setOpen(true);
            }}
          >
            Change
          </button>
        </div>
      ) : (
        <Input
          value={term}
          onChange={(e) => {
            setTerm(e.target.value);
            setOpen(true);
            setHighlightedIndex(-1);
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={handleInputKeyDown}
          placeholder="Search by code or name…"
          aria-label={ariaLabel ?? label ?? "Project"}
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
          className="bg-popover absolute z-20 mt-1 max-h-64 w-full overflow-y-auto rounded border p-1 text-sm shadow-md"
        >
          {isFetching && <p className="text-muted-foreground p-2">Searching…</p>}
          {!isFetching && results.length === 0 && (
            <p className="text-muted-foreground p-2">
              {debounced.length > 0 ? "No matching projects." : "Type to search projects…"}
            </p>
          )}
          {results.map((project, i) => (
            <button
              key={project.id}
              id={`${listboxId}-option-${i}`}
              type="button"
              className={`hover:bg-secondary block w-full rounded px-2 py-1 text-left ${
                i === highlightedIndex ? "bg-secondary" : ""
              }`}
              onClick={() => choose(project)}
            >
              {project.code} — {project.name}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
