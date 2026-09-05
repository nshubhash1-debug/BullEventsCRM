"use client";

import * as React from "react";
import { ClipboardCheck, Handshake, HardHat, Truck, Warehouse } from "lucide-react";

import { PagePanel } from "@/components/shell/page-panel";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { formatMoney } from "@/lib/crm-api";
import { eventResourcesApi, humaniseLabel, type DaySheet } from "@/lib/resources-api";
import { cn } from "@/lib/utils";

function isoToday(offsetDays = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

function shortDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { day: "2-digit", month: "short" });
}

function Section({
  icon: Icon,
  title,
  count,
  children,
}: {
  icon: React.ElementType;
  title: string;
  count: number;
  children: React.ReactNode;
}) {
  return (
    <section>
      <h2 className="mb-2 flex items-center gap-1.5 text-[13px] font-semibold">
        <Icon className="size-3.5 text-muted-foreground" />
        {title}
        <span className="rounded bg-muted px-1.5 py-0.5 text-[11px] font-normal text-muted-foreground">
          {count}
        </span>
      </h2>
      {count === 0 ? (
        <p className="rounded-lg border border-dashed py-6 text-center text-[12.5px] text-muted-foreground">
          Nothing over these dates.
        </p>
      ) : (
        <div className="overflow-x-auto rounded-lg border">{children}</div>
      )}
    </section>
  );
}

const HEAD =
  "text-left text-[10.5px] uppercase tracking-wide text-muted-foreground bg-muted/40";

export default function DaySheetPage() {
  const [from, setFrom] = React.useState(() => isoToday());
  const [to, setTo] = React.useState(() => isoToday(6));
  const [data, setData] = React.useState<DaySheet | null>(null);
  const [loadedKey, setLoadedKey] = React.useState<string | null>(null);

  const queryKey = JSON.stringify({ from, to });
  const loading = loadedKey !== queryKey;

  React.useEffect(() => {
    let cancelled = false;

    eventResourcesApi
      .daySheet(from, to)
      .then((r) => !cancelled && setData(r))
      .catch(() => !cancelled && setData(null))
      .finally(() => !cancelled && setLoadedKey(queryKey));

    return () => {
      cancelled = true;
    };
  }, [from, to, queryKey]);

  const s = data?.summary;

  return (
    <PagePanel
      icon={ClipboardCheck}
      title="Day sheet"
      hint="Everything the operation is committed to over a window — stock, crew, suppliers and trucks, whoever they belong to."
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
          {loading || !s
            ? "Loading…"
            : `${s.gatePasses} gate passes · ${s.pieces.toLocaleString()} pieces · ${s.crewBooked} crew · ${s.vendorsDue} suppliers · ${s.tripsRunning} trips`}
        </span>
      }
    >
      <div className="min-h-0 flex-1 space-y-6 overflow-y-auto px-5 pb-5">
        {!data || !s ? (
          <p className="py-16 text-center text-[13px] text-muted-foreground">Loading…</p>
        ) : (
          <>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
              {[
                { label: "Gate passes", value: s.gatePasses, hint: `${s.pieces.toLocaleString()} pieces` },
                { label: "Crew booked", value: s.crewBooked, hint: "people on site" },
                { label: "Suppliers due", value: s.vendorsDue, hint: formatMoney(s.vendorCost) },
                { label: "Trips", value: s.tripsRunning, hint: "on the road" },
                {
                  label: "Committed",
                  value: formatMoney(s.vendorCost),
                  hint: "to suppliers",
                },
              ].map((tile) => (
                <div key={tile.label} className="rounded-lg border bg-card p-3">
                  <div className="text-[11px] uppercase tracking-wide text-muted-foreground">
                    {tile.label}
                  </div>
                  <div className="mt-1 text-2xl font-semibold tabular-nums">
                    {tile.value}
                  </div>
                  <div className="text-[11px] text-muted-foreground">{tile.hint}</div>
                </div>
              ))}
            </div>

            <Section icon={Warehouse} title="Going out" count={data.dispatches.length}>
              <table className="w-full min-w-[720px] text-[12.5px]">
                <thead>
                  <tr className={HEAD}>
                    <th className="px-3 py-2 font-medium">Pass</th>
                    <th className="px-2 py-2 font-medium">Event</th>
                    <th className="px-2 py-2 font-medium">Venue</th>
                    <th className="px-2 py-2 font-medium">Out</th>
                    <th className="px-2 py-2 font-medium">Back</th>
                    <th className="px-2 py-2 text-right font-medium">Pieces</th>
                    <th className="px-2 py-2 font-medium">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {data.dispatches.map((d) => (
                    <tr key={d.id} className="border-t">
                      <td className="px-3 py-1.5 font-mono text-[11px]">{d.code}</td>
                      <td className="px-2 py-1.5">
                        <div className="font-medium">{d.eventName ?? "—"}</div>
                        {d.clientName ? (
                          <div className="text-[11px] text-muted-foreground">
                            {d.clientName}
                          </div>
                        ) : null}
                      </td>
                      <td className="px-2 py-1.5 text-muted-foreground">
                        {d.venueName ?? "—"}
                      </td>
                      <td className="px-2 py-1.5 tabular-nums">{shortDate(d.dispatchDate)}</td>
                      <td className="px-2 py-1.5 tabular-nums text-muted-foreground">
                        {shortDate(d.expectedReturnDate)}
                      </td>
                      <td className="px-2 py-1.5 text-right tabular-nums font-medium">
                        {d.pieces}
                      </td>
                      <td className="px-2 py-1.5 text-[11.5px] text-muted-foreground">
                        {humaniseLabel(d.status)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Section>

            <Section icon={HardHat} title="Crew on site" count={data.crew.length}>
              <table className="w-full min-w-[720px] text-[12.5px]">
                <thead>
                  <tr className={HEAD}>
                    <th className="px-3 py-2 font-medium">Name</th>
                    <th className="px-2 py-2 font-medium">Role</th>
                    <th className="px-2 py-2 font-medium">Phone</th>
                    <th className="px-2 py-2 font-medium">Event</th>
                    <th className="px-2 py-2 font-medium">Venue</th>
                    <th className="px-2 py-2 font-medium">Dates</th>
                    <th className="px-2 py-2 font-medium">Reports</th>
                  </tr>
                </thead>
                <tbody>
                  {data.crew.map((c) => (
                    <tr key={c.id} className="border-t">
                      <td className="px-3 py-1.5 font-medium">{c.name}</td>
                      <td className="px-2 py-1.5">{humaniseLabel(c.role)}</td>
                      <td className="px-2 py-1.5 tabular-nums text-muted-foreground">
                        {c.phone ?? "—"}
                      </td>
                      <td className="px-2 py-1.5">{c.eventName ?? "—"}</td>
                      <td className="px-2 py-1.5 text-muted-foreground">
                        {c.venueName ?? "—"}
                      </td>
                      <td className="px-2 py-1.5 tabular-nums text-muted-foreground">
                        {shortDate(c.fromDate)} – {shortDate(c.toDate)}
                      </td>
                      <td className="px-2 py-1.5 tabular-nums text-muted-foreground">
                        {c.reportingTime?.slice(0, 5) ?? "—"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Section>

            <Section icon={Handshake} title="Suppliers due" count={data.orders.length}>
              <table className="w-full min-w-[720px] text-[12.5px]">
                <thead>
                  <tr className={HEAD}>
                    <th className="px-3 py-2 font-medium">Order</th>
                    <th className="px-2 py-2 font-medium">Vendor</th>
                    <th className="px-2 py-2 font-medium">Service</th>
                    <th className="px-2 py-2 font-medium">Event</th>
                    <th className="px-2 py-2 font-medium">Date</th>
                    <th className="px-2 py-2 text-right font-medium">Cost</th>
                    <th className="px-2 py-2 font-medium">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {data.orders.map((o) => (
                    <tr key={o.id} className="border-t">
                      <td className="px-3 py-1.5 font-mono text-[11px]">{o.code}</td>
                      <td className="px-2 py-1.5">
                        <div className="font-medium">{o.vendorName}</div>
                        {o.phone ? (
                          <div className="text-[11px] tabular-nums text-muted-foreground">
                            {o.phone}
                          </div>
                        ) : null}
                      </td>
                      <td className="px-2 py-1.5 text-muted-foreground">
                        {humaniseLabel(o.service)}
                      </td>
                      <td className="px-2 py-1.5">{o.eventName ?? "—"}</td>
                      <td className="px-2 py-1.5 tabular-nums">{shortDate(o.serviceDate)}</td>
                      <td className="px-2 py-1.5 text-right tabular-nums">
                        {formatMoney(o.totalCost)}
                      </td>
                      <td className="px-2 py-1.5 text-[11.5px] text-muted-foreground">
                        {humaniseLabel(o.status)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Section>

            <Section icon={Truck} title="On the road" count={data.trips.length}>
              <table className="w-full min-w-[720px] text-[12.5px]">
                <thead>
                  <tr className={HEAD}>
                    <th className="px-3 py-2 font-medium">Trip</th>
                    <th className="px-2 py-2 font-medium">Vehicle</th>
                    <th className="px-2 py-2 font-medium">Driver</th>
                    <th className="px-2 py-2 font-medium">Event</th>
                    <th className="px-2 py-2 font-medium">To</th>
                    <th className="px-2 py-2 font-medium">Leaves</th>
                    <th className="px-2 py-2 text-right font-medium">Loads</th>
                    <th className="px-2 py-2 font-medium">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {data.trips.map((t) => (
                    <tr key={t.id} className="border-t">
                      <td className="px-3 py-1.5 font-mono text-[11px]">{t.code}</td>
                      <td className="px-2 py-1.5 font-mono text-[11.5px]">{t.vehicle}</td>
                      <td className="px-2 py-1.5 text-muted-foreground">
                        {t.driverName ?? "—"}
                      </td>
                      <td className="px-2 py-1.5">{t.eventName ?? "—"}</td>
                      <td className="px-2 py-1.5 text-muted-foreground">
                        {t.toLocation ?? "—"}
                      </td>
                      <td className="px-2 py-1.5 tabular-nums">
                        {shortDate(t.fromDate)}
                        {t.departureTime ? ` ${t.departureTime.slice(0, 5)}` : ""}
                      </td>
                      <td className="px-2 py-1.5 text-right tabular-nums">{t.loads}</td>
                      <td
                        className={cn(
                          "px-2 py-1.5 text-[11.5px]",
                          t.status === "InTransit"
                            ? "font-medium text-amber-600"
                            : "text-muted-foreground"
                        )}
                      >
                        {humaniseLabel(t.status)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Section>
          </>
        )}
      </div>
    </PagePanel>
  );
}
