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
import { createItemCategory, listItemCategories, updateItemCategory } from "./api";
import type { ItemCategoryDto } from "./types";

export function ItemCategoriesPage() {
  const queryClient = useQueryClient();
  const { confirm, dialog: confirmDialog } = useConfirmDialog();
  const [name, setName] = useState("");
  const [editing, setEditing] = useState<ItemCategoryDto | null>(null);

  const { data, isPending } = useQuery({
    queryKey: ["item-categories"],
    queryFn: listItemCategories,
  });
  const { pageRows, page, setPage, pageCount, total } = usePagination(data, 20);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["item-categories"] });

  const add = useMutation({
    mutationFn: () => createItemCategory(name.trim()),
    onSuccess: (category) => {
      toast.success(`${category.name} added`);
      setName("");
      void invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not add the category"),
  });

  const save = useMutation({
    mutationFn: (input: { category: ItemCategoryDto; name: string }) =>
      updateItemCategory(input.category.id, {
        name: input.name,
        isActive: input.category.isActive,
        concurrencyStamp: input.category.concurrencyStamp,
      }),
    onSuccess: () => {
      toast.success("Category updated");
      setEditing(null);
      void invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not update the category"),
  });

  const toggleActive = useMutation({
    mutationFn: (category: ItemCategoryDto) =>
      updateItemCategory(category.id, {
        name: category.name,
        isActive: !category.isActive,
        concurrencyStamp: category.concurrencyStamp,
      }),
    onSuccess: (updated) => {
      toast.success(updated.isActive ? "Category reactivated" : "Category deactivated");
      void invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not update the category"),
  });

  async function handleToggle(category: ItemCategoryDto) {
    const options: ConfirmOptions = category.isActive
      ? {
          title: `Deactivate "${category.name}"?`,
          description: "It will no longer show up when picking a category for an item.",
          confirmLabel: "Deactivate",
          destructive: true,
        }
      : {
          title: `Reactivate "${category.name}"?`,
          confirmLabel: "Reactivate",
        };
    const result = await confirm(options);
    if (result.confirmed) toggleActive.mutate(category);
  }

  return (
    <div className="max-w-xl space-y-4">
      <PageHeader title="Item Categories" />

      <form
        className="bg-card border-border flex items-end gap-2 rounded-xl border p-4 shadow-xs"
        onSubmit={(e) => {
          e.preventDefault();
          if (name.trim()) add.mutate();
        }}
      >
        <div className="flex-1 space-y-1">
          <label className="text-sm font-medium">Name</label>
          <Input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="e.g. Cement & Concrete"
            aria-label="New category name"
          />
        </div>
        <Button type="submit" disabled={add.isPending || !name.trim()}>
          Add Category
        </Button>
      </form>

      {dataState({
        isPending,
        isEmpty: !isPending && data?.length === 0,
        emptyLabel: "No categories found.",
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
              {pageRows.map((category) => (
                <tr key={category.id} className="border-b last:border-0">
                  <td className="p-2">{category.name}</td>
                  <td className="p-2">
                    <StatusBadge status={category.isActive ? "Active" : "Inactive"} />
                  </td>
                  <td className="p-2 text-right whitespace-nowrap">
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      onClick={() => setEditing(category)}
                    >
                      Edit
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      onClick={() => void handleToggle(category)}
                    >
                      {category.isActive ? "Deactivate" : "Reactivate"}
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
        itemLabel="categories"
      />

      <Dialog open={editing !== null} onOpenChange={(open) => !open && setEditing(null)}>
        {editing && (
          <DialogContent>
            <DialogTitle>Edit category</DialogTitle>
            <EditCategoryForm
              category={editing}
              onSave={(name) => save.mutate({ category: editing, name })}
              saving={save.isPending}
            />
          </DialogContent>
        )}
      </Dialog>

      {confirmDialog}
    </div>
  );
}

function EditCategoryForm({
  category,
  onSave,
  saving,
}: {
  category: ItemCategoryDto;
  onSave: (name: string) => void;
  saving: boolean;
}) {
  const [name, setName] = useState(category.name);

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
