"use client";

import * as React from "react";
import { History, PackageMinus, PackagePlus, Wrench } from "lucide-react";
import { toast } from "sonner";

import { PropThumb } from "@/components/props/prop-thumb";
import { Badge } from "@/components/ui/badge";
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ApiError } from "@/lib/api";
import { formatDateTime, formatMoney } from "@/lib/crm-api";
import {
  PROP_MANUAL_MOVEMENTS,
  propsApi,
  type PropItem,
  type PropMovement,
} from "@/lib/props-api";
import { cn } from "@/lib/utils";

/** A single labelled fact in the detail pane. Skipped entirely when empty. */
function Fact({ label, value }: { label: string; value: React.ReactNode }) {
  if (value === null || value === undefined || value === "" || value === "—") return null;

  return (
    <div className="flex items-baseline justify-between gap-3 border-b py-1.5 text-[13px] last:border-0">
      <span className="shrink-0 text-muted-foreground">{label}</span>
      <span className="text-right font-medium">{value}</span>
    </div>
  );
}

/** How a ledger row's sign should read — green for in, amber for out. */
function movementTone(type: string) {
  if (["Opening", "Purchase", "Return", "Repair", "TransferIn"].includes(type)) {
    return "text-emerald-600 dark:text-emerald-400";
  }
  if (["IssueOut", "Consumed", "WriteOff", "Lost", "TransferOut"].includes(type)) {
    return "text-amber-600 dark:text-amber-400";
  }
  return "text-muted-foreground";
}

export function PropDetailSheet({
  item,
  open,
  onOpenChange,
  onChanged,
}: {
  item: PropItem | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onChanged?: (item: PropItem) => void;
}) {
  const [photo, setPhoto] = React.useState(0);
  const [movements, setMovements] = React.useState<PropMovement[] | null>(null);

  const [movementType, setMovementType] = React.useState<string>("Purchase");
  const [quantity, setQuantity] = React.useState("1");
  const [notes, setNotes] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  // Mounted under a key of the item id by the parent, so a different item is a
  // fresh component — the photo index, the form and the stock card all start
  // clean without an effect unpicking the last item's state.
  const loadMovements = React.useCallback(() => {
    if (!item) return;
    propsApi.movements(item.id, 60).then(setMovements).catch(() => setMovements([]));
  }, [item]);

  if (!item) return null;

  async function record() {
    if (!item) return;
    const amount = Number(quantity);
    if (!Number.isFinite(amount) || amount <= 0) {
      toast.error("Enter how many pieces moved.");
      return;
    }

    setSaving(true);
    try {
      const updated = await propsApi.recordMovement({
        propItemId: item.id,
        movementType,
        quantity: amount,
        notes: notes || null,
        isIncrease: true,
      });
      toast.success("Stock updated. The movement is on the item's card.");
      setQuantity("1");
      setNotes("");
      setMovements(null);
      loadMovements();
      onChanged?.(updated);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not record that.");
    } finally {
      setSaving(false);
    }
  }

  const photos = item.photos.length > 0 ? item.photos : [];
  const current = photos[photo];

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="flex w-full flex-col gap-0 overflow-y-auto p-0 sm:max-w-xl">
        <SheetHeader className="border-b px-5 py-4">
          <SheetTitle className="text-[15px]">{item.name}</SheetTitle>
          <SheetDescription className="font-mono text-xs">
            {item.code} · {item.categoryName}
          </SheetDescription>
        </SheetHeader>

        <div className="px-5 py-4">
          <PropThumb
            src={current?.url ?? item.primaryPhotoUrl}
            alt={item.name}
            className="h-56 w-full rounded-lg border object-contain"
          />

          {photos.length > 1 ? (
            <div className="mt-2 flex flex-wrap gap-1.5">
              {photos.map((p, index) => (
                <button
                  key={p.id}
                  type="button"
                  onClick={() => setPhoto(index)}
                  className={cn(
                    "overflow-hidden rounded border",
                    index === photo ? "ring-2 ring-primary" : "opacity-70 hover:opacity-100"
                  )}
                >
                  <PropThumb
                    src={p.thumbnailUrl ?? p.url}
                    alt={`${item.name} ${index + 1}`}
                    className="size-12"
                    rounded="rounded-none"
                  />
                </button>
              ))}
            </div>
          ) : null}

          <div className="mt-4 grid grid-cols-3 gap-2 text-center">
            <div className="rounded-lg border bg-emerald-50 py-2 dark:bg-emerald-950/30">
              <div className="text-lg font-semibold text-emerald-700 dark:text-emerald-400">
                {item.goodQuantity}
              </div>
              <div className="text-[11px] text-muted-foreground">Usable</div>
            </div>
            <div className="rounded-lg border bg-amber-50 py-2 dark:bg-amber-950/30">
              <div className="text-lg font-semibold text-amber-700 dark:text-amber-400">
                {item.repairableQuantity}
              </div>
              <div className="text-[11px] text-muted-foreground">Repairable</div>
            </div>
            <div className="rounded-lg border bg-rose-50 py-2 dark:bg-rose-950/30">
              <div className="text-lg font-semibold text-rose-700 dark:text-rose-400">
                {item.damagedQuantity}
              </div>
              <div className="text-[11px] text-muted-foreground">Damaged</div>
            </div>
          </div>
        </div>

        <Tabs defaultValue="details" className="flex-1">
          <TabsList className="mx-5">
            <TabsTrigger value="details">Details</TabsTrigger>
            <TabsTrigger value="stock" onClick={loadMovements}>
              Stock card
            </TabsTrigger>
          </TabsList>

          <TabsContent value="details" className="px-5 pb-6">
            <Fact label="Size" value={item.size} />
            <Fact label="Colour" value={item.colour} />
            <Fact label="Material" value={item.material} />
            <Fact label="Unit" value={item.unit} />
            <Fact label="Type" value={item.itemType} />
            <Fact
              label="Ownership"
              value={item.ownership === "SubHired" ? "Sub-hired" : item.ownership}
            />
            <Fact label="Status" value={item.status} />
            <Fact label="Store" value={item.storeName} />
            <Fact label="Storage location" value={item.storageLocation} />
            <Fact
              label="Rental per day"
              value={item.rentalRatePerDay ? formatMoney(item.rentalRatePerDay) : null}
            />
            <Fact
              label="Replacement value"
              value={item.replacementValue ? formatMoney(item.replacementValue) : null}
            />
            <Fact label="Supplier" value={item.supplierName} />
            <Fact label="Weight" value={item.weightKg ? `${item.weightKg} kg` : null} />
            <Fact
              label="Turnaround"
              value={item.turnaroundDays > 0 ? `${item.turnaroundDays} days` : null}
            />
            <Fact
              label="Reorder level"
              value={item.reorderLevel > 0 ? item.reorderLevel : null}
            />

            {item.isFragile || item.isSerialised ? (
              <div className="mt-3 flex gap-1.5">
                {item.isFragile ? <Badge variant="outline">Fragile</Badge> : null}
                {item.isSerialised ? <Badge variant="outline">Serialised</Badge> : null}
              </div>
            ) : null}
          </TabsContent>

          <TabsContent value="stock" className="px-5 pb-6">
            <div className="rounded-lg border p-3">
              <div className="mb-2 flex items-center gap-1.5 text-[13px] font-medium">
                <Wrench className="size-3.5 text-muted-foreground" />
                Record a movement
              </div>

              <div className="grid gap-2">
                <div>
                  <Label className="text-[11px] text-muted-foreground">Reason</Label>
                  <select
                    className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm"
                    value={movementType}
                    onChange={(e) => setMovementType(e.target.value)}
                  >
                    {PROP_MANUAL_MOVEMENTS.map((m) => (
                      <option key={m.value} value={m.value}>
                        {m.label}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="flex gap-2">
                  <div className="w-24">
                    <Label className="text-[11px] text-muted-foreground">Pieces</Label>
                    <Input
                      className="mt-1 h-9"
                      value={quantity}
                      onChange={(e) => setQuantity(e.target.value)}
                    />
                  </div>
                  <div className="flex-1">
                    <Label className="text-[11px] text-muted-foreground">Note</Label>
                    <Input
                      className="mt-1 h-9"
                      placeholder="Optional"
                      value={notes}
                      onChange={(e) => setNotes(e.target.value)}
                    />
                  </div>
                </div>

                <Button className="h-9" disabled={saving} onClick={() => void record()}>
                  {saving ? "Saving…" : "Record"}
                </Button>
              </div>

              <p className="mt-2 text-[11px] leading-relaxed text-muted-foreground">
                Dispatch and return are not here on purpose — they belong to a gate
                pass, which releases the reservation as the stock comes back.
              </p>
            </div>

            <div className="mt-4">
              <div className="mb-2 flex items-center gap-1.5 text-[13px] font-medium">
                <History className="size-3.5 text-muted-foreground" />
                Movement history
              </div>

              {movements === null ? (
                <p className="py-4 text-center text-[13px] text-muted-foreground">
                  Loading…
                </p>
              ) : movements.length === 0 ? (
                <p className="py-4 text-center text-[13px] text-muted-foreground">
                  Nothing recorded yet.
                </p>
              ) : (
                <ul className="space-y-1">
                  {movements.map((m) => (
                    <li
                      key={m.id}
                      className="flex items-start justify-between gap-3 rounded border px-3 py-2 text-[12px]"
                    >
                      <div className="min-w-0">
                        <div className={cn("font-medium", movementTone(m.movementType))}>
                          {m.quantity > 0 ? (
                            <PackagePlus className="mr-1 inline size-3" />
                          ) : (
                            <PackageMinus className="mr-1 inline size-3" />
                          )}
                          {m.movementType}
                          {m.issueCode ? (
                            <span className="ml-1 font-mono text-muted-foreground">
                              {m.issueCode}
                            </span>
                          ) : null}
                        </div>
                        {m.notes ? (
                          <div className="truncate text-muted-foreground">{m.notes}</div>
                        ) : null}
                        <div className="text-[11px] text-muted-foreground">
                          {formatDateTime(m.movedAt)}
                          {m.recordedBy ? ` · ${m.recordedBy}` : ""}
                        </div>
                      </div>

                      <div className="shrink-0 text-right">
                        <div className={cn("font-semibold", movementTone(m.movementType))}>
                          {m.quantity > 0 ? "+" : ""}
                          {m.quantity}
                        </div>
                        <div className="text-[11px] text-muted-foreground">
                          bal {m.balanceAfter}
                        </div>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </TabsContent>
        </Tabs>
      </SheetContent>
    </Sheet>
  );
}
