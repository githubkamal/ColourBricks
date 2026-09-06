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
import { createParty, getParty, listParties, updateParty } from "./api";
import type { PartyDto, PartySearchItem, PartyType } from "./types";

export function VendorsPage({
  partyType = "Vendor",
  title = "Vendors",
}: {
  partyType?: PartyType;
  title?: string;
}) {
  const queryClient = useQueryClient();
  const { confirm, dialog: confirmDialog } = useConfirmDialog();
  const [name, setName] = useState("");
  const [category, setCategory] = useState("");
  const [phone, setPhone] = useState("");
  const [editing, setEditing] = useState<PartyDto | null>(null);
  const [page, setPage] = useState(1);

  const { data, isPending } = useQuery({
    queryKey: ["parties", { type: partyType, page }],
    queryFn: () => listParties(partyType, undefined, page),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["parties"] });

  const add = useMutation({
    mutationFn: () =>
      createParty(
        {
          name: name.trim(),
          types: [partyType],
          category: category.trim() || null,
          phone: phone.trim() || null,
        },
        false,
      ),
    onSuccess: (outcome) => {
      if (outcome.kind === "created") {
        toast.success(`${outcome.party.name} added`);
        setName("");
        setCategory("");
        setPhone("");
        void invalidate();
      } else if (outcome.kind === "needs-confirmation") {
        toast.message("A similar entry already exists — check the list before adding.");
      } else {
        toast.message("That entry already exists");
      }
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not add this entry"),
  });

  const openEdit = async (party: PartySearchItem) => {
    try {
      const full = await getParty(party.id);
      setEditing(full);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not load this entry");
    }
  };

  const save = useMutation({
    mutationFn: (input: { party: PartyDto; name: string; category: string | null }) =>
      updateParty(input.party.id, {
        name: input.name,
        types: input.party.types as PartyType[],
        category: input.category,
        contactPerson: input.party.contactPerson,
        phone: input.party.phone,
        email: input.party.email,
        address: input.party.address,
        gstNumber: input.party.gstNumber,
        bankDetails: input.party.bankDetails,
        paymentTerms: input.party.paymentTerms,
        departmentId: input.party.departmentId,
        isActive: input.party.isActive,
        concurrencyStamp: input.party.concurrencyStamp,
      }),
    onSuccess: () => {
      toast.success("Updated");
      setEditing(null);
      void invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not update this entry"),
  });

  const toggleActive = useMutation({
    mutationFn: async (party: PartySearchItem) => {
      const full = await getParty(party.id);
      return updateParty(full.id, {
        name: full.name,
        types: full.types as PartyType[],
        category: full.category,
        contactPerson: full.contactPerson,
        phone: full.phone,
        email: full.email,
        address: full.address,
        gstNumber: full.gstNumber,
        bankDetails: full.bankDetails,
        paymentTerms: full.paymentTerms,
        departmentId: full.departmentId,
        isActive: !full.isActive,
        concurrencyStamp: full.concurrencyStamp,
      });
    },
    onSuccess: (updated) => {
      toast.success(updated.isActive ? "Reactivated" : "Deactivated");
      void invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not update this entry"),
  });

  async function handleToggle(party: PartySearchItem) {
    const options: ConfirmOptions = party.isActive
      ? {
          title: `Deactivate "${party.name}"?`,
          description: "It will no longer show up when picking it for a new transaction.",
          confirmLabel: "Deactivate",
          destructive: true,
        }
      : {
          title: `Reactivate "${party.name}"?`,
          confirmLabel: "Reactivate",
        };
    const result = await confirm(options);
    if (result.confirmed) toggleActive.mutate(party);
  }

  return (
    <div className="max-w-2xl space-y-6">
      <PageHeader title={title} />

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
            placeholder="Name"
            aria-label="Name"
          />
        </div>
        <div className="min-w-40 space-y-1">
          <label className="text-sm font-medium">Category</label>
          <Input
            value={category}
            onChange={(e) => setCategory(e.target.value)}
            placeholder="Optional"
            aria-label="Category"
          />
        </div>
        <div className="min-w-36 space-y-1">
          <label className="text-sm font-medium">Phone</label>
          <Input
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            placeholder="Optional"
            aria-label="Phone"
          />
        </div>
        <Button type="submit" disabled={add.isPending || !name.trim()}>
          Add
        </Button>
      </form>

      {dataState({
        isPending,
        isEmpty: data?.items.length === 0,
        emptyLabel: "No entries yet.",
      }) ?? (
        <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">Name</th>
                <th className="p-2 font-medium">Roles</th>
                <th className="p-2 font-medium">Category</th>
                <th className="p-2 font-medium">Status</th>
                <th className="p-2 font-medium" />
              </tr>
            </thead>
            <tbody>
              {data?.items.map((vendor) => (
                <tr key={vendor.id} className="border-b last:border-0">
                  <td className="p-2">{vendor.name}</td>
                  <td className="text-muted-foreground p-2">{vendor.types.join(", ")}</td>
                  <td className="text-muted-foreground p-2">{vendor.category ?? "—"}</td>
                  <td className="p-2">
                    <StatusBadge status={vendor.isActive ? "Active" : "Inactive"} />
                  </td>
                  <td className="p-2 text-right whitespace-nowrap">
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      onClick={() => void openEdit(vendor)}
                    >
                      Edit
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      onClick={() => void handleToggle(vendor)}
                    >
                      {vendor.isActive ? "Deactivate" : "Reactivate"}
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
          itemLabel="entries"
        />
      )}

      <Dialog open={editing !== null} onOpenChange={(open) => !open && setEditing(null)}>
        {editing && (
          <DialogContent>
            <DialogTitle>Edit</DialogTitle>
            <EditPartyForm
              party={editing}
              onSave={(name, category) => save.mutate({ party: editing, name, category })}
              saving={save.isPending}
            />
          </DialogContent>
        )}
      </Dialog>

      {confirmDialog}
    </div>
  );
}

function EditPartyForm({
  party,
  onSave,
  saving,
}: {
  party: PartyDto;
  onSave: (name: string, category: string | null) => void;
  saving: boolean;
}) {
  const [name, setName] = useState(party.name);
  const [category, setCategory] = useState(party.category ?? "");

  return (
    <form
      className="mt-3 space-y-3"
      onSubmit={(e) => {
        e.preventDefault();
        if (name.trim()) onSave(name.trim(), category.trim() || null);
      }}
    >
      <div className="space-y-1">
        <label className="text-sm font-medium">Name</label>
        <Input value={name} onChange={(e) => setName(e.target.value)} autoFocus />
      </div>
      <div className="space-y-1">
        <label className="text-sm font-medium">Category</label>
        <Input value={category} onChange={(e) => setCategory(e.target.value)} />
      </div>
      <div className="flex justify-end gap-2 pt-2">
        <Button type="submit" disabled={saving || !name.trim()}>
          Save
        </Button>
      </div>
    </form>
  );
}
