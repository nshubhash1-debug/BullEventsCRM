"use client";

import * as React from "react";
import { AlertTriangle, Plus, Truck } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ApiError } from "@/lib/api";
import { formatMoney } from "@/lib/crm-api";
import {
  fleetApi,
  humaniseLabel,
  type Vehicle,
  type VehicleTrip,
} from "@/lib/resources-api";
import { cn } from "@/lib/utils";

const TRIP_TONE: Record<string, string> = {
  Planned: "bg-muted text-muted-foreground",
  Loading: "bg-sky-100 text-sky-800 dark:bg-sky-950/50 dark:text-sky-300",
  InTransit: "bg-amber-100 text-amber-800 dark:bg-amber-950/50 dark:text-amber-300",
  Delivered: "bg-violet-100 text-violet-800 dark:bg-violet-950/50 dark:text-violet-300",
  Returning: "bg-amber-100 text-amber-800 dark:bg-amber-950/50 dark:text-amber-300",
  Completed: "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/50 dark:text-emerald-300",
  Cancelled: "bg-muted text-muted-foreground line-through",
};

function isoToday(offsetDays = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

export default function FleetPage() {
  const [from, setFrom] = React.useState(() => isoToday(7));
  const [to, setTo] = React.useState(() => isoToday(9));
  const [minPayload, setMinPayload] = React.useState("");

  const [vehicles, setVehicles] = React.useState<Vehicle[]>([]);
  const [trips, setTrips] = React.useState<VehicleTrip[]>([]);
  const [nonce, setNonce] = React.useState(0);
  const [loadedKey, setLoadedKey] = React.useState<string | null>(null);

  const [creating, setCreating] = React.useState(false);
  const [saving, setSaving] = React.useState(false);
  const [trip, setTrip] = React.useState({
    vehicleId: "",
    eventName: "",
    toLocation: "",
    direction: "Outbound",
  });

  const [closing, setClosing] = React.useState<VehicleTrip | null>(null);
  const [closeForm, setCloseForm] = React.useState({ odo: "", fuel: "", toll: "" });

  const queryKey = JSON.stringify({ from, to, minPayload, nonce });
  const loading = loadedKey !== queryKey;

  React.useEffect(() => {
    let cancelled = false;

    Promise.all([
      fleetApi.availability({
        from,
        to,
        minimumPayloadKg: minPayload ? Number(minPayload) : undefined,
      }),
      fleetApi.trips({ from, to, take: 200 }),
    ])
      .then(([fleet, runs]) => {
        if (cancelled) return;
        setVehicles(fleet);
        setTrips(runs);
      })
      .catch(() => {
        if (cancelled) return;
        setVehicles([]);
        setTrips([]);
      })
      .finally(() => {
        if (!cancelled) setLoadedKey(queryKey);
      });

    return () => {
      cancelled = true;
    };
  }, [from, to, minPayload, nonce, queryKey]);

  async function createTrip() {
    if (!trip.vehicleId) {
      toast.error("Pick a vehicle.");
      return;
    }

    setSaving(true);
    try {
      const created = await fleetApi.createTrip({
        vehicleId: Number(trip.vehicleId),
        fromDate: from,
        toDate: to,
        direction: trip.direction,
        eventName: trip.eventName.trim() || null,
        toLocation: trip.toLocation.trim() || null,
      });
      toast.success(`${created.code} planned on ${created.vehicleRegistration}.`);
      setCreating(false);
      setTrip((t) => ({ ...t, eventName: "", toLocation: "" }));
      setNonce((n) => n + 1);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not plan that.");
    } finally {
      setSaving(false);
    }
  }

  async function close() {
    if (!closing) return;

    setSaving(true);
    try {
      await fleetApi.closeTrip(closing.id, {
        endOdometerKm: closeForm.odo ? Number(closeForm.odo) : null,
        fuelCost: closeForm.fuel ? Number(closeForm.fuel) : null,
        tollCost: closeForm.toll ? Number(closeForm.toll) : null,
      });
      toast.success("Trip closed. The odometer is rolled forward on the vehicle.");
      setClosing(null);
      setCloseForm({ odo: "", fuel: "", toll: "" });
      setNonce((n) => n + 1);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not close that.");
    } finally {
      setSaving(false);
    }
  }

  const free = vehicles.filter((v) => v.isAvailable).length;
  const lapsed = vehicles.filter((v) => v.hasLapsedPapers).length;

  return (
    <PagePanel
      icon={Truck}
      title="Fleet"
      hint="The vehicles that move everything, what they can carry, and the runs they are on."
      actions={
        <Button size="sm" className="h-8" onClick={() => setCreating(true)}>
          <Plus className="mr-1 size-3.5" />
          Plan a trip
        </Button>
      }
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
          <div>
            <Label className="text-[11px] text-muted-foreground">Needs to carry (kg)</Label>
            <Input
              className="mt-1 h-9 w-36"
              placeholder="Any"
              value={minPayload}
              onChange={(e) => setMinPayload(e.target.value)}
            />
          </div>
        </div>
      }
      subToolbar={
        <span className="text-[12px] text-muted-foreground">
          {loading
            ? "Loading…"
            : `${free} of ${vehicles.length} free between ${from} and ${to} · ${trips.length} trips in the window`}
          {lapsed > 0 ? ` · ${lapsed} with lapsed papers` : ""}
        </span>
      }
    >
      <div className="min-h-0 flex-1 overflow-y-auto px-5 pb-5">
        <Tabs defaultValue="vehicles">
          <TabsList>
            <TabsTrigger value="vehicles">Vehicles</TabsTrigger>
            <TabsTrigger value="trips">Trips</TabsTrigger>
          </TabsList>

          {/* ---------------- vehicles ---------------- */}
          <TabsContent value="vehicles" className="mt-3">
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {vehicles.map((v) => (
                <div
                  key={v.id}
                  className={cn(
                    "rounded-lg border bg-card p-3",
                    v.hasLapsedPapers ? "border-rose-300 dark:border-rose-900" : ""
                  )}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <div className="font-medium">{v.name}</div>
                      <div className="font-mono text-[11px] text-muted-foreground">
                        {v.registrationNumber}
                      </div>
                    </div>
                    <span
                      className={cn(
                        "shrink-0 rounded px-1.5 py-0.5 text-[11px] font-medium",
                        v.isAvailable
                          ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400"
                          : "bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400"
                      )}
                    >
                      {v.isAvailable ? "Free" : "Committed"}
                    </span>
                  </div>

                  <div className="mt-2 grid grid-cols-3 gap-2 text-center text-[12px]">
                    <div>
                      <div className="font-semibold tabular-nums">
                        {v.payloadKg ? `${v.payloadKg}` : "—"}
                      </div>
                      <div className="text-[10.5px] text-muted-foreground">kg payload</div>
                    </div>
                    <div>
                      <div className="font-semibold tabular-nums">
                        {v.capacityCubicFeet ? `${v.capacityCubicFeet}` : "—"}
                      </div>
                      <div className="text-[10.5px] text-muted-foreground">cu ft</div>
                    </div>
                    <div>
                      <div className="font-semibold tabular-nums">
                        {v.dayRate ? formatMoney(v.dayRate) : "—"}
                      </div>
                      <div className="text-[10.5px] text-muted-foreground">per day</div>
                    </div>
                  </div>

                  <div className="mt-2 flex items-center justify-between text-[11.5px] text-muted-foreground">
                    <span>{humaniseLabel(v.vehicleType)}</span>
                    <span>{v.defaultDriverName ?? "no driver set"}</span>
                  </div>

                  {v.hasLapsedPapers ? (
                    <div className="mt-2 flex items-center gap-1 rounded bg-rose-50 px-2 py-1 text-[11.5px] text-rose-700 dark:bg-rose-950/40 dark:text-rose-400">
                      <AlertTriangle className="size-3" />
                      Papers lapsed — cannot be dispatched
                    </div>
                  ) : null}
                </div>
              ))}
            </div>

            {!loading && vehicles.length === 0 ? (
              <p className="py-16 text-center text-[13px] text-muted-foreground">
                No vehicle matches that. Try a smaller payload.
              </p>
            ) : null}
          </TabsContent>

          {/* ---------------- trips ---------------- */}
          <TabsContent value="trips" className="mt-3">
            <div className="overflow-x-auto">
              <table className="w-full min-w-[900px] text-[13px]">
                <thead>
                  <tr className="border-b text-left text-[11px] uppercase tracking-wide text-muted-foreground">
                    <th className="py-2 pr-2 font-medium">Trip</th>
                    <th className="px-2 py-2 font-medium">Vehicle</th>
                    <th className="px-2 py-2 font-medium">Driver</th>
                    <th className="px-2 py-2 font-medium">Event</th>
                    <th className="px-2 py-2 font-medium">To</th>
                    <th className="px-2 py-2 font-medium">Dates</th>
                    <th className="px-2 py-2 text-right font-medium">Load</th>
                    <th className="px-2 py-2 text-right font-medium">Cost</th>
                    <th className="px-2 py-2 font-medium">Status</th>
                    <th className="px-2 py-2" />
                  </tr>
                </thead>

                <tbody>
                  {trips.map((t) => (
                    <tr key={t.id} className="border-b last:border-0 hover:bg-muted/40">
                      <td className="py-2 pr-2 font-mono text-[11.5px]">{t.code}</td>
                      <td className="px-2 py-2">
                        <div className="font-medium">{t.vehicleName}</div>
                        <div className="font-mono text-[10.5px] text-muted-foreground">
                          {t.vehicleRegistration}
                        </div>
                      </td>
                      <td className="px-2 py-2 text-muted-foreground">
                        {t.driverName ?? "—"}
                      </td>
                      <td className="px-2 py-2">{t.eventName ?? "—"}</td>
                      <td className="px-2 py-2 text-muted-foreground">
                        {t.toLocation ?? "—"}
                      </td>
                      <td className="px-2 py-2 tabular-nums text-muted-foreground">
                        {t.fromDate.slice(5, 10)} – {t.toDate.slice(5, 10)}
                      </td>
                      <td className="px-2 py-2 text-right tabular-nums">
                        {t.plannedWeightKg ? (
                          <span className={cn(t.isOverloaded ? "font-semibold text-rose-600" : "")}>
                            {Math.round(t.plannedWeightKg)} kg
                            {t.isOverloaded ? (
                              <AlertTriangle className="ml-1 inline size-3" />
                            ) : null}
                          </span>
                        ) : (
                          <span className="text-muted-foreground">
                            {t.loads.length || "—"}
                          </span>
                        )}
                      </td>
                      <td className="px-2 py-2 text-right tabular-nums text-muted-foreground">
                        {t.totalRunningCost ? formatMoney(t.totalRunningCost) : "—"}
                      </td>
                      <td className="px-2 py-2">
                        <span
                          className={cn(
                            "rounded px-1.5 py-0.5 text-[11px] font-medium",
                            TRIP_TONE[t.status] ?? "bg-muted"
                          )}
                        >
                          {humaniseLabel(t.status)}
                        </span>
                      </td>
                      <td className="px-2 py-2 text-right">
                        {t.status !== "Completed" && t.status !== "Cancelled" ? (
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-7 text-[11.5px]"
                            onClick={() => {
                              setClosing(t);
                              setCloseForm({
                                odo: String((t.startOdometerKm ?? 0) + 100),
                                fuel: "",
                                toll: "",
                              });
                            }}
                          >
                            Close
                          </Button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {!loading && trips.length === 0 ? (
              <p className="py-16 text-center text-[13px] text-muted-foreground">
                No trips over these dates.
              </p>
            ) : null}
          </TabsContent>
        </Tabs>
      </div>

      {/* ---------------- plan a trip ---------------- */}
      <Dialog open={creating} onOpenChange={setCreating}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Plan a trip</DialogTitle>
            <DialogDescription>
              {from} to {to}. A vehicle with lapsed papers, or one already committed
              over these dates, will be refused.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-3">
            <div>
              <Label className="text-[12px]">Vehicle</Label>
              <select
                className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm"
                value={trip.vehicleId}
                onChange={(e) => setTrip((t) => ({ ...t, vehicleId: e.target.value }))}
              >
                <option value="">Select…</option>
                {vehicles
                  .filter((v) => v.isAvailable && !v.hasLapsedPapers)
                  .map((v) => (
                    <option key={v.id} value={v.id}>
                      {v.name} · {v.registrationNumber}
                      {v.payloadKg ? ` · ${v.payloadKg} kg` : ""}
                    </option>
                  ))}
              </select>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label className="text-[12px]">Event</Label>
                <Input
                  className="mt-1 h-9"
                  value={trip.eventName}
                  onChange={(e) => setTrip((t) => ({ ...t, eventName: e.target.value }))}
                />
              </div>
              <div>
                <Label className="text-[12px]">Going to</Label>
                <Input
                  className="mt-1 h-9"
                  placeholder="The Grand Palladium"
                  value={trip.toLocation}
                  onChange={(e) => setTrip((t) => ({ ...t, toLocation: e.target.value }))}
                />
              </div>
            </div>

            <div>
              <Label className="text-[12px]">Direction</Label>
              <select
                className="mt-1 h-9 w-full rounded-md border bg-background px-2 text-sm"
                value={trip.direction}
                onChange={(e) => setTrip((t) => ({ ...t, direction: e.target.value }))}
              >
                <option value="Outbound">Outbound — to the venue</option>
                <option value="Return">Return — bringing it home</option>
              </select>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCreating(false)}>
              Cancel
            </Button>
            <Button disabled={saving} onClick={() => void createTrip()}>
              {saving ? "Planning…" : "Plan trip"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ---------------- close a trip ---------------- */}
      <Dialog open={closing !== null} onOpenChange={(open) => !open && setClosing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Close {closing?.code}</DialogTitle>
            <DialogDescription>
              The closing reading rolls forward onto {closing?.vehicleRegistration}, so
              the next trip starts from it.
              {closing?.startOdometerKm
                ? ` Opened at ${closing.startOdometerKm.toLocaleString()} km.`
                : ""}
            </DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-3 gap-3">
            <div>
              <Label className="text-[12px]">Closing km</Label>
              <Input
                className="mt-1 h-9"
                value={closeForm.odo}
                onChange={(e) => setCloseForm((f) => ({ ...f, odo: e.target.value }))}
              />
            </div>
            <div>
              <Label className="text-[12px]">Fuel</Label>
              <Input
                className="mt-1 h-9"
                value={closeForm.fuel}
                onChange={(e) => setCloseForm((f) => ({ ...f, fuel: e.target.value }))}
              />
            </div>
            <div>
              <Label className="text-[12px]">Tolls</Label>
              <Input
                className="mt-1 h-9"
                value={closeForm.toll}
                onChange={(e) => setCloseForm((f) => ({ ...f, toll: e.target.value }))}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setClosing(null)}>
              Cancel
            </Button>
            <Button disabled={saving} onClick={() => void close()}>
              {saving ? "Closing…" : "Close trip"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </PagePanel>
  );
}
