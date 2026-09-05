"use client";

import * as React from "react";
import {
  type ColumnDef,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
} from "@tanstack/react-table";
import { Download, MapPinned, Pencil, Search } from "lucide-react";
import { toast } from "sonner";

import { BranchFormDialog } from "@/components/dashboard/branch-form-dialog";
import { PagePanel } from "@/components/shell/page-panel";
import { DataTable } from "@/components/ui/table/data-table";
import { DataTableColumnHeader } from "@/components/ui/table/data-table-column-header";
import { DataTableViewOptions } from "@/components/ui/table/data-table-view-options";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { ApiError, getBranches, type Branch } from "@/lib/api";

export default function BranchesPage() {
  const [branches, setBranches] = React.useState<Branch[] | null>(null);
  const [search, setSearch] = React.useState("");
  const [sorting, setSorting] = React.useState<{ id: string; desc: boolean }[]>(
    [{ id: "name", desc: false }]
  );

  React.useEffect(() => {
    getBranches()
      .then(setBranches)
      .catch((error: unknown) => {
        toast.error("Could not load branches", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setBranches([]);
      });
  }, []);

  function upsertBranch(branch: Branch) {
    setBranches((prev) => {
      const existing = prev ?? [];
      const exists = existing.some((b) => b.id === branch.id);
      return exists
        ? existing.map((b) => (b.id === branch.id ? branch : b))
        : [...existing, branch];
    });
  }

  const all = React.useMemo(() => branches ?? [], [branches]);

  const data = React.useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) return all;
    return all.filter((branch) =>
      `${branch.name} ${branch.city} ${branch.contactPhone ?? ""}`
        .toLowerCase()
        .includes(needle)
    );
  }, [all, search]);

  const columns = React.useMemo<ColumnDef<Branch>[]>(
    () => [
      {
        id: "name",
        accessorFn: (row) => row.name,
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Branch" />
        ),
        cell: ({ row }) => (
          <div className="flex items-center gap-2.5">
            <span className="flex size-7 shrink-0 items-center justify-center rounded bg-primary/10 text-primary">
              <MapPinned className="size-3.5" />
            </span>
            <span className="truncate text-[13px] font-medium text-primary">
              {row.original.name}
            </span>
          </div>
        ),
        meta: { label: "Branch" },
      },
      {
        id: "city",
        accessorFn: (row) => row.city,
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="City" />
        ),
        cell: ({ row }) => (
          <span className="text-[12.5px]">{row.original.city}</span>
        ),
        meta: { label: "City" },
      },
      {
        id: "contactPhone",
        accessorFn: (row) => row.contactPhone ?? "",
        header: "Contact",
        enableSorting: false,
        cell: ({ row }) => (
          <span className="text-[12.5px] text-muted-foreground">
            {row.original.contactPhone ?? "—"}
          </span>
        ),
        meta: { label: "Contact" },
      },
      {
        id: "userCount",
        accessorFn: (row) => row.userCount,
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Users" />
        ),
        cell: ({ row }) => (
          <span className="text-[12.5px] tabular-nums">
            {row.original.userCount}
          </span>
        ),
        meta: { label: "Users" },
      },
      {
        id: "actions",
        header: "",
        enableSorting: false,
        enableHiding: false,
        cell: ({ row }) => (
          <div className="flex justify-end">
            <BranchFormDialog
              branch={row.original}
              onSaved={upsertBranch}
              trigger={
                <Button size="sm" variant="outline" className="h-7">
                  <Pencil /> Edit
                </Button>
              }
            />
          </div>
        ),
      },
    ],
    []
  );

  const table = useReactTable({
    data,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    initialState: { pagination: { pageSize: 15 } },
  });

  return (
    <PagePanel
      flush
      icon={MapPinned}
      title="Branches"
      hint="Every office under your company. Users, leads and inventory are scoped to the branches a person is assigned to."
      actions={
        <>
          <BranchFormDialog onSaved={upsertBranch} />
          <Button
            variant="ghost"
            size="icon"
            aria-label="Export branches"
            title="Export branches"
            className="size-8 text-muted-foreground"
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
              placeholder="Branch name, city, phone…"
              className="h-8 w-56 pl-8 text-[13px]"
            />
          </div>
          <span className="text-[12px] text-muted-foreground tabular-nums">
            {branches ? `${data.length} of ${all.length}` : "Loading…"}
          </span>
          <div className="ml-auto">
            <DataTableViewOptions table={table} />
          </div>
        </>
      }
    >
      {branches === null ? (
        <CrmLoadingState label="Loading branches" />
      ) : (
        <DataTable table={table} />
      )}
    </PagePanel>
  );
}
