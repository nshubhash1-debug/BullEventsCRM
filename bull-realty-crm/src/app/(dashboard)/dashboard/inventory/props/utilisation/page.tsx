"use client";

import * as React from "react";
import { TrendingUp } from "lucide-react";

import { PropThumb } from "@/components/props/prop-thumb";
import { PagePanel } from "@/components/shell/page-panel";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { formatMoney } from "@/lib/crm-api";
import { kitsApi, type Utilisation, type UtilisationRow } from "@/lib/resources-api";
import { cn } from "@/lib/utils";

function isoToday(offsetDays = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

function Row({ row, showRate }: { row: UtilisationRow; showRate: boolean }) {
  return (
    <tr className="border-b last:border-0">
      <td className="py-1.5 pr-2">
        <div className="flex items-center gap-2">
          <PropThumb
            src={row.primaryThumbnailUrl}
            alt={row.itemName}
            className="size-8 shrink-0"
          />
          <div className="min-w-0">
            <div className="truncate font-medium">{row.itemName}</div>
            <div className="font-mono text-[10px] text-muted-foreground">
              {row.itemCode} · {row.categoryName}
            </div>
          </div>
        </div>
      </td>
      <td className="px-2 py-1.5 text-right tabular-nums text-muted-foreground">
        {row.goodQuantity}
      </td>
      {showRate ? (
        <>
          <td className="px-2 py-1.5 text-right tabular-nums">{row.timesIssued}</td>
          <td className="px-2 py-1.5 text-right tabular-nums">{row.piecesIssued}</td>
          <td className="px-2 py-1.5 text-right tabular-nums">{row.daysOut}</td>
          <td className="px-2 py-1.5">
            <div className="flex items-center gap-2">
              <div className="h-1.5 w-16 overflow-hidden rounded bg-muted">
                <div
                  className="h-full rounded bg-primary/70"
                  style={{ width: `${Math.min(100, row.utilisationRate * 100)}%` }}
                />
              </div>
              <span className="tabular-nums text-[11.5px]">
                {Math.round(row.utilisationRate * 100)}%
              </span>
            </div>
          </td>
          <td className="px-2 py-1.5 text-right tabular-nums text-muted-foreground">
            {row.estimatedRevenue ? formatMoney(row.estimatedRevenue) : "—"}
          </td>
          <td
            className={cn(
              "px-2 py-1.5 text-right tabular-nums",
              row.lossValue > 0 ? "text-rose-600" : "text-muted-foreground"
            )}
          >
            {row.lossValue > 0 ? formatMoney(row.lossValue) : "—"}
          </td>
        </>
      ) : (
        <td className="px-2 py-1.5 text-right text-[11.5px] text-muted-foreground">
          never left the godown
        </td>
      )}
    </tr>
  );
}

export default function UtilisationPage() {
  const [from, setFrom] = React.useState(() => isoToday(-365));
  const [to, setTo] = React.useState(() => isoToday());
  const [data, setData] = React.useState<Utilisation | null>(null);
  const [loadedKey, setLoadedKey] = React.useState<string | null>(null);

  const queryKey = JSON.stringify({ from, to });
  const loading = loadedKey !== queryKey;

  React.useEffect(() => {
    let cancelled = false;

    kitsApi
      .utilisation({ from, to, take: 25 })
      .then((r) => !cancelled && setData(r))
      .catch(() => !cancelled && setData(null))
      .finally(() => !cancelled && setLoadedKey(queryKey));

    return () => {
      cancelled = true;
    };
  }, [from, to, queryKey]);

  return (
    <PagePanel
      icon={TrendingUp}
      title="Utilisation"
      hint="Which pieces earn their shelf space and which have not moved. Read straight off the stock ledger."
      toolbar={
        <div className="flex flex-wrap items-end gap-2">
          <div>
            <Label className="text-[11px] text-muted-foreground">From</Label>
            <Input
              type="date"
              className="mt-1 h-9 w-36"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
            />
          </div>
          <div>
            <Label className="text-[11px] text-muted-foreground">To</Label>
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
          {loading || !data
            ? "Loading…"
            : `${data.itemsTracked} items over ${data.windowDays} days · ${data.itemsNeverIssued} never issued`}
        </span>
      }
    >
      <div className="min-h-0 flex-1 space-y-6 overflow-y-auto px-5 pb-5">
        {!data ? (
          <p className="py-16 text-center text-[13px] text-muted-foreground">Loading…</p>
        ) : (
          <>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <div className="rounded-lg border bg-card p-3">
                <div className="text-[11px] uppercase tracking-wide text-muted-foreground">
                  Items tracked
                </div>
                <div className="mt-1 text-2xl font-semibold tabular-nums">
                  {data.itemsTracked.toLocaleString()}
                </div>
              </div>
              <div className="rounded-lg border bg-card p-3">
                <div className="text-[11px] uppercase tracking-wide text-muted-foreground">
                  Never issued
                </div>
                <div className="mt-1 text-2xl font-semibold tabular-nums text-amber-600">
                  {data.itemsNeverIssued.toLocaleString()}
                </div>
              </div>
              <div className="rounded-lg border bg-card p-3">
                <div className="text-[11px] uppercase tracking-wide text-muted-foreground">
                  Rental earned
                </div>
                <div className="mt-1 text-2xl font-semibold tabular-nums text-emerald-600 dark:text-emerald-400">
                  {formatMoney(data.estimatedRevenue)}
                </div>
              </div>
              <div className="rounded-lg border bg-card p-3">
                <div className="text-[11px] uppercase tracking-wide text-muted-foreground">
                  Breakage & loss
                </div>
                <div className="mt-1 text-2xl font-semibold tabular-nums text-rose-600">
                  {formatMoney(data.lossValue)}
                </div>
              </div>
            </div>

            {data.estimatedRevenue === 0 ? (
              <div className="rounded-lg border border-dashed px-4 py-2.5 text-[12.5px] text-muted-foreground">
                Rental earned reads zero because the catalogue has no rates on it yet —
                the days-out figures below are still real.
              </div>
            ) : null}

            <section>
              <h2 className="mb-2 text-[13px] font-semibold">Hardest working</h2>
              {data.busiest.length === 0 ? (
                <p className="rounded-lg border border-dashed py-8 text-center text-[12.5px] text-muted-foreground">
                  Nothing has been dispatched over this window yet.
                </p>
              ) : (
                <div className="overflow-x-auto rounded-lg border">
                  <table className="w-full min-w-[820px] text-[12.5px]">
                    <thead className="bg-muted/40">
                      <tr className="text-left text-[10.5px] uppercase tracking-wide text-muted-foreground">
                        <th className="px-3 py-2 font-medium">Item</th>
                        <th className="px-2 py-2 text-right font-medium">Stock</th>
                        <th className="px-2 py-2 text-right font-medium">Trips</th>
                        <th className="px-2 py-2 text-right font-medium">Pieces</th>
                        <th className="px-2 py-2 text-right font-medium">Days out</th>
                        <th className="px-2 py-2 font-medium">Utilisation</th>
                        <th className="px-2 py-2 text-right font-medium">Rental</th>
                        <th className="px-2 py-2 text-right font-medium">Loss</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.busiest.map((r) => (
                        <Row key={r.propItemId} row={r} showRate />
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </section>

            <section>
              <h2 className="mb-1 text-[13px] font-semibold">Gathering dust</h2>
              <p className="mb-2 text-[12px] text-muted-foreground">
                Ranked by how much stock is sitting idle — the biggest holdings are the
                ones worth a decision.
              </p>
              <div className="overflow-x-auto rounded-lg border">
                <table className="w-full text-[12.5px]">
                  <thead className="bg-muted/40">
                    <tr className="text-left text-[10.5px] uppercase tracking-wide text-muted-foreground">
                      <th className="px-3 py-2 font-medium">Item</th>
                      <th className="px-2 py-2 text-right font-medium">Stock</th>
                      <th className="px-2 py-2 text-right font-medium">Activity</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.idle.map((r) => (
                      <Row key={r.propItemId} row={r} showRate={false} />
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          </>
        )}
      </div>
    </PagePanel>
  );
}
