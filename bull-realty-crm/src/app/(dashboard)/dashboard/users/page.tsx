"use client";

import * as React from "react";
import {
  type ColumnDef,
  type RowSelectionState,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
} from "@tanstack/react-table";
import {
  Blocks,
  Download,
  KeyRound,
  MoreHorizontal,
  Pencil,
  Search,
  ShieldCheck,
  UserRound,
  UsersRound,
  Workflow,
} from "lucide-react";
import Link from "next/link";
import { toast } from "sonner";

import { ResetPasswordDialog } from "@/components/dashboard/reset-password-dialog";
import { UserFormDialog } from "@/components/dashboard/user-form-dialog";
import { UserModulesSheet } from "@/components/dashboard/user-modules-sheet";
import { PagePanel } from "@/components/shell/page-panel";
import { DataTable } from "@/components/ui/table/data-table";
import { DataTableColumnHeader } from "@/components/ui/table/data-table-column-header";
import { DataTableFacetedFilter } from "@/components/ui/table/data-table-faceted-filter";
import { DataTableViewOptions } from "@/components/ui/table/data-table-view-options";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import {
  accessApi,
  ApiError,
  getBranches,
  getUsers,
  SCOPE_LABELS,
  setUsersActive,
  type Branch,
  type RoleSummary,
  type UserListItem,
} from "@/lib/api";
import { cn } from "@/lib/utils";

const SCOPE_TONE: Record<string, string> = {
  Own: "text-slate-600 dark:text-slate-300",
  Team: "text-sky-600 dark:text-sky-400",
  Company: "text-amber-600 dark:text-amber-400",
  Platform: "text-rose-600 dark:text-rose-400",
};

export default function UsersPage() {
  const [users, setUsers] = React.useState<UserListItem[] | null>(null);
  const [branches, setBranches] = React.useState<Branch[]>([]);
  const [roles, setRoles] = React.useState<RoleSummary[]>([]);
  const [search, setSearch] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const [rowSelection, setRowSelection] = React.useState<RowSelectionState>({});
  const [sorting, setSorting] = React.useState<{ id: string; desc: boolean }[]>(
    [{ id: "name", desc: false }]
  );
  const [columnFilters, setColumnFilters] = React.useState<
    { id: string; value: unknown }[]
  >([]);

  React.useEffect(() => {
    getUsers()
      .then(setUsers)
      .catch((error: unknown) => {
        toast.error("Could not load users", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setUsers([]);
      });

    getBranches()
      .then(setBranches)
      .catch(() => setBranches([]));

    // The role catalogue is a nicety, not a dependency — it gives the invite
    // dialog live names and scope hints. Administrators get it; everyone else
    // is refused it, and the static fallback covers them.
    accessApi
      .roles()
      .then(setRoles)
      .catch(() => setRoles([]));
  }, []);

  const upsertUser = React.useCallback((user: UserListItem) => {
    setUsers((prev) => {
      const existing = prev ?? [];
      return existing.some((u) => u.id === user.id)
        ? existing.map((u) => (u.id === user.id ? user : u))
        : [...existing, user];
    });
  }, []);

  const all = React.useMemo(() => users ?? [], [users]);

  const data = React.useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) return all;
    return all.filter((user) =>
      `${user.name} ${user.email} ${user.roleName} ${user.managerName ?? ""} ${user.branchNames.join(" ")}`
        .toLowerCase()
        .includes(needle)
    );
  }, [all, search]);

  const roleOptions = React.useMemo(() => {
    const present = [...new Set(all.map((user) => user.role))];
    return present.map((role) => ({
      label: all.find((user) => user.role === role)?.roleName ?? role,
      value: role,
      count: all.filter((user) => user.role === role).length,
    }));
  }, [all]);

  const statusOptions = React.useMemo(
    () => [
      {
        label: "Active",
        value: "Active",
        count: all.filter((user) => user.isActive).length,
      },
      {
        label: "Inactive",
        value: "Inactive",
        count: all.filter((user) => !user.isActive).length,
      },
      {
        label: "Never signed in",
        value: "Pending",
        count: all.filter((user) => user.lastLoginAt === null).length,
      },
    ],
    [all]
  );

  const columns = React.useMemo<ColumnDef<UserListItem>[]>(
    () => [
      {
        id: "select",
        header: ({ table }) => (
          <Checkbox
            aria-label="Select every user on this page"
            checked={
              table.getIsAllPageRowsSelected() ||
              (table.getIsSomePageRowsSelected() && "indeterminate")
            }
            onCheckedChange={(value) =>
              table.toggleAllPageRowsSelected(value === true)
            }
          />
        ),
        cell: ({ row }) => (
          <Checkbox
            aria-label={`Select ${row.original.name}`}
            checked={row.getIsSelected()}
            onCheckedChange={(value) => row.toggleSelected(value === true)}
          />
        ),
        enableSorting: false,
        enableHiding: false,
        size: 32,
      },
      {
        id: "name",
        accessorFn: (row) => row.name,
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="User" />
        ),
        cell: ({ row }) => (
          <div className="flex items-center gap-2.5">
            <Avatar className="size-7 shrink-0">
              <AvatarFallback className="bg-primary/10 text-[10px] text-primary">
                {initials(row.original.name)}
              </AvatarFallback>
            </Avatar>
            <div className="min-w-0">
              <p className="flex items-center gap-1.5 truncate text-[13px] font-medium text-primary">
                {row.original.name}
                {row.original.mustChangePassword ? (
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <Badge
                        variant="outline"
                        className="h-4 cursor-help px-1 text-[9px] font-normal text-amber-600 dark:text-amber-400"
                      >
                        Invite
                      </Badge>
                    </TooltipTrigger>
                    <TooltipContent>
                      Still on the password it was created with.
                    </TooltipContent>
                  </Tooltip>
                ) : null}
              </p>
              <p className="truncate text-[11px] text-muted-foreground">
                {row.original.email}
              </p>
            </div>
          </div>
        ),
        meta: { label: "User" },
      },
      {
        id: "role",
        accessorFn: (row) => row.role,
        header: "Role",
        enableSorting: false,
        cell: ({ row }) => (
          <div className="flex flex-col gap-0.5">
            <Badge variant="secondary" className="h-5 w-fit px-1.5 text-[11px]">
              {row.original.roleName}
            </Badge>
            <span
              className={cn(
                "text-[10.5px]",
                SCOPE_TONE[row.original.scope] ?? "text-muted-foreground"
              )}
            >
              {SCOPE_LABELS[row.original.scope]}
            </span>
          </div>
        ),
        meta: { label: "Role", variant: "multiSelect", options: roleOptions },
        enableColumnFilter: true,
        filterFn: (row, _id, value: string[]) =>
          value.includes(row.original.role),
      },
      {
        id: "manager",
        accessorFn: (row) => row.managerName ?? "",
        header: "Reports to",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="text-[12.5px] text-muted-foreground">
            {row.original.managerName ?? "—"}
          </span>
        ),
        meta: { label: "Reports to" },
      },
      {
        id: "teamSize",
        accessorFn: (row) => row.teamSize,
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Team" />
        ),
        cell: ({ row }) =>
          row.original.teamSize > 0 ? (
            <span className="text-[12.5px] tabular-nums">
              {row.original.teamSize}
            </span>
          ) : (
            <span className="text-[12.5px] text-muted-foreground">—</span>
          ),
        meta: { label: "Team" },
      },
      {
        id: "branches",
        accessorFn: (row) => row.branchNames.join(", "),
        header: "Branches",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="text-[12.5px] text-muted-foreground">
            {row.original.branchNames.length > 0
              ? row.original.branchNames.join(", ")
              : "—"}
          </span>
        ),
        meta: { label: "Branches" },
      },
      {
        id: "modules",
        accessorFn: (row) => row.moduleOverrides,
        header: "Modules",
        enableSorting: false,
        cell: ({ row }) =>
          row.original.moduleOverrides > 0 ? (
            <Badge
              variant="outline"
              className="h-5 px-1.5 text-[11px] font-normal text-sky-600 dark:text-sky-400"
            >
              {row.original.moduleOverrides} override
              {row.original.moduleOverrides === 1 ? "" : "s"}
            </Badge>
          ) : (
            <span className="text-[12.5px] text-muted-foreground">
              Follows role
            </span>
          ),
        meta: { label: "Modules" },
      },
      {
        id: "lastLoginAt",
        accessorFn: (row) => row.lastLoginAt ?? "",
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Last sign-in" />
        ),
        cell: ({ row }) => (
          <span
            className={cn(
              "text-[12.5px]",
              row.original.lastLoginAt
                ? "text-muted-foreground"
                : "text-amber-600 dark:text-amber-400"
            )}
          >
            {relativeDay(row.original.lastLoginAt)}
          </span>
        ),
        meta: { label: "Last sign-in" },
      },
      {
        id: "status",
        accessorFn: (row) =>
          row.isActive
            ? row.lastLoginAt === null
              ? "Pending"
              : "Active"
            : "Inactive",
        header: "Status",
        enableSorting: false,
        cell: ({ row }) => (
          <Badge
            variant="outline"
            className={cn(
              "h-5 gap-1.5 px-1.5 text-[11px] font-normal",
              row.original.isActive
                ? "text-emerald-600 dark:text-emerald-400"
                : "text-muted-foreground"
            )}
          >
            <span className="size-1.5 rounded-full bg-current" />
            {row.original.isActive ? "Active" : "Inactive"}
          </Badge>
        ),
        meta: { label: "Status", variant: "multiSelect", options: statusOptions },
        enableColumnFilter: true,
        filterFn: (row, _id, value: string[]) => {
          const user = row.original;
          return value.some((choice) =>
            choice === "Active"
              ? user.isActive
              : choice === "Inactive"
                ? !user.isActive
                : user.lastLoginAt === null
          );
        },
      },
      {
        id: "actions",
        header: "",
        enableSorting: false,
        enableHiding: false,
        cell: ({ row }) => (
          <RowActions
            user={row.original}
            branches={branches}
            people={all}
            roles={roles}
            onSaved={upsertUser}
          />
        ),
      },
    ],
    [all, branches, roleOptions, roles, statusOptions, upsertUser]
  );

  const table = useReactTable({
    data,
    columns,
    state: { sorting, columnFilters, rowSelection },
    getRowId: (row) => String(row.id),
    enableRowSelection: true,
    onRowSelectionChange: setRowSelection,
    onSortingChange: setSorting,
    onColumnFiltersChange: setColumnFilters,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    initialState: { pagination: { pageSize: 15 } },
  });

  const selectedIds = Object.keys(rowSelection)
    .filter((id) => rowSelection[id])
    .map(Number);

  async function applyStatus(isActive: boolean) {
    setBusy(true);
    try {
      const updated = await setUsersActive(selectedIds, isActive);
      for (const user of updated) upsertUser(user);
      setRowSelection({});
      toast.success(
        `${updated.length} ${updated.length === 1 ? "user" : "users"} ${
          isActive ? "activated" : "deactivated"
        }`
      );
    } catch (error) {
      toast.error("Could not change those accounts", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(false);
    }
  }

  function exportCsv() {
    const rows = table.getFilteredRowModel().rows.map((row) => row.original);

    if (rows.length === 0) {
      toast.error("Nothing to export", {
        description: "No users match the current filters.",
      });
      return;
    }

    downloadCsv(
      `users-${new Date().toISOString().slice(0, 10)}.csv`,
      [
        "Name",
        "Email",
        "Role",
        "Data scope",
        "Reports to",
        "Team size",
        "Branches",
        "Status",
        "Invite pending",
        "Last sign-in",
        "Created",
      ],
      rows.map((user) => [
        user.name,
        user.email,
        user.roleName,
        SCOPE_LABELS[user.scope],
        user.managerName ?? "",
        String(user.teamSize),
        user.branchNames.join("; "),
        user.isActive ? "Active" : "Inactive",
        user.mustChangePassword ? "Yes" : "No",
        user.lastLoginAt ?? "Never",
        user.createdAt,
      ])
    );

    toast.success(`Exported ${rows.length} users`);
  }

  return (
    <PagePanel
      flush
      icon={UserRound}
      title="Users"
      hint="Everyone with access to your workspace. Their role sets how much they can see, the reporting line sets whose records that includes, and branches narrow it further."
      actions={
        <>
          <Button size="sm" variant="outline" className="h-8" asChild>
            <Link href="/dashboard/users/roles">
              <ShieldCheck /> Roles & access
            </Link>
          </Button>
          <Button size="sm" variant="outline" className="h-8" asChild>
            <Link href="/dashboard/users/teams">
              <UsersRound /> Teams
            </Link>
          </Button>
          <Button size="sm" variant="outline" className="h-8" asChild>
            <Link href="/dashboard/users/hierarchy">
              <Workflow /> Reporting lines
            </Link>
          </Button>
          <UserFormDialog
            branches={branches}
            people={all}
            roles={roles}
            onSaved={upsertUser}
          />
          <Button
            variant="ghost"
            size="icon"
            aria-label="Export users"
            title="Export the filtered list as CSV"
            className="size-8 text-muted-foreground"
            onClick={exportCsv}
          >
            <Download className="size-4" />
          </Button>
        </>
      }
      toolbar={
        <>
          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Name, email, role, manager, branch…"
              className="h-8 w-72 pl-8 text-[13px]"
            />
          </div>
          <DataTableFacetedFilter
            column={table.getColumn("role")}
            title="Role"
            options={roleOptions}
          />
          <DataTableFacetedFilter
            column={table.getColumn("status")}
            title="Status"
            options={statusOptions}
          />
          {columnFilters.length > 0 ? (
            <Button
              variant="ghost"
              size="sm"
              className="h-8"
              onClick={() => table.resetColumnFilters()}
            >
              Clear
            </Button>
          ) : null}
          <span className="text-[12px] text-muted-foreground tabular-nums">
            {users
              ? `${table.getFilteredRowModel().rows.length} of ${all.length}`
              : "Loading…"}
          </span>
          <div className="ml-auto">
            <DataTableViewOptions table={table} />
          </div>
        </>
      }
      subToolbar={
        selectedIds.length > 0 ? (
          <>
            <span className="font-medium text-foreground tabular-nums">
              {selectedIds.length} selected
            </span>
            <Button
              size="sm"
              variant="outline"
              className="h-7"
              disabled={busy}
              onClick={() => void applyStatus(true)}
            >
              Activate
            </Button>
            <Button
              size="sm"
              variant="outline"
              className="h-7"
              disabled={busy}
              onClick={() => void applyStatus(false)}
            >
              Deactivate
            </Button>
            <Button
              size="sm"
              variant="ghost"
              className="h-7"
              onClick={() => setRowSelection({})}
            >
              Clear selection
            </Button>
          </>
        ) : undefined
      }
    >
      {users === null ? (
        <CrmLoadingState label="Loading users" />
      ) : (
        <DataTable table={table} />
      )}
    </PagePanel>
  );
}

/* ------------------------------------------------------------------ *
 * Row actions
 * ------------------------------------------------------------------ */

/**
 * Edit sits outside the menu because it is what an administrator came for; the
 * rest — module exceptions, a password reset — are the occasional cases and
 * would only lengthen every row.
 */
function RowActions({
  user,
  branches,
  people,
  roles,
  onSaved,
}: {
  user: UserListItem;
  branches: Branch[];
  people: UserListItem[];
  roles: RoleSummary[];
  onSaved: (user: UserListItem) => void;
}) {
  const [modulesOpen, setModulesOpen] = React.useState(false);
  const [resetOpen, setResetOpen] = React.useState(false);

  async function toggleActive() {
    try {
      const [updated] = await setUsersActive([user.id], !user.isActive);
      if (updated) onSaved(updated);
      toast.success(user.isActive ? "User deactivated" : "User activated");
    } catch (error) {
      toast.error("Could not change that account", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  return (
    <div className="flex items-center justify-end gap-1">
      <UserFormDialog
        user={user}
        branches={branches}
        people={people}
        roles={roles}
        onSaved={onSaved}
        trigger={
          <Button size="sm" variant="outline" className="h-7">
            <Pencil /> Edit
          </Button>
        }
      />

      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            size="icon"
            variant="ghost"
            className="size-7 text-muted-foreground"
            aria-label={`More actions for ${user.name}`}
          >
            <MoreHorizontal className="size-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-52">
          <DropdownMenuItem onSelect={() => setModulesOpen(true)}>
            <Blocks className="size-4" />
            Module access
          </DropdownMenuItem>
          <DropdownMenuItem onSelect={() => setResetOpen(true)}>
            <KeyRound className="size-4" />
            Reset password
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem
            variant={user.isActive ? "destructive" : "default"}
            onSelect={() => void toggleActive()}
          >
            {user.isActive ? "Deactivate" : "Activate"}
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      {/*
        Mounted beside the menu rather than inside it and driven by state: a
        dialog rendered inside a DropdownMenuItem is unmounted the moment the
        menu closes, which is the same click that opens it.
      */}
      <UserModulesSheet
        user={user}
        open={modulesOpen}
        onOpenChange={setModulesOpen}
        onOverridesChanged={(count) =>
          onSaved({ ...user, moduleOverrides: count })
        }
      />

      <ResetPasswordDialog
        user={user}
        open={resetOpen}
        onOpenChange={setResetOpen}
        onReset={() => onSaved({ ...user, mustChangePassword: true })}
      />
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Formatting
 * ------------------------------------------------------------------ */

function initials(name: string) {
  return name
    .split(" ")
    .map((part) => part[0])
    .slice(0, 2)
    .join("")
    .toUpperCase();
}

/** "Never" reads as a state worth acting on; a blank cell reads as missing data. */
function relativeDay(iso: string | null) {
  if (!iso) return "Never";

  const then = new Date(iso);
  const days = Math.floor((Date.now() - then.getTime()) / 86_400_000);

  if (days <= 0) return "Today";
  if (days === 1) return "Yesterday";
  if (days < 30) return `${days} days ago`;

  return then.toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

/**
 * RFC 4180 quoting throughout rather than only where a comma appears — a name
 * carrying a quote or a newline is rarer than one carrying a comma, and exactly
 * the case that corrupts a spreadsheet silently.
 */
function downloadCsv(filename: string, headers: string[], rows: string[][]) {
  const escape = (value: string) => `"${value.replaceAll('"', '""')}"`;

  const csv = [headers, ...rows]
    .map((row) => row.map(escape).join(","))
    .join("\r\n");

  // The BOM is what makes Excel read the file as UTF-8 rather than as the local
  // code page, which is where Indian names come back mangled.
  const blob = new Blob([`﻿${csv}`], {
    type: "text/csv;charset=utf-8",
  });

  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}
