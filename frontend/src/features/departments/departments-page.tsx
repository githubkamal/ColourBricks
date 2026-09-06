"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { dataState } from "@/components/ui/data-state";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { StatusBadge } from "@/components/ui/status-badge";
import { ApiError } from "@/lib/api";
import { createDepartment, listDepartments, updateDepartment } from "./api";
import type { DepartmentDto } from "./types";

export function DepartmentsPage() {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");

  const { data, isPending } = useQuery({
    queryKey: ["departments", { includeInactive: true }],
    queryFn: () => listDepartments(true),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["departments"] });

  const add = useMutation({
    mutationFn: () => createDepartment({ name: name.trim() }),
    onSuccess: (outcome) => {
      if (outcome.kind === "created") {
        toast.success(`${outcome.department.name} added`);
        setName("");
        void invalidate();
      } else {
        toast.message("That department already exists");
      }
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not add the department"),
  });

  const toggle = useMutation({
    mutationFn: (department: DepartmentDto) =>
      updateDepartment(department.id, {
        name: department.name,
        isActive: !department.isActive,
        concurrencyStamp: department.concurrencyStamp,
      }),
    onSuccess: () => void invalidate(),
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not update the department"),
  });

  return (
    <div className="max-w-xl space-y-4">
      <PageHeader title="Departments" />

      <form
        className="flex gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          if (name.trim()) add.mutate();
        }}
      >
        <Input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g. Site Operations"
          aria-label="New department name"
        />
        <Button type="submit" disabled={add.isPending || !name.trim()}>
          Add
        </Button>
      </form>

      {dataState({
        isPending,
        isEmpty: !isPending && (data?.length ?? 0) === 0,
        emptyLabel: "No departments found.",
      }) ?? (
        <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">Name</th>
                <th className="p-2 font-medium">Status</th>
                <th className="p-2 font-medium" />
              </tr>
            </thead>
            <tbody>
              {data?.map((department) => (
                <tr key={department.id} className="border-b last:border-0">
                  <td className="p-2">{department.name}</td>
                  <td className="p-2">
                    <StatusBadge status={department.isActive ? "Active" : "Inactive"} />
                  </td>
                  <td className="p-2 text-right">
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      disabled={toggle.isPending}
                      onClick={() => toggle.mutate(department)}
                    >
                      {department.isActive ? "Deactivate" : "Reactivate"}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
