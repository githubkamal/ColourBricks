"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import {
  createRole,
  deleteRole,
  getPermissionCatalogue,
  getRole,
  listRoles,
  setRolePermissions,
} from "./api";

export function RolesPage() {
  const queryClient = useQueryClient();
  const { confirm, dialog } = useConfirmDialog();
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [newName, setNewName] = useState("");

  const { data: roles = [] } = useQuery({ queryKey: ["roles"], queryFn: listRoles });

  const add = useMutation({
    mutationFn: () => createRole(newName.trim()),
    onSuccess: (outcome) => {
      if (outcome.kind === "created") {
        toast.success(`${outcome.role.name} created`);
        setNewName("");
        setSelectedId(outcome.role.id);
        void queryClient.invalidateQueries({ queryKey: ["roles"] });
      } else {
        toast.message("A role with that name already exists");
      }
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not create the role"),
  });

  const remove = useMutation({
    mutationFn: ({ id, reason }: { id: number; reason: string }) => deleteRole(id, reason),
    onSuccess: (_, { id }) => {
      toast.success("Role deleted");
      if (selectedId === id) setSelectedId(null);
      void queryClient.invalidateQueries({ queryKey: ["roles"] });
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not delete the role"),
  });

  return (
    <div className="flex max-w-5xl gap-6">
      {dialog}
      <div className="w-64 shrink-0 space-y-3">
        <h1 className="text-lg font-semibold">Roles</h1>
        <ul className="bg-card rounded border text-sm">
          {roles.map((role) => (
            <li key={role.id} className="flex items-center">
              <button
                type="button"
                className={`block flex-1 px-3 py-2 text-left ${
                  selectedId === role.id ? "bg-secondary font-medium" : "hover:bg-secondary/50"
                }`}
                onClick={() => setSelectedId(role.id)}
              >
                {role.name}
                <span className="text-muted-foreground"> · {role.permissionCount}</span>
                {role.isSystem && <span className="text-muted-foreground"> · system</span>}
              </button>
              {!role.isSystem && (
                <Button
                  type="button"
                  variant="ghost"
                  size="xs"
                  className="mr-1"
                  onClick={async () => {
                    const { confirmed, value: reason } = await confirm({
                      title: `Delete ${role.name}?`,
                      description: "This cannot be undone.",
                      inputLabel: "Reason",
                      destructive: true,
                      confirmLabel: "Delete",
                    });
                    if (!confirmed) return;
                    remove.mutate({ id: role.id, reason: reason ?? "" });
                  }}
                >
                  Delete
                </Button>
              )}
            </li>
          ))}
        </ul>
        <form
          className="flex gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            if (newName.trim()) add.mutate();
          }}
        >
          <Input
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            placeholder="New role"
            aria-label="New role"
          />
          <Button type="submit" size="xs" disabled={add.isPending || !newName.trim()}>
            Add
          </Button>
        </form>
      </div>

      <div className="flex-1">
        {selectedId === null ? (
          <p className="text-muted-foreground text-sm">Select a role to edit its permissions.</p>
        ) : (
          <PermissionMatrix key={selectedId} roleId={selectedId} />
        )}
      </div>
    </div>
  );
}

function PermissionMatrix({ roleId }: { roleId: number }) {
  const queryClient = useQueryClient();
  const { data: catalogue } = useQuery({
    queryKey: ["permission-catalogue"],
    queryFn: getPermissionCatalogue,
  });
  const { data: role, isPending } = useQuery({
    queryKey: ["role", roleId],
    queryFn: () => getRole(roleId),
  });

  if (isPending || !catalogue || !role) {
    return <p className="text-muted-foreground text-sm">Loading…</p>;
  }

  return (
    <MatrixEditor
      roleName={role.name}
      initialKeys={role.permissionKeys}
      catalogue={catalogue}
      roleId={roleId}
      onSaved={() => {
        void queryClient.invalidateQueries({ queryKey: ["roles"] });
        void queryClient.invalidateQueries({ queryKey: ["role", roleId] });
      }}
    />
  );
}

function MatrixEditor({
  roleId,
  roleName,
  initialKeys,
  catalogue,
  onSaved,
}: {
  roleId: number;
  roleName: string;
  initialKeys: string[];
  catalogue: { modules: { key: string; name: string }[]; actions: string[] };
  onSaved: () => void;
}) {
  const [keys, setKeys] = useState<Set<string>>(() => new Set(initialKeys));

  const has = (m: string, a: string) => keys.has(`${m}.${a}`);
  const toggle = (m: string, a: string) =>
    setKeys((prev) => {
      const next = new Set(prev);
      const k = `${m}.${a}`;
      if (next.has(k)) next.delete(k);
      else next.add(k);
      return next;
    });
  const toggleModule = (m: string, on: boolean) =>
    setKeys((prev) => {
      const next = new Set(prev);
      for (const a of catalogue.actions) {
        const k = `${m}.${a}`;
        if (on) next.add(k);
        else next.delete(k);
      }
      return next;
    });
  const toggleAction = (a: string, on: boolean) =>
    setKeys((prev) => {
      const next = new Set(prev);
      for (const m of catalogue.modules) {
        const k = `${m.key}.${a}`;
        if (on) next.add(k);
        else next.delete(k);
      }
      return next;
    });

  const save = useMutation({
    mutationFn: () => setRolePermissions(roleId, [...keys]),
    onSuccess: () => {
      toast.success(`${roleName} permissions saved`);
      onSaved();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not save the permissions"),
  });

  return (
    <div className="space-y-3">
      <h2 className="text-sm font-semibold">{roleName} — permission matrix</h2>
      <div className="bg-card overflow-x-auto rounded border">
        <table className="text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr>
              <th className="p-2 text-left font-medium">Module</th>
              {catalogue.actions.map((a) => (
                <th key={a} className="p-2 font-medium">
                  <label className="flex flex-col items-center gap-1">
                    <span>{a}</span>
                    <input
                      type="checkbox"
                      aria-label={`All ${a}`}
                      checked={catalogue.modules.every((m) => has(m.key, a))}
                      onChange={(e) => toggleAction(a, e.target.checked)}
                    />
                  </label>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {catalogue.modules.map((m) => (
              <tr key={m.key} className="border-t">
                <td className="p-2">
                  <label className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      aria-label={`All ${m.name}`}
                      checked={catalogue.actions.every((a) => has(m.key, a))}
                      onChange={(e) => toggleModule(m.key, e.target.checked)}
                    />
                    {m.name}
                  </label>
                </td>
                {catalogue.actions.map((a) => (
                  <td key={a} className="p-2 text-center">
                    <input
                      type="checkbox"
                      aria-label={`${m.key}.${a}`}
                      checked={has(m.key, a)}
                      onChange={() => toggle(m.key, a)}
                    />
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Button type="button" disabled={save.isPending} onClick={() => save.mutate()}>
        Save permissions
      </Button>
    </div>
  );
}
