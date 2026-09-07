"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { useConfirmDialog, type ConfirmOptions } from "@/components/ui/confirm-dialog";
import { dataState } from "@/components/ui/data-state";
import { Dialog, DialogContent, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { StatusBadge } from "@/components/ui/status-badge";
import { ApiError } from "@/lib/api";
import { usePagination } from "@/lib/use-pagination";
import {
  createTemple,
  getTemple,
  listTemples,
  updateTemple,
  type TempleDetail,
  type TempleSummary,
} from "./api";

export function TemplesPage() {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [editing, setEditing] = useState<TempleDetail | null>(null);
  const { confirm, dialog } = useConfirmDialog();

  const { data, isPending } = useQuery({ queryKey: ["temples"], queryFn: () => listTemples() });
  const { pageRows, page, setPage, pageCount, total } = usePagination(data, 20);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["temples"] });

  const add = useMutation({
    mutationFn: (confirm: boolean) => createTemple(name.trim(), confirm),
    onSuccess: (outcome) => {
      if (outcome.kind === "created") {
        toast.success(`${name.trim()} added`);
        setName("");
        void invalidate();
      } else if (outcome.kind === "needs-confirmation") {
        void confirm({
          title: "Similar temple name exists",
          description: `A similar temple name exists. Add "${name.trim()}" anyway?`,
          confirmLabel: "Add anyway",
        }).then(({ confirmed }) => {
          if (confirmed) add.mutate(true);
        });
      } else {
        toast.message("That temple already exists");
      }
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not add the temple"),
  });

  const openEdit = async (temple: TempleSummary) => {
    try {
      setEditing(await getTemple(temple.id));
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not load this temple");
    }
  };

  const save = useMutation({
    mutationFn: (input: { temple: TempleDetail; name: string }) =>
      updateTemple(input.temple.id, {
        name: input.name,
        isActive: input.temple.isActive,
        concurrencyStamp: input.temple.concurrencyStamp,
      }),
    onSuccess: () => {
      toast.success("Updated");
      setEditing(null);
      void invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not update this temple"),
  });

  const toggleActive = useMutation({
    mutationFn: async (temple: TempleSummary) => {
      const full = await getTemple(temple.id);
      return updateTemple(full.id, {
        name: full.name,
        isActive: !full.isActive,
        concurrencyStamp: full.concurrencyStamp,
      });
    },
    onSuccess: (updated) => {
      toast.success(updated.isActive ? "Reactivated" : "Deactivated");
      void invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not update this temple"),
  });

  async function handleToggle(temple: TempleSummary) {
    const options: ConfirmOptions = temple.isActive
      ? {
          title: `Deactivate "${temple.name}"?`,
          description: "It will no longer show up when picking it for a new donation.",
          confirmLabel: "Deactivate",
          destructive: true,
        }
      : {
          title: `Reactivate "${temple.name}"?`,
          confirmLabel: "Reactivate",
        };
    const result = await confirm(options);
    if (result.confirmed) toggleActive.mutate(temple);
  }

  return (
    <div className="max-w-xl space-y-4">
      {dialog}
      <PageHeader title="Temple Master" />

      <form
        className="flex gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          if (name.trim()) add.mutate(false);
        }}
      >
        <Input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Temple name"
          aria-label="Temple name"
        />
        <Button type="submit" disabled={add.isPending || !name.trim()}>
          Add
        </Button>
      </form>

      {dataState({
        isPending,
        isEmpty: !isPending && data?.length === 0,
        emptyLabel: "No temples yet.",
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
              {pageRows.map((temple) => (
                <tr key={temple.id} className="border-b last:border-0">
                  <td className="p-2">{temple.name}</td>
                  <td className="p-2">
                    <StatusBadge status={temple.isActive ? "Active" : "Inactive"} />
                  </td>
                  <td className="p-2 text-right whitespace-nowrap">
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      onClick={() => void openEdit(temple)}
                    >
                      Edit
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      onClick={() => void handleToggle(temple)}
                    >
                      {temple.isActive ? "Deactivate" : "Reactivate"}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <PaginationBar
        page={page}
        pageCount={pageCount}
        total={total}
        onPageChange={setPage}
        itemLabel="temples"
      />

      <Dialog open={editing !== null} onOpenChange={(open) => !open && setEditing(null)}>
        {editing && (
          <DialogContent>
            <DialogTitle>Edit temple</DialogTitle>
            <EditTempleForm
              temple={editing}
              onSave={(name) => save.mutate({ temple: editing, name })}
              saving={save.isPending}
            />
          </DialogContent>
        )}
      </Dialog>
    </div>
  );
}

function EditTempleForm({
  temple,
  onSave,
  saving,
}: {
  temple: TempleDetail;
  onSave: (name: string) => void;
  saving: boolean;
}) {
  const [name, setName] = useState(temple.name);

  return (
    <form
      className="mt-3 space-y-3"
      onSubmit={(e) => {
        e.preventDefault();
        if (name.trim()) onSave(name.trim());
      }}
    >
      <div className="space-y-1">
        <label className="text-sm font-medium">Name</label>
        <Input value={name} onChange={(e) => setName(e.target.value)} autoFocus />
      </div>
      <div className="flex justify-end gap-2 pt-2">
        <Button type="submit" disabled={saving || !name.trim()}>
          Save
        </Button>
      </div>
    </form>
  );
}
