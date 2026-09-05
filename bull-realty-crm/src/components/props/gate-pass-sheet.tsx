"use client";

import * as React from "react";
import { Check, PackageCheck, Plus, Search, Trash2, Truck, X } from "lucide-react";
import { toast } from "sonner";

import { PropThumb } from "@/components/props/prop-thumb";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { ApiError } from "@/lib/api";
import { formatMoney } from "@/lib/crm-api";
import {
  PROP_ISSUE_STATUS_LABELS,
  propsApi,
  type PropAvailability,
  type PropIssue,
} from "@/lib/props-api";
import { cn } from "@/lib/utils";

/** The per-line numbers the return form collects, keyed by line id. */
type ReturnDraft = Record<
  number,
  { returned: string; damaged: string; lost: string; consumed: string }
>;

export function GatePassSheet({
  issueId,
  open,
  onOpenChange,
  onChanged,
}: {
  issueId: number | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onChanged?: () => void;
}) {
  const [issue, setIssue] = React.useState<PropIssue | null>(null);
  const [busy, setBusy] = React.useState(false);

  const [picker, setPicker] = React.useState(false);
  const [search, setSearch] = React.useState("");
  const [candidates, setCandidates] = React.useState<PropAvailability[]>([]);
  const [searching, setSearching] = React.useState(false);

  const [mode, setMode] = React.useState<"view" | "dispatch" | "return">("view");

  /*
   * The count forms hold only what a person actually typed. Anything untouched
   * falls back to the pass's own numbers at render time — every reserved piece
   * goes on the truck, every issued piece comes back — so the common case is
   * one click, and the defaults follow the server after each action instead of
   * being copied into state and going stale.
   */
  const [dispatchDraft, setDispatchDraft] = React.useState<Record<number, string>>({});
  const [returnDraft, setReturnDraft] = React.useState<ReturnDraft>({});
  const [vehicle, setVehicle] = React.useState<string | null>(null);
  const [driver, setDriver] = React.useState<string | null>(null);

  const load = React.useCallback(() => {
    if (issueId === null) return;
    propsApi.issue(issueId).then(setIssue).catch(() => setIssue(null));
  }, [issueId]);

  // Mounted under a key of the pass id by the parent, so a different pass is a
  // fresh component and none of this needs resetting by hand.
  React.useEffect(() => load(), [load]);

  function dispatchValue(lineId: number, fallback: number) {
    return dispatchDraft[lineId] ?? String(fallback);
  }

  function returnValue(lineId: number, field: keyof ReturnDraft[number], fallback: number) {
    return returnDraft[lineId]?.[field] ?? (field === "returned" ? String(fallback) : "0");
  }

  const searchProps = React.useCallback(async () => {
    if (!issue) return;
    setSearching(true);
    try {
      // Availability is asked for over this pass's own window, with the pass
      // excluded — so the number shown is what it can still take, not what it
      // would compete with itself for.
      const all = await propsApi.availability({
        from: issue.dispatchDate.slice(0, 10),
        to: issue.expectedReturnDate.slice(0, 10),
        excludePropIssueId: issue.id,
      });

      const needle = search.trim().toLowerCase();
      setCandidates(
        (needle
          ? all.filter(
              (a) =>
                a.itemName.toLowerCase().includes(needle) ||
                a.itemCode.toLowerCase().includes(needle)
            )
          : all
        ).slice(0, 60)
      );
    } catch {
      setCandidates([]);
    } finally {
      setSearching(false);
    }
  }, [issue, search]);

  React.useEffect(() => {
    if (!picker) return;
    const timer = setTimeout(() => void searchProps(), 250);
    return () => clearTimeout(timer);
  }, [picker, search, searchProps]);

  /**
   * Runs one gate-pass action and folds its result straight back into the
   * sheet. Every mutating call on the pass returns the whole pass, so the
   * sheet re-renders from the server's answer rather than from a guess about
   * what the action did.
   */
  async function act(work: () => Promise<PropIssue>, success: string) {
    setBusy(true);
    try {
      setIssue(await work());
      toast.success(success);
      onChanged?.();
      return true;
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "That did not work.");
      return false;
    } finally {
      setBusy(false);
    }
  }

  if (!issue) {
    return (
      <Sheet open={open} onOpenChange={onOpenChange}>
        <SheetContent className="w-full sm:max-w-2xl">
          <SheetHeader>
            <SheetTitle>Gate pass</SheetTitle>
            <SheetDescription>Loading…</SheetDescription>
          </SheetHeader>
        </SheetContent>
      </Sheet>
    );
  }

  const editable = issue.status === "Draft";
  const canReserve = issue.status === "Draft" && issue.lines.length > 0;
  const canDispatch = issue.status === "Reserved" || issue.status === "Draft";
  const canReturn = issue.status === "Dispatched" || issue.status === "PartiallyReturned";
  const canClose = issue.status === "Returned";

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="flex w-full flex-col gap-0 overflow-y-auto p-0 sm:max-w-2xl">
        <SheetHeader className="border-b px-5 py-4">
          <SheetTitle className="flex items-center gap-2 text-[15px]">
            {issue.eventName ?? "Gate pass"}
            <span className="rounded bg-muted px-1.5 py-0.5 text-[11px] font-medium text-muted-foreground">
              {PROP_ISSUE_STATUS_LABELS[issue.status] ?? issue.status}
            </span>
          </SheetTitle>
          <SheetDescription className="text-xs">
            <span className="font-mono">{issue.code}</span>
            {issue.clientName ? ` · ${issue.clientName}` : ""}
            {issue.venueName ? ` · ${issue.venueName}` : ""}
            {" · out "}
            {issue.dispatchDate.slice(0, 10)}
            {" → back "}
            {issue.expectedReturnDate.slice(0, 10)}
          </SheetDescription>
        </SheetHeader>

        {/* ---------------- actions ---------------- */}
        <div className="flex flex-wrap gap-2 border-b px-5 py-3">
          {canReserve ? (
            <Button
              size="sm"
              className="h-8"
              disabled={busy}
              onClick={() =>
                void act(() => propsApi.reserve(issue.id), "Reserved — the stock is held for these dates.")
              }
            >
              <Check className="mr-1 size-3.5" />
              Reserve stock
            </Button>
          ) : null}

          {canDispatch ? (
            <Button
              size="sm"
              variant={mode === "dispatch" ? "secondary" : "outline"}
              className="h-8"
              onClick={() => setMode(mode === "dispatch" ? "view" : "dispatch")}
            >
              <Truck className="mr-1 size-3.5" />
              Dispatch
            </Button>
          ) : null}

          {canReturn ? (
            <Button
              size="sm"
              variant={mode === "return" ? "secondary" : "outline"}
              className="h-8"
              onClick={() => setMode(mode === "return" ? "view" : "return")}
            >
              <PackageCheck className="mr-1 size-3.5" />
              Record return
            </Button>
          ) : null}

          {canClose ? (
            <Button
              size="sm"
              variant="outline"
              className="h-8"
              disabled={busy}
              onClick={() => void act(() => propsApi.close(issue.id), "Closed.")}
            >
              Close pass
            </Button>
          ) : null}

          {editable ? (
            <Button
              size="sm"
              variant="outline"
              className="h-8"
              onClick={() => setPicker((v) => !v)}
            >
              <Plus className="mr-1 size-3.5" />
              Add props
            </Button>
          ) : null}
        </div>

        {/* ---------------- picker ---------------- */}
        {picker && editable ? (
          <div className="border-b bg-muted/30 px-5 py-3">
            <div className="relative mb-2">
              <Search className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
              <Input
                className="h-9 pl-8"
                placeholder="Search the godown…"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>

            <div className="max-h-64 space-y-1 overflow-y-auto">
              {searching ? (
                <p className="py-3 text-center text-[12px] text-muted-foreground">Searching…</p>
              ) : (
                candidates.map((c) => {
                  const already = issue.lines.find((l) => l.propItemId === c.propItemId);

                  return (
                    <div
                      key={c.propItemId}
                      className="flex items-center gap-2 rounded border bg-card px-2 py-1.5"
                    >
                      <PropThumb
                        src={c.primaryThumbnailUrl}
                        alt={c.itemName}
                        className="size-8 shrink-0"
                      />
                      <div className="min-w-0 flex-1">
                        <div className="truncate text-[12.5px] font-medium">{c.itemName}</div>
                        <div className="text-[10.5px] text-muted-foreground">
                          <span className="font-mono">{c.itemCode}</span> ·{" "}
                          <span
                            className={cn(
                              c.availableQuantity === 0
                                ? "text-rose-600"
                                : "text-emerald-600"
                            )}
                          >
                            {c.availableQuantity} free
                          </span>{" "}
                          of {c.goodQuantity}
                          {already ? ` · ${already.reservedQuantity} on this pass` : ""}
                        </div>
                      </div>

                      <AddLineControl
                        max={c.availableQuantity}
                        disabled={busy || c.availableQuantity <= 0}
                        onAdd={(quantity) =>
                          void act(
                            () =>
                              propsApi.addLine(issue.id, {
                                propItemId: c.propItemId,
                                quantity,
                              }),
                            `${quantity} × ${c.itemName} added.`
                          ).then((ok) => {
                            if (ok) void searchProps();
                          })
                        }
                      />
                    </div>
                  );
                })
              )}

              {!searching && candidates.length === 0 ? (
                <p className="py-3 text-center text-[12px] text-muted-foreground">
                  Nothing free matches that over these dates.
                </p>
              ) : null}
            </div>
          </div>
        ) : null}

        {/* ---------------- lines ---------------- */}
        <div className="flex-1 px-5 py-4">
          {issue.lines.length === 0 ? (
            <p className="py-10 text-center text-[13px] text-muted-foreground">
              Nothing on this pass yet.
            </p>
          ) : (
            <table className="w-full text-[12.5px]">
              <thead>
                <tr className="border-b text-left text-[10.5px] uppercase tracking-wide text-muted-foreground">
                  <th className="py-1.5 pr-2 font-medium">Item</th>
                  <th className="py-1.5 px-1 text-right font-medium">Res</th>
                  <th className="py-1.5 px-1 text-right font-medium">Out</th>
                  <th className="py-1.5 px-1 text-right font-medium">Back</th>
                  <th className="py-1.5 px-1 text-right font-medium">Dmg</th>
                  <th className="py-1.5 px-1 text-right font-medium">Lost</th>
                  <th className="py-1.5 px-1 text-right font-medium">Pend</th>
                  {mode === "dispatch" ? (
                    <th className="py-1.5 px-1 text-right font-medium">Loading</th>
                  ) : null}
                  {mode === "return" ? (
                    <th className="py-1.5 px-1 text-center font-medium" colSpan={4}>
                      Counted back — good / damaged / lost / used
                    </th>
                  ) : null}
                  {editable ? <th className="w-8" /> : null}
                </tr>
              </thead>

              <tbody>
                {issue.lines.map((line) => (
                  <tr key={line.id} className="border-b last:border-0">
                    <td className="py-1.5 pr-2">
                      <div className="flex items-center gap-2">
                        <PropThumb
                          src={line.primaryThumbnailUrl}
                          alt={line.itemName}
                          className="size-7 shrink-0"
                        />
                        <div className="min-w-0">
                          <div className="truncate font-medium">{line.itemName}</div>
                          <div className="font-mono text-[10px] text-muted-foreground">
                            {line.itemCode}
                          </div>
                        </div>
                      </div>
                    </td>
                    <td className="px-1 py-1.5 text-right tabular-nums">
                      {line.reservedQuantity}
                    </td>
                    <td className="px-1 py-1.5 text-right tabular-nums">
                      {line.issuedQuantity || "—"}
                    </td>
                    <td className="px-1 py-1.5 text-right tabular-nums">
                      {line.returnedQuantity || "—"}
                    </td>
                    <td className="px-1 py-1.5 text-right tabular-nums text-rose-600">
                      {line.damagedQuantity || "—"}
                    </td>
                    <td className="px-1 py-1.5 text-right tabular-nums text-rose-600">
                      {line.lostQuantity || "—"}
                    </td>
                    <td
                      className={cn(
                        "px-1 py-1.5 text-right tabular-nums",
                        line.pendingQuantity > 0 ? "font-semibold text-amber-600" : ""
                      )}
                    >
                      {line.pendingQuantity || "—"}
                    </td>

                    {mode === "dispatch" ? (
                      <td className="px-1 py-1.5 text-right">
                        <Input
                          className="h-7 w-16 text-right text-[12px]"
                          value={dispatchValue(line.id, line.reservedQuantity)}
                          onChange={(e) =>
                            setDispatchDraft((d) => ({ ...d, [line.id]: e.target.value }))
                          }
                        />
                      </td>
                    ) : null}

                    {mode === "return" ? (
                      <>
                        {(["returned", "damaged", "lost", "consumed"] as const).map((field) => (
                          <td key={field} className="px-0.5 py-1.5">
                            <Input
                              className="h-7 w-12 text-right text-[12px]"
                              value={returnValue(line.id, field, line.pendingQuantity)}
                              onChange={(e) =>
                                setReturnDraft((d) => ({
                                  ...d,
                                  // Written out in full rather than spread over
                                  // the previous draft: until a field is typed
                                  // into it has no entry at all, and a partial
                                  // object here would lose the fallbacks.
                                  [line.id]: {
                                    returned: returnValue(line.id, "returned", line.pendingQuantity),
                                    damaged: returnValue(line.id, "damaged", 0),
                                    lost: returnValue(line.id, "lost", 0),
                                    consumed: returnValue(line.id, "consumed", 0),
                                    [field]: e.target.value,
                                  },
                                }))
                              }
                            />
                          </td>
                        ))}
                      </>
                    ) : null}

                    {editable ? (
                      <td className="py-1.5">
                        <button
                          type="button"
                          className="text-muted-foreground hover:text-destructive"
                          onClick={() =>
                            void act(
                              () => propsApi.removeLine(issue.id, line.id),
                              "Removed."
                            )
                          }
                        >
                          <Trash2 className="size-3.5" />
                        </button>
                      </td>
                    ) : null}
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {mode === "dispatch" ? (
            <div className="mt-4 rounded-lg border p-3">
              <div className="mb-2 grid grid-cols-2 gap-2">
                <div>
                  <Label className="text-[11px] text-muted-foreground">Vehicle</Label>
                  <Input
                    className="mt-1 h-9"
                    placeholder="MH 01 AB 1234"
                    value={vehicle ?? issue.vehicleNumber ?? ""}
                    onChange={(e) => setVehicle(e.target.value)}
                  />
                </div>
                <div>
                  <Label className="text-[11px] text-muted-foreground">Driver</Label>
                  <Input
                    className="mt-1 h-9"
                    value={driver ?? issue.driverName ?? ""}
                    onChange={(e) => setDriver(e.target.value)}
                  />
                </div>
              </div>

              <div className="flex gap-2">
                <Button
                  className="h-9 flex-1"
                  disabled={busy}
                  onClick={() =>
                    void act(
                      () =>
                        propsApi.dispatch(issue.id, {
                          lines: issue.lines.map((l) => ({
                            propIssueLineId: l.id,
                            issuedQuantity:
                              Number(dispatchValue(l.id, l.reservedQuantity)) || 0,
                          })),
                          vehicleNumber: vehicle ?? issue.vehicleNumber ?? null,
                          driverName: driver ?? issue.driverName ?? null,
                        }),
                      "Dispatched. The stock is off the shelf."
                    ).then((ok) => ok && setMode("view"))
                  }
                >
                  Confirm dispatch
                </Button>
                <Button variant="outline" className="h-9" onClick={() => setMode("view")}>
                  <X className="size-3.5" />
                </Button>
              </div>
            </div>
          ) : null}

          {mode === "return" ? (
            <div className="mt-4 rounded-lg border p-3">
              <p className="mb-2 text-[11.5px] leading-relaxed text-muted-foreground">
                Good pieces go back on the shelf, damaged ones into the damaged
                bucket, lost ones off the books and onto the client&apos;s bill.
                Used-up stock — petals, candles — is never chased.
              </p>

              <div className="flex gap-2">
                <Button
                  className="h-9 flex-1"
                  disabled={busy}
                  onClick={() =>
                    void act(
                      () =>
                        propsApi.returnStock(issue.id, {
                          lines: issue.lines
                            .filter((l) => l.pendingQuantity > 0)
                            .map((l) => {
                              return {
                                propIssueLineId: l.id,
                                returnedQuantity:
                                  Number(returnValue(l.id, "returned", l.pendingQuantity)) || 0,
                                damagedQuantity: Number(returnValue(l.id, "damaged", 0)) || 0,
                                lostQuantity: Number(returnValue(l.id, "lost", 0)) || 0,
                                consumedQuantity: Number(returnValue(l.id, "consumed", 0)) || 0,
                              };
                            }),
                        }),
                      "Return recorded."
                    ).then((ok) => ok && setMode("view"))
                  }
                >
                  Confirm return
                </Button>
                <Button variant="outline" className="h-9" onClick={() => setMode("view")}>
                  <X className="size-3.5" />
                </Button>
              </div>
            </div>
          ) : null}

          <div className="mt-4 flex items-center justify-between border-t pt-3 text-[13px]">
            <span className="text-muted-foreground">
              {issue.lineCount} items · {issue.totalIssued || issue.totalReserved} pieces
              {issue.totalPending > 0 ? ` · ${issue.totalPending} still out` : ""}
            </span>
            <span className="font-semibold">{formatMoney(issue.estimatedValue)}</span>
          </div>

          {issue.damageRecovery ? (
            <div className="mt-1 text-right text-[12px] text-rose-600">
              Damage & loss recovery: {formatMoney(issue.damageRecovery)}
            </div>
          ) : null}
        </div>
      </SheetContent>
    </Sheet>
  );
}

/** Quantity box plus an add button, used once per row in the picker. */
function AddLineControl({
  max,
  disabled,
  onAdd,
}: {
  max: number;
  disabled: boolean;
  onAdd: (quantity: number) => void;
}) {
  const [value, setValue] = React.useState("1");

  return (
    <div className="flex shrink-0 items-center gap-1">
      <Input
        className="h-7 w-14 text-right text-[12px]"
        value={value}
        onChange={(e) => setValue(e.target.value)}
      />
      <Button
        size="sm"
        className="h-7 px-2"
        disabled={disabled}
        onClick={() => {
          const quantity = Number(value);
          if (!Number.isFinite(quantity) || quantity <= 0) return;
          if (quantity > max) {
            toast.error(`Only ${max} free over these dates.`);
            return;
          }
          onAdd(quantity);
        }}
      >
        <Plus className="size-3.5" />
      </Button>
    </div>
  );
}
