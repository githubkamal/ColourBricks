"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { useConfirmDialog, type ConfirmOptions } from "@/components/ui/confirm-dialog";
import { dataState } from "@/components/ui/data-state";
import { Dialog, DialogContent, DialogDescription, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { StatusBadge } from "@/components/ui/status-badge";
import { Select } from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import { createItem, getItem, listItemCategories, listItems, updateItem } from "./api";
import type { ItemDto, ItemSearchItem } from "./types";

export function ItemsPage() {
  const queryClient = useQueryClient();
  const { confirm, dialog: confirmDialog } = useConfirmDialog();
  const [name, setName] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [editing, setEditing] = useState<ItemDto | null>(null);
  const [page, setPage] = useState(1);

  const { data, isPending } = useQuery({
    queryKey: ["items", { page }],
    queryFn: () => listItems(undefined, undefined, page),
  });

  const { data: categories = [] } = useQuery({
    queryKey: ["item-categories"],
    queryFn: listItemCategories,
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["items"] });

  const add = useMutation({
    mutationFn: () =>
      createItem(
        {
          name: name.trim(),
          unit: "Nos",
          defaultRate: 0,
          taxRate: 0,
          categoryId: categoryId ? Number(categoryId) : null,
        },
        false,
      ),
    onSuccess: (outcome) => {
      if (outcome.kind === "created") {
        toast.success(`${outcome.item.name} added`);
        setName("");
        setCategoryId("");
        void invalidate();
      } else if (outcome.kind === "needs-confirmation") {
        toast.message("A similar item already exists — check the list before adding.");
      } else {
        toast.message("That item already exists");
      }
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not add the item"),
  });

  const openEdit = async (item: ItemSearchItem) => {
    try {
      const full = await getItem(item.id);
      setEditing(full);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not load the item");
    }
  };

  const save = useMutation({
    mutationFn: (input: { item: ItemDto; name: string; categoryId: number | null }) =>
      updateItem(input.item.id, {
        name: input.name,
        unit: input.item.unit,
        defaultRate: input.item.defaultRate,
        taxRate: input.item.taxRate,
        isActive: input.item.isActive,
        concurrencyStamp: input.item.concurrencyStamp,
        categoryId: input.categoryId,
      }),
    onSuccess: () => {
      toast.success("Item updated");
      setEditing(null);
      void invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not update the item"),
  });

  const toggleActive = useMutation({
    mutationFn: async (item: ItemSearchItem) => {
      const full = await getItem(item.id);
      return updateItem(full.id, {
        name: full.name,
        unit: full.unit,
        defaultRate: full.defaultRate,
        taxRate: full.taxRate,
        isActive: !full.isActive,
        concurrencyStamp: full.concurrencyStamp,
        categoryId: full.categoryId,
      });
    },
    onSuccess: (updated) => {
      toast.success(updated.isActive ? "Item reactivated" : "Item deactivated");
      void invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not update the item"),
  });

  async function handleToggle(item: ItemSearchItem, isActive: boolean) {
    const options: ConfirmOptions = isActive
      ? {
          title: `Deactivate "${item.name}"?`,
          description: "It will no longer show up when picking items for a new purchase.",
          confirmLabel: "Deactivate",
          destructive: true,
        }
      : {
          title: `Reactivate "${item.name}"?`,
          confirmLabel: "Reactivate",
        };
    const result = await confirm(options);
    if (result.confirmed) toggleActive.mutate(item);
  }

  return (
    <div className="max-w-2xl space-y-6">
      <PageHeader title="Item Master" />

      <form
        className="bg-card border-border flex flex-wrap items-end gap-2 rounded-xl border p-4 shadow-xs"
        onSubmit={(e) => {
          e.preventDefault();
          if (name.trim()) add.mutate();
        }}
      >
        <div className="min-w-48 flex-1 space-y-1">
          <label className="text-sm font-medium">Name</label>
          <Input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="e.g. Portland Cement"
            aria-label="New item name"
          />
        </div>
        <div className="min-w-40 space-y-1">
          <label className="text-sm font-medium">Category</label>
          <Select
            className="w-full"
            value={categoryId}
            onChange={(e) => setCategoryId(e.target.value)}
            aria-label="Category"
          >
            <option value="">No category</option>
            {categories
              .filter((c) => c.isActive)
              .map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
          </Select>
        </div>
        <Button type="submit" disabled={add.isPending || !name.trim()}>
          Add Item
        </Button>
      </form>

      {dataState({
        isPending,
        isEmpty: !isPending && data?.items.length === 0,
        emptyLabel: "No items yet.",
      }) ?? (
        <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">Name</th>
                <th className="p-2 font-medium">Category</th>
                <th className="p-2 font-medium">Status</th>
                <th className="p-2 font-medium" />
              </tr>
            </thead>
            <tbody>
              {data?.items.map((item) => (
                <tr key={item.id} className="border-b last:border-0">
                  <td className="max-w-xs truncate p-2 break-words">{item.name}</td>
                  <td className="text-muted-foreground p-2">{item.categoryName ?? "—"}</td>
                  <td className="p-2">
                    <StatusBadge status={item.isActive ? "Active" : "Inactive"} />
                  </td>
                  <td className="p-2 text-right whitespace-nowrap">
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      onClick={() => void openEdit(item)}
                    >
                      Edit
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      onClick={() => void handleToggle(item, item.isActive)}
                    >
                      {item.isActive ? "Deactivate" : "Reactivate"}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {data && (
        <PaginationBar
          page={data.page}
          pageCount={data.totalPages}
          total={data.totalCount}
          onPageChange={setPage}
          itemLabel="items"
        />
      )}

      <Dialog open={editing !== null} onOpenChange={(open) => !open && setEditing(null)}>
        {editing && (
          <DialogContent>
            <DialogTitle>Edit item</DialogTitle>
            <DialogDescription>
              Unit, default rate and tax are entered per purchase, not stored here.
            </DialogDescription>
            <EditItemForm
              item={editing}
              categories={categories}
              onSave={(name, categoryId) => save.mutate({ item: editing, name, categoryId })}
              saving={save.isPending}
            />
          </DialogContent>
        )}
      </Dialog>

      {confirmDialog}
    </div>
  );
}

function EditItemForm({
  item,
  categories,
  onSave,
  saving,
}: {
  item: ItemDto;
  categories: { id: number; name: string; isActive: boolean }[];
  onSave: (name: string, categoryId: number | null) => void;
  saving: boolean;
}) {
  const [name, setName] = useState(item.name);
  const [categoryId, setCategoryId] = useState(item.categoryId ? String(item.categoryId) : "");

  return (
    <form
      className="mt-3 space-y-3"
      onSubmit={(e) => {
        e.preventDefault();
        if (name.trim()) onSave(name.trim(), categoryId ? Number(categoryId) : null);
      }}
    >
      <div className="space-y-1">
        <label className="text-sm font-medium">Name</label>
        <Input value={name} onChange={(e) => setName(e.target.value)} autoFocus />
      </div>
      <div className="space-y-1">
        <label className="text-sm font-medium">Category</label>
        <Select
          className="w-full"
          value={categoryId}
          onChange={(e) => setCategoryId(e.target.value)}
        >
          <option value="">No category</option>
          {categories
            .filter((c) => c.isActive || String(c.id) === categoryId)
            .map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
        </Select>
      </div>
      <div className="flex justify-end gap-2 pt-2">
        <Button type="submit" disabled={saving || !name.trim()}>
          Save
        </Button>
      </div>
    </form>
  );
}
