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
import { Building2, Download, Search } from "lucide-react";
import { toast } from "sonner";

import {
  CompanyFormDialog,
  CreateCompanyDialog,
} from "@/components/dashboard/company-form-dialog";
import { useSession } from "@/components/dashboard/session-provider";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { PagePanel } from "@/components/shell/page-panel";
import { DataTable } from "@/components/ui/table/data-table";
import { DataTableColumnHeader } from "@/components/ui/table/data-table-column-header";
import { DataTableViewOptions } from "@/components/ui/table/data-table-view-options";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  ApiError,
  getCompanies,
  isPlatformAdmin,
  type Company,
} from "@/lib/api";

export default function CompaniesPage() {
  const { user } = useSession();
  // Only the platform tier can add tenants or move one between plans; a company
  // admin lands here to rename its own company and nothing else.
  const canManageTenants = isPlatformAdmin(user);

  const [companies, setCompanies] = React.useState<Company[] | null>(null);
  const [search, setSearch] = React.useState("");
  const [sorting, setSorting] = React.useState<{ id: string; desc: boolean }[]>(
    [{ id: "name", desc: false }]
  );

  React.useEffect(() => {
    getCompanies()
      .then(setCompanies)
      .catch((error: unknown) => {
        toast.error("Could not load companies", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setCompanies([]);
      });
  }, []);

  function upsertCompany(updated: Company) {
    setCompanies((prev) =>
      (prev ?? []).map((c) => (c.id === updated.id ? updated : c))
    );
  }

  function addCompany(created: Company) {
    setCompanies((prev) =>
      [...(prev ?? []), created].sort((a, b) => a.name.localeCompare(b.name))
    );
  }

  const all = React.useMemo(() => companies ?? [], [companies]);

  const data = React.useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) return all;
    return all.filter((company) =>
      `${company.name} ${company.slug} ${company.planTier}`
        .toLowerCase()
        .includes(needle)
    );
  }, [all, search]);

  const columns = React.useMemo<ColumnDef<Company>[]>(
    () => [
      {
        id: "name",
        accessorFn: (row) => row.name,
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Company" />
        ),
        cell: ({ row }) => (
          <div className="flex items-center gap-2.5">
            <span className="flex size-7 shrink-0 items-center justify-center rounded bg-primary/10 text-primary">
              <Building2 className="size-3.5" />
            </span>
            <div className="min-w-0">
              <p className="truncate text-[13px] font-medium text-primary">
                {row.original.name}
              </p>
              <p className="truncate text-[11px] text-muted-foreground">
                {row.original.slug}
              </p>
            </div>
          </div>
        ),
        meta: { label: "Company" },
      },
      {
        id: "planTier",
        accessorFn: (row) => row.planTier,
        header: "Plan",
        enableSorting: false,
        cell: ({ row }) => (
          <Badge variant="secondary" className="h-5 px-1.5 text-[11px]">
            {row.original.planTier}
          </Badge>
        ),
        meta: { label: "Plan" },
      },
      {
        id: "status",
        accessorFn: (row) => row.status,
        header: "Status",
        enableSorting: false,
        cell: ({ row }) => (
          <Badge
            variant="outline"
            className="h-5 gap-1.5 px-1.5 text-[11px] font-normal text-emerald-600 dark:text-emerald-400"
          >
            <span className="size-1.5 rounded-full bg-current" />
            {row.original.status}
          </Badge>
        ),
        meta: { label: "Status" },
      },
      {
        id: "actions",
        header: "",
        enableSorting: false,
        enableHiding: false,
        cell: ({ row }) => (
          <div className="flex justify-end">
            <CompanyFormDialog
              company={row.original}
              canManagePlan={canManageTenants}
              onSaved={upsertCompany}
            />
          </div>
        ),
      },
    ],
    [canManageTenants]
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
      icon={Building2}
      title="Companies"
      hint="Tenant accounts on the platform. Each company owns its own branches, users and pipeline — data never crosses between them. A super admin sees every company and can add more; everyone else sees only their own."
      actions={
        <>
          <Button
            variant="ghost"
            size="icon"
            aria-label="Export companies"
            title="Export companies"
            className="size-8 text-muted-foreground"
          >
            <Download className="size-4" />
          </Button>
          {canManageTenants ? (
            <CreateCompanyDialog onCreated={addCompany} />
          ) : null}
        </>
      }
      toolbar={
        <>
          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Company name, slug, plan…"
              className="h-8 w-56 pl-8 text-[13px]"
            />
          </div>
          <span className="text-[12px] text-muted-foreground tabular-nums">
            {companies ? `${data.length} of ${all.length}` : "Loading…"}
          </span>
          <div className="ml-auto">
            <DataTableViewOptions table={table} />
          </div>
        </>
      }
    >
      {companies === null ? (
        <CrmLoadingState
          label="Loading companies"
          detail="Fetching every tenant on the platform"
        />
      ) : (
        <DataTable table={table} />
      )}
    </PagePanel>
  );
}
