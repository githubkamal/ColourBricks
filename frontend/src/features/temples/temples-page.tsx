"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { dataState } from "@/components/ui/data-state";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { ApiError } from "@/lib/api";
import { usePagination } from "@/lib/use-pagination";
import { createTemple, listTemples } from "./api";

export function TemplesPage() {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const { confirm, dialog } = useConfirmDialog();

  const { data, isPending } = useQuery({ queryKey: ["temples"], queryFn: () => listTemples() });
  const { pageRows, page, setPage, pageCount, total } = usePagination(data, 20);

  const add = useMutation({
    mutationFn: (confirm: boolean) => createTemple(name.trim(), confirm),
    onSuccess: (outcome) => {
      if (outcome.kind === "created") {
        toast.success(`${name.trim()} added`);
        setName("");
        void queryClient.invalidateQueries({ queryKey: ["temples"] });
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
              </tr>
            </thead>
            <tbody>
              {pageRows.map((temple) => (
                <tr key={temple.id} className="border-b last:border-0">
                  <td className="p-2">{temple.name}</td>
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
    </div>
  );
}
