"use client";

import { useQuery } from "@tanstack/react-query";
import { Select } from "@/components/ui/select";
import { listTeamsGrouped } from "./api";
import type { TeamDto } from "./types";

export interface TeamPickerProps {
  selected: TeamDto | null;
  onSelect: (team: TeamDto | null) => void;
  label?: string;
  /**
   * Show only this department's teams (client request, 2026-11-21 — pick
   * "Electrical" and the team dropdown lists just the electrical teams).
   */
  departmentId?: number | null;
}

/**
 * Team selector grouped by department (BRD §8) — teams are listed under an
 * <optgroup> per department so "Electrical Team A/B/C" sit together.
 */
export function TeamPicker({ selected, onSelect, label = "Team", departmentId }: TeamPickerProps) {
  const { data: groups = [], isPending } = useQuery({
    queryKey: ["teams", "grouped"],
    queryFn: () => listTeamsGrouped(),
  });

  const byId = new Map(groups.flatMap((g) => g.teams).map((t) => [t.id, t]));
  const filtered = departmentId ? groups.filter((g) => g.departmentId === departmentId) : null;
  const visible = (filtered ?? groups).flatMap((g) => g.teams);
  const showSelectedExtra = selected !== null && !visible.some((t) => t.id === selected.id);

  return (
    <label className="block space-y-1">
      <span className="text-sm font-medium">{label}</span>
      <Select
        className="w-full"
        value={selected?.id ?? ""}
        disabled={isPending}
        aria-label={label}
        onChange={(e) => {
          const id = Number(e.target.value);
          onSelect(byId.get(id) ?? (selected?.id === id ? selected : null));
        }}
      >
        <option value="">Select a team…</option>
        {/* A selection outside the filter (e.g. restored from an edit) still needs an option. */}
        {showSelectedExtra && <option value={selected.id}>{selected.name}</option>}
        {filtered
          ? visible.map((team) => (
              <option key={team.id} value={team.id}>
                {team.name}
              </option>
            ))
          : groups.map((group) => (
              <optgroup key={group.departmentId ?? "none"} label={group.departmentName}>
                {group.teams.map((team) => (
                  <option key={team.id} value={team.id}>
                    {team.name}
                  </option>
                ))}
              </optgroup>
            ))}
      </Select>
    </label>
  );
}
