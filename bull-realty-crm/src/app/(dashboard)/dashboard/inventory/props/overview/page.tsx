"use client";

import * as React from "react";
import Link from "next/link";
import { AlertTriangle, Boxes, PackageCheck, Truck } from "lucide-react";

import { PropThumb } from "@/components/props/prop-thumb";
import { PagePanel } from "@/components/shell/page-panel";
import { formatMoney } from "@/lib/crm-api";
import { propsApi, type PropDashboard } from "@/lib/props-api";
import { cn } from "@/lib/utils";

function Tile({
  label,
  value,
  hint,
  tone = "",
}: {
  label: string;
  value: React.ReactNode;
  hint?: string;
  tone?: string;
}) {
  return (
    <div className="rounded-lg border bg-card p-3">
      <div className="text-[11px] uppercase tracking-wide text-muted-foreground">{label}</div>
      <div className={cn("mt-1 text-2xl font-semibold tabular-nums", tone)}>{value}</div>
      {hint ? <div className="mt-0.5 text-[11px] text-muted-foreground">{hint}</div> : null}
    </div>
  );
}

export default function PropOverviewPage() {
  const [data, setData] = React.useState<PropDashboard | null>(null);

  React.useEffect(() => {
    propsApi.dashboard().then(setData).catch(() => setData(null));
  }, []);

  const widest = Math.max(1, ...(data?.categories.map((c) => c.goodQuantity) ?? [1]));

  return (
    <PagePanel
      icon={Boxes}
      title="Godown overview"
      hint="What is on the shelf, what is out on events, and what is not coming back on time."
    >
      <div className="min-h-0 flex-1 space-y-5 overflow-y-auto px-5 pb-5">
        {!data ? (
          <p className="py-16 text-center text-[13px] text-muted-foreground">Loading…</p>
        ) : (
          <>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
              <Tile
                label="Catalogue"
                value={data.totalItems.toLocaleString()}
                hint="distinct items"
              />
              <Tile
                label="On the shelf"
                value={data.goodPieces.toLocaleString()}
                hint="usable pieces"
                tone="text-emerald-600 dark:text-emerald-400"
              />
              <Tile
                label="Out on events"
                value={data.piecesOutOnEvents.toLocaleString()}
                hint={`${data.dispatchedIssues} passes`}
                tone="text-amber-600 dark:text-amber-400"
              />
              <Tile
                label="Needs repair"
                value={data.repairablePieces.toLocaleString()}
                hint={`${data.damagedPieces} beyond repair`}
              />
              <Tile
                label="Overdue"
                value={data.overdueIssues}
                hint="passes past their return date"
                tone={data.overdueIssues > 0 ? "text-rose-600 dark:text-rose-400" : ""}
              />
              <Tile
                label="This week"
                value={`${data.dispatchesThisWeek} / ${data.returnsDueThisWeek}`}
                hint="dispatches / returns due"
              />
            </div>

            {data.catalogueValue > 0 ? (
              <div className="rounded-lg border bg-muted/30 px-4 py-2.5 text-[13px]">
                Catalogue value on the shelf:{" "}
                <span className="font-semibold">{formatMoney(data.catalogueValue)}</span>
                <span className="ml-2 text-[11.5px] text-muted-foreground">
                  (replacement value × usable stock)
                </span>
              </div>
            ) : (
              <div className="rounded-lg border border-dashed px-4 py-2.5 text-[12.5px] text-muted-foreground">
                No rental rates or replacement values are set yet, so the catalogue
                cannot be valued and a damage bill has nothing to charge against.
                Add them on the items that matter most.
              </div>
            )}

            <section>
              <h2 className="mb-2 text-[13px] font-semibold">Stock by category</h2>
              <div className="space-y-1">
                {data.categories.map((c) => (
                  <div key={c.categoryId} className="flex items-center gap-3 text-[12.5px]">
                    <span className="w-52 shrink-0 truncate">{c.categoryName}</span>
                    <div className="h-4 flex-1 overflow-hidden rounded bg-muted">
                      <div
                        className="h-full rounded bg-primary/70"
                        style={{ width: `${(c.goodQuantity / widest) * 100}%` }}
                      />
                    </div>
                    <span className="w-16 shrink-0 text-right tabular-nums font-medium">
                      {c.goodQuantity.toLocaleString()}
                    </span>
                    <span className="w-20 shrink-0 text-right text-[11px] tabular-nums text-muted-foreground">
                      {c.itemCount} items
                    </span>
                  </div>
                ))}
              </div>
            </section>

            {data.overdue.length > 0 ? (
              <section>
                <h2 className="mb-2 flex items-center gap-1.5 text-[13px] font-semibold text-rose-600">
                  <AlertTriangle className="size-3.5" />
                  Overdue returns
                </h2>
                <ul className="space-y-1">
                  {data.overdue.map((issue) => (
                    <li
                      key={issue.id}
                      className="flex items-center justify-between rounded border px-3 py-2 text-[12.5px]"
                    >
                      <span>
                        <span className="font-mono text-[11px]">{issue.code}</span>
                        {" · "}
                        {issue.eventName ?? issue.clientName ?? "Unnamed"}
                      </span>
                      <span className="text-rose-600">
                        due {issue.expectedReturnDate.slice(0, 10)} ·{" "}
                        {issue.totalPending} pieces out
                      </span>
                    </li>
                  ))}
                </ul>
              </section>
            ) : null}

            {data.lowStock.length > 0 ? (
              <section>
                <h2 className="mb-2 text-[13px] font-semibold">Running low</h2>
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-5">
                  {data.lowStock.map((item) => (
                    <div key={item.id} className="flex items-center gap-2 rounded border p-2">
                      <PropThumb
                        src={item.primaryThumbnailUrl}
                        alt={item.name}
                        className="size-9 shrink-0"
                      />
                      <div className="min-w-0">
                        <div className="truncate text-[12px] font-medium">{item.name}</div>
                        <div className="text-[11px] text-amber-600">
                          {item.goodQuantity} left · reorder at {item.reorderLevel}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </section>
            ) : null}

            <div className="flex flex-wrap gap-2 border-t pt-4 text-[13px]">
              <Link
                href="/dashboard/inventory/props"
                className="rounded-md border px-3 py-1.5 hover:bg-muted"
              >
                <Boxes className="mr-1 inline size-3.5" />
                Browse the catalogue
              </Link>
              <Link
                href="/dashboard/inventory/props/availability"
                className="rounded-md border px-3 py-1.5 hover:bg-muted"
              >
                <PackageCheck className="mr-1 inline size-3.5" />
                Check availability
              </Link>
              <Link
                href="/dashboard/inventory/props/gate-passes"
                className="rounded-md border px-3 py-1.5 hover:bg-muted"
              >
                <Truck className="mr-1 inline size-3.5" />
                Gate passes
              </Link>
            </div>
          </>
        )}
      </div>
    </PagePanel>
  );
}
