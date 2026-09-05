"use client";

import type { Column, Table } from "@tanstack/react-table";
import { X } from "lucide-react";
import * as React from "react";

import { DataTableFacetedFilter } from "@/components/ui/table/data-table-faceted-filter";
import { DataTableViewOptions } from "@/components/ui/table/data-table-view-options";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

interface DataTableToolbarProps<TData> {
  table: Table<TData>;
  children?: React.ReactNode;
}

export function DataTableToolbar<TData>({
  table,
  children,
}: DataTableToolbarProps<TData>) {
  const isFiltered = table.getState().columnFilters.length > 0;

  const columns = React.useMemo(
    () => table.getAllColumns().filter((column) => column.getCanFilter()),
    [table]
  );

  return (
    <div className="flex w-full flex-wrap items-center justify-between gap-2">
      <div className="flex flex-1 flex-wrap items-center gap-2">
        {columns.map((column) => (
          <ToolbarFilter key={column.id} column={column} />
        ))}
        {isFiltered && (
          <Button
            variant="outline"
            size="sm"
            className="h-8 border-dashed"
            onClick={() => table.resetColumnFilters()}
          >
            <X /> Reset
          </Button>
        )}
      </div>
      <div className="flex items-center gap-2">
        {children}
        <DataTableViewOptions table={table} />
      </div>
    </div>
  );
}

function ToolbarFilter<TData>({ column }: { column: Column<TData> }) {
  const meta = column.columnDef.meta;
  if (!meta?.variant) return null;

  if (meta.variant === "text") {
    return (
      <Input
        placeholder={meta.placeholder ?? meta.label}
        value={(column.getFilterValue() as string) ?? ""}
        onChange={(event) => column.setFilterValue(event.target.value)}
        className="h-8 w-40 lg:w-56"
      />
    );
  }

  if (meta.variant === "select" || meta.variant === "multiSelect") {
    return (
      <DataTableFacetedFilter
        column={column}
        title={meta.label ?? column.id}
        options={meta.options ?? []}
        multiple={meta.variant === "multiSelect"}
      />
    );
  }

  return null;
}
