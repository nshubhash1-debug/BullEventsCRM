"use client";

import * as React from "react";
import { AlertTriangle, Blocks, Check, Clock, UsersRound } from "lucide-react";

import { PropThumb } from "@/components/props/prop-thumb";
import { PagePanel } from "@/components/shell/page-panel";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { formatMoney } from "@/lib/crm-api";
import { humaniseLabel, kitsApi, type PropKit } from "@/lib/resources-api";
import { cn } from "@/lib/utils";

function isoToday(offsetDays = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

export default function PropKitsPage() {
  const [from, setFrom] = React.useState(() => isoToday(7));
  const [to, setTo] = React.useState(() => isoToday(9));
  const [kits, setKits] = React.useState<PropKit[]>([]);
  const [openId, setOpenId] = React.useState<number | null>(null);
  const [loadedKey, setLoadedKey] = React.useState<string | null>(null);

  const queryKey = JSON.stringify({ from, to });
  const loading = loadedKey !== queryKey;

  React.useEffect(() => {
    let cancelled = false;

    kitsApi
      .list({ from, to })
      .then((r) => !cancelled && setKits(r))
      .catch(() => !cancelled && setKits([]))
      .finally(() => !cancelled && setLoadedKey(queryKey));

    return () => {
      cancelled = true;
    };
  }, [from, to, queryKey]);

  const fieldable = kits.filter((k) => k.canFulfil).length;

  return (
    <PagePanel
      icon={Blocks}
      title="Kits"
      hint="Sets that always travel together. Every line is checked against the dates, so a kit that cannot be fielded says so here rather than at the loading bay."
      toolbar={
        <div className="flex flex-wrap items-end gap-2">
          <div>
            <Label className="text-[11px] text-muted-foreground">Dispatch</Label>
            <Input
              type="date"
              className="mt-1 h-9 w-36"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
            />
          </div>
          <div>
            <Label className="text-[11px] text-muted-foreground">Back by</Label>
            <Input
              type="date"
              className="mt-1 h-9 w-36"
              value={to}
              onChange={(e) => setTo(e.target.value)}
            />
          </div>
        </div>
      }
      subToolbar={
        <span className="text-[12px] text-muted-foreground">
          {loading
            ? "Loading…"
            : `${kits.length} kits · ${fieldable} can be fielded between ${from} and ${to}`}
        </span>
      }
    >
      <div className="min-h-0 flex-1 space-y-3 overflow-y-auto px-5 pb-5">
        {kits.map((kit) => {
          const expanded = openId === kit.id;

          return (
            <div key={kit.id} className="overflow-hidden rounded-lg border bg-card">
              <button
                type="button"
                onClick={() => setOpenId(expanded ? null : kit.id)}
                className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-muted/40"
              >
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <span className="font-medium">{kit.name}</span>
                    <span className="font-mono text-[10.5px] text-muted-foreground">
                      {kit.code}
                    </span>
                    {kit.eventType ? (
                      <span className="rounded bg-muted px-1.5 py-0.5 text-[10.5px] text-muted-foreground">
                        {humaniseLabel(kit.eventType)}
                      </span>
                    ) : null}
                  </div>

                  <div className="mt-0.5 flex flex-wrap items-center gap-3 text-[11.5px] text-muted-foreground">
                    <span>
                      {kit.lineCount} items · {kit.totalPieces} pieces
                    </span>
                    {kit.setupHours ? (
                      <span className="inline-flex items-center gap-1">
                        <Clock className="size-3" />
                        {kit.setupHours}h to build
                      </span>
                    ) : null}
                    {kit.crewRequired ? (
                      <span className="inline-flex items-center gap-1">
                        <UsersRound className="size-3" />
                        {kit.crewRequired} crew
                      </span>
                    ) : null}
                    {kit.rentalRatePerDay || kit.lineRateTotal ? (
                      <span>
                        {formatMoney(kit.rentalRatePerDay ?? kit.lineRateTotal)}/day
                      </span>
                    ) : null}
                  </div>
                </div>

                <span
                  className={cn(
                    "shrink-0 rounded px-2 py-1 text-[11.5px] font-medium",
                    kit.canFulfil
                      ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400"
                      : "bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400"
                  )}
                >
                  {kit.canFulfil ? (
                    <>
                      <Check className="mr-1 inline size-3" />
                      Can be fielded
                    </>
                  ) : (
                    <>
                      <AlertTriangle className="mr-1 inline size-3" />
                      {kit.shortLines} essential line{kit.shortLines === 1 ? "" : "s"} short
                    </>
                  )}
                </span>
              </button>

              {expanded ? (
                <div className="border-t px-4 py-3">
                  {kit.description ? (
                    <p className="mb-3 text-[12.5px] text-muted-foreground">
                      {kit.description}
                    </p>
                  ) : null}

                  <table className="w-full text-[12.5px]">
                    <thead>
                      <tr className="border-b text-left text-[10.5px] uppercase tracking-wide text-muted-foreground">
                        <th className="py-1.5 pr-2 font-medium">Item</th>
                        <th className="px-1 py-1.5 text-right font-medium">Needs</th>
                        <th className="px-1 py-1.5 text-right font-medium">In stock</th>
                        <th className="px-1 py-1.5 text-right font-medium">Free</th>
                        <th className="px-1 py-1.5 text-center font-medium">Essential</th>
                      </tr>
                    </thead>
                    <tbody>
                      {kit.lines.map((l) => (
                        <tr key={l.id} className="border-b last:border-0">
                          <td className="py-1.5 pr-2">
                            <div className="flex items-center gap-2">
                              <PropThumb
                                src={l.primaryThumbnailUrl}
                                alt={l.itemName}
                                className="size-7 shrink-0"
                              />
                              <div className="min-w-0">
                                <div className="truncate font-medium">{l.itemName}</div>
                                <div className="font-mono text-[10px] text-muted-foreground">
                                  {l.itemCode}
                                </div>
                              </div>
                            </div>
                          </td>
                          <td className="px-1 py-1.5 text-right tabular-nums font-medium">
                            {l.quantity}
                          </td>
                          <td className="px-1 py-1.5 text-right tabular-nums text-muted-foreground">
                            {l.goodQuantity}
                          </td>
                          <td
                            className={cn(
                              "px-1 py-1.5 text-right tabular-nums font-semibold",
                              l.isShort
                                ? "text-rose-600"
                                : "text-emerald-600 dark:text-emerald-400"
                            )}
                          >
                            {l.availableQuantity ?? "—"}
                          </td>
                          <td className="px-1 py-1.5 text-center text-[11px] text-muted-foreground">
                            {l.isOptional ? "optional" : "yes"}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>

                  <p className="mt-3 text-[11.5px] leading-relaxed text-muted-foreground">
                    Add this kit to a gate pass from the pass itself — every line is
                    re-checked at that moment, and an optional line that falls short is
                    reduced rather than refused.
                  </p>
                </div>
              ) : null}
            </div>
          );
        })}

        {!loading && kits.length === 0 ? (
          <p className="py-16 text-center text-[13px] text-muted-foreground">
            No kits yet.
          </p>
        ) : null}
      </div>
    </PagePanel>
  );
}
