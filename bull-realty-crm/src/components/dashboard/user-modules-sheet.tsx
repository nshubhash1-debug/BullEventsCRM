"use client";

import * as React from "react";
import { RotateCcw } from "lucide-react";
import { toast } from "sonner";

import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet";
import { Switch } from "@/components/ui/switch";
import {
  accessApi,
  ApiError,
  type UserListItem,
  type UserModuleRow,
} from "@/lib/api";

const MODULE_LABELS: Record<string, string> = {
  leads: "Leads",
  engagement: "Engagement",
  automation: "Lead Automation",
  calls: "Calls",
  sales: "Sales",
  inventory: "Inventory",
  reports: "Reports",
  calendar: "Calendar",
  goals: "Goals",
  "post-sales": "Post Sales",
  "customer-care": "Customer Care",
  constructions: "Constructions",
  hr: "HR",
  administration: "Administration",
  "system-console": "System Console",
};

/**
 * Per-person module exceptions.
 *
 * The role is meant to be the answer, so every row starts as "follows the
 * role". An override is the case a company argues for one person at a time —
 * the executive who also needs Post Sales, the manager covering HR for a month
 * — and it is shown as a deviation rather than as a plain on/off, so it stays
 * obvious that somebody made a decision here.
 */
export function UserModulesSheet({
  user,
  trigger,
  open: controlledOpen,
  onOpenChange,
  onOverridesChanged,
}: {
  user: UserListItem;
  /** Omitted when the caller drives the sheet through `open` instead. */
  trigger?: React.ReactNode;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  onOverridesChanged?: (count: number) => void;
}) {
  // Works either way: a trigger the sheet owns, or a caller holding the state.
  // The row menu needs the second — a sheet rendered inside a menu item is
  // unmounted by the same click that opens it.
  const [uncontrolledOpen, setUncontrolledOpen] = React.useState(false);
  const open = controlledOpen ?? uncontrolledOpen;

  const setOpen = React.useCallback(
    (next: boolean) => {
      setUncontrolledOpen(next);
      onOpenChange?.(next);
    },
    [onOpenChange]
  );

  const [rows, setRows] = React.useState<UserModuleRow[] | null>(null);
  const [pending, setPending] = React.useState<string | null>(null);

  // Clearing on the way open, during render rather than in the effect below:
  // the sheet stays mounted between openings, so without this the previous
  // person's modules are on screen until the fetch lands.
  const [loadedFor, setLoadedFor] = React.useState<number | null>(null);
  const wanted = open ? user.id : null;

  if (wanted !== loadedFor) {
    setLoadedFor(wanted);
    setRows(null);
  }

  React.useEffect(() => {
    if (!open) return;

    accessApi
      .userModules(user.id)
      .then(setRows)
      .catch((error: unknown) => {
        toast.error("Could not load module access", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setRows([]);
      });
  }, [open, user.id]);

  async function apply(module: string, granted: boolean | null) {
    const previous = rows;
    setPending(module);

    setRows((current) =>
      (current ?? []).map((row) =>
        row.module === module
          ? { ...row, override: granted, effective: granted ?? row.fromRole }
          : row
      )
    );

    try {
      await accessApi.setUserModule(user.id, module, granted);

      const next = (previous ?? []).map((row) =>
        row.module === module ? { ...row, override: granted } : row
      );
      onOverridesChanged?.(next.filter((row) => row.override !== null).length);
    } catch (error) {
      setRows(previous);
      toast.error("Could not change module access", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setPending(null);
    }
  }

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      {trigger ? <SheetTrigger asChild>{trigger}</SheetTrigger> : null}
      <SheetContent className="w-full sm:max-w-md">
        <SheetHeader>
          <SheetTitle>Module access</SheetTitle>
          <SheetDescription>
            {user.name} · {user.roleName}. Every module follows the role unless
            somebody makes an exception here.
          </SheetDescription>
        </SheetHeader>

        {rows === null ? (
          <div className="px-4">
            <CrmLoadingState label="Loading modules" />
          </div>
        ) : (
          <div className="flex min-h-0 flex-col gap-1 overflow-y-auto px-4 pb-4">
            {rows.map((row) => {
              const overridden = row.override !== null;

              return (
                <div
                  key={row.module}
                  className="flex items-center gap-2 rounded-md border px-3 py-2"
                >
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-[13px] font-medium">
                      {MODULE_LABELS[row.module] ?? row.module}
                    </p>
                    <p className="text-[11px] text-muted-foreground">
                      {overridden
                        ? `Overridden — the ${user.roleName} role ${
                            row.fromRole ? "grants" : "does not grant"
                          } this`
                        : row.fromRole
                          ? "Granted by the role"
                          : "Not granted by the role"}
                    </p>
                  </div>

                  {overridden ? (
                    <Button
                      size="icon"
                      variant="ghost"
                      aria-label={`Follow the role for ${row.module}`}
                      title="Follow the role"
                      className="size-7 text-muted-foreground"
                      disabled={pending === row.module}
                      onClick={() => void apply(row.module, null)}
                    >
                      <RotateCcw className="size-3.5" />
                    </Button>
                  ) : (
                    <Badge
                      variant="outline"
                      className="h-5 px-1.5 text-[10px] font-normal text-muted-foreground"
                    >
                      Role
                    </Badge>
                  )}

                  <Switch
                    aria-label={`${MODULE_LABELS[row.module] ?? row.module} for ${user.name}`}
                    checked={row.effective}
                    disabled={pending === row.module}
                    onCheckedChange={(value) => void apply(row.module, value)}
                  />
                </div>
              );
            })}
          </div>
        )}
      </SheetContent>
    </Sheet>
  );
}
