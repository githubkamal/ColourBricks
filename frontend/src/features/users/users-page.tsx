"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Fragment, useState } from "react";
import { toast } from "sonner";
import { listProjects } from "@/features/projects/api";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { Input } from "@/components/ui/input";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { Select } from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import { useDebouncedValue } from "@/lib/use-debounced-value";
import { usePagination } from "@/lib/use-pagination";
import {
  assignProjects,
  createUser,
  deleteUser,
  listRoles,
  listUsers,
  resetPassword,
  updateUser,
} from "./api";
import type { RoleOption, UserListItem } from "./types";

const MIN_PASSWORD_LENGTH = 10;

/** FluentValidation's problem+json has no `detail`, just field-keyed `errors` — surface those. */
function apiErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof ApiError)) return fallback;
  const fieldMessages = Object.values(error.fieldErrors).flat();
  return fieldMessages.length > 0 ? fieldMessages.join(" ") : error.message;
}

export function UsersPage() {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["users"] });

  const { data: users, isPending } = useQuery({ queryKey: ["users"], queryFn: () => listUsers() });
  const { pageRows: pagedUsers, page, setPage, pageCount, total } = usePagination(users, 20);
  const { data: roles = [] } = useQuery({ queryKey: ["user-roles"], queryFn: listRoles });
  const [editingId, setEditingId] = useState<number | null>(null);
  const { confirm, dialog } = useConfirmDialog();

  const toggleActive = useMutation({
    mutationFn: (user: UserListItem) =>
      updateUser(user.id, {
        name: user.name,
        email: user.email,
        mobile: user.mobile,
        roleId: user.roleId,
        isActive: !user.isActive,
        concurrencyStamp: user.concurrencyStamp,
      }),
    onSuccess: () => void invalidate(),
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not update the user"),
  });

  const remove = useMutation({
    mutationFn: ({ id, reason }: { id: number; reason: string }) => deleteUser(id, reason),
    onSuccess: () => {
      toast.success("User deleted");
      void invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not delete the user"),
  });

  const reset = useMutation({
    mutationFn: ({ id, password }: { id: number; password: string }) => resetPassword(id, password),
    onSuccess: () => toast.success("Password reset; the user must sign in again"),
    onError: (error) => toast.error(apiErrorMessage(error, "Could not reset the password")),
  });

  return (
    <div className="max-w-4xl space-y-6">
      {dialog}
      <h1 className="text-lg font-semibold">Users</h1>

      <AddUserForm roles={roles} onAdded={invalidate} />

      <div className="bg-card rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Name</th>
              <th className="p-2 font-medium">Email</th>
              <th className="p-2 font-medium">Role</th>
              <th className="p-2 font-medium">Projects</th>
              <th className="p-2 font-medium">Status</th>
              <th className="p-2 font-medium" />
            </tr>
          </thead>
          <tbody>
            {isPending && (
              <tr>
                <td colSpan={6} className="text-muted-foreground p-3 text-center">
                  Loading…
                </td>
              </tr>
            )}
            {pagedUsers.map((user) => (
              <Fragment key={user.id}>
                <tr className="border-b last:border-0">
                  <td className="p-2">{user.name}</td>
                  <td className="text-muted-foreground p-2">{user.email}</td>
                  <td className="text-muted-foreground p-2">{user.roleName ?? "—"}</td>
                  <td className="text-muted-foreground p-2">
                    {user.assignedProjectIds.length === 0
                      ? "All"
                      : `${user.assignedProjectIds.length} assigned`}
                  </td>
                  <td className="text-muted-foreground p-2">
                    {user.isActive ? "Active" : "Inactive"}
                  </td>
                  <td className="space-x-2 p-2 text-right whitespace-nowrap">
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      onClick={() => setEditingId(editingId === user.id ? null : user.id)}
                    >
                      {editingId === user.id ? "Close" : "Edit"}
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      disabled={toggleActive.isPending}
                      onClick={() => toggleActive.mutate(user)}
                    >
                      {user.isActive ? "Deactivate" : "Reactivate"}
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      onClick={() => {
                        void confirm({
                          title: `New password for ${user.name}`,
                          description: `At least ${MIN_PASSWORD_LENGTH} characters.`,
                          inputLabel: "Temporary password",
                          inputType: "password",
                          confirmLabel: "Reset password",
                        }).then(({ confirmed, value: password }) => {
                          if (!confirmed || !password) return;
                          if (password.length < MIN_PASSWORD_LENGTH) {
                            toast.error(
                              `Password must be at least ${MIN_PASSWORD_LENGTH} characters.`,
                            );
                            return;
                          }
                          reset.mutate({ id: user.id, password });
                        });
                      }}
                    >
                      Reset password
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      onClick={() => {
                        const reason = window.prompt(
                          `Delete ${user.name}? This cannot be undone. Reason?`,
                        );
                        if (reason?.trim()) remove.mutate({ id: user.id, reason: reason.trim() });
                      }}
                    >
                      Delete
                    </Button>
                  </td>
                </tr>
                {editingId === user.id && (
                  <tr className="border-b last:border-0">
                    <td colSpan={6} className="bg-secondary/30 p-4">
                      <UserEditor
                        key={user.id}
                        user={user}
                        roles={roles}
                        onSaved={() => {
                          setEditingId(null);
                          void invalidate();
                        }}
                      />
                    </td>
                  </tr>
                )}
              </Fragment>
            ))}
          </tbody>
        </table>
      </div>

      <PaginationBar
        page={page}
        pageCount={pageCount}
        total={total}
        onPageChange={setPage}
        itemLabel="users"
      />
    </div>
  );
}

function AddUserForm({ roles, onAdded }: { roles: RoleOption[]; onAdded: () => void }) {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [roleId, setRoleId] = useState<number | "">("");

  const add = useMutation({
    mutationFn: () =>
      createUser({
        name: name.trim(),
        email: email.trim(),
        password,
        roleId: roleId === "" ? null : roleId,
      }),
    onSuccess: (outcome) => {
      if (outcome.kind === "created") {
        toast.success(`${outcome.user.name} added`);
        setName("");
        setEmail("");
        setPassword("");
        setRoleId("");
        onAdded();
      } else {
        toast.message("That email is already in use");
      }
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not add the user"),
  });

  const ready = name.trim() && email.trim() && password.length >= 10;

  return (
    <form
      className="bg-card flex flex-wrap items-end gap-2 rounded border p-3"
      onSubmit={(e) => {
        e.preventDefault();
        if (ready) add.mutate();
      }}
    >
      <label className="space-y-1">
        <span className="text-sm font-medium">Name</span>
        <Input value={name} onChange={(e) => setName(e.target.value)} aria-label="Name" />
      </label>
      <label className="space-y-1">
        <span className="text-sm font-medium">Email</span>
        <Input
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          aria-label="Email"
        />
      </label>
      <label className="space-y-1">
        <span className="text-sm font-medium">Temporary password</span>
        <Input
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          aria-label="Temporary password"
          autoComplete="new-password"
        />
      </label>
      <label className="space-y-1">
        <span className="text-sm font-medium">Role</span>
        <Select
          value={roleId}
          aria-label="Role"
          onChange={(e) => setRoleId(e.target.value ? Number(e.target.value) : "")}
        >
          <option value="">No role</option>
          {roles.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
        </Select>
      </label>
      <Button type="submit" disabled={!ready || add.isPending}>
        Add user
      </Button>
    </form>
  );
}

function UserEditor({
  user,
  roles,
  onSaved,
}: {
  user: UserListItem;
  roles: RoleOption[];
  onSaved: () => void;
}) {
  const [name, setName] = useState(user.name);
  const [email, setEmail] = useState(user.email);
  const [mobile, setMobile] = useState(user.mobile ?? "");
  const [roleId, setRoleId] = useState<number | "">(user.roleId ?? "");
  const [projectIds, setProjectIds] = useState<number[]>(user.assignedProjectIds);
  const [projectSearch, setProjectSearch] = useState("");
  const debouncedProjectSearch = useDebouncedValue(projectSearch, 200);

  // Search-as-you-type rather than fetching every project up front (client
  // request, 2026-09-07) — a flat checkbox list of hundreds/thousands of
  // projects is unusable either way.
  const { data: projects } = useQuery({
    queryKey: ["projects", { forUserAssignment: true, search: debouncedProjectSearch }],
    queryFn: () => listProjects({ search: debouncedProjectSearch || undefined, pageSize: 50 }),
  });

  const save = useMutation({
    mutationFn: async () => {
      const updated = await updateUser(user.id, {
        name: name.trim(),
        email: email.trim(),
        mobile: mobile.trim() || null,
        roleId: roleId === "" ? null : roleId,
        isActive: user.isActive,
        concurrencyStamp: user.concurrencyStamp,
      });
      await assignProjects(user.id, projectIds);
      return updated;
    },
    onSuccess: () => {
      toast.success("User updated");
      onSaved();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not save the user"),
  });

  const toggleProject = (id: number) =>
    setProjectIds((ids) => (ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id]));

  return (
    <form
      className="space-y-3"
      onSubmit={(e) => {
        e.preventDefault();
        save.mutate();
      }}
    >
      <div className="flex flex-wrap gap-2">
        <label className="space-y-1">
          <span className="text-sm font-medium">Name</span>
          <Input value={name} onChange={(e) => setName(e.target.value)} aria-label="Edit name" />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">Email</span>
          <Input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            aria-label="Edit email"
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">Mobile</span>
          <Input
            type="tel"
            inputMode="tel"
            value={mobile}
            onChange={(e) => setMobile(e.target.value)}
            aria-label="Edit mobile"
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">Role</span>
          <Select
            value={roleId}
            aria-label="Edit role"
            onChange={(e) => setRoleId(e.target.value ? Number(e.target.value) : "")}
          >
            <option value="">No role</option>
            {roles.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
          </Select>
        </label>
      </div>

      <fieldset className="space-y-1">
        <legend className="text-sm font-medium">
          Project access <span className="text-muted-foreground">(none = all projects)</span>
        </legend>
        {projectIds.length > 0 && (
          <p className="text-muted-foreground text-xs">
            {projectIds.length} project{projectIds.length === 1 ? "" : "s"} selected
          </p>
        )}
        <Input
          value={projectSearch}
          onChange={(e) => setProjectSearch(e.target.value)}
          placeholder="Search projects by code or name…"
          aria-label="Search projects"
          className="h-8"
        />
        <div className="bg-card grid max-h-40 grid-cols-2 gap-1 overflow-auto rounded border p-2">
          {projects?.items.map((project) => (
            <label key={project.id} className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={projectIds.includes(project.id)}
                onChange={() => toggleProject(project.id)}
              />
              {project.code} — {project.name}
            </label>
          ))}
        </div>
      </fieldset>

      <Button type="submit" disabled={save.isPending}>
        Save changes
      </Button>
    </form>
  );
}
