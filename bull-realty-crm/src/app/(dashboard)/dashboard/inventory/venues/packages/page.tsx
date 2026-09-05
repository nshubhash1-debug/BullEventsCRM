"use client";

import * as React from "react";
import { Loader2, Package, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Checkbox } from "@/components/ui/checkbox";
import { ApiError } from "@/lib/api";
import {
  formatMoney,
  inventoryApi,
  type ProjectRow,
  type UnitRow,
} from "@/lib/crm-api";
import {
  packagesApi,
  type VenuePackage,
  type VenuePeakWindow,
} from "@/lib/inventory-api";

export default function VenuePackagesPage() {
  const [projects, setProjects] = React.useState<ProjectRow[]>([]);
  const [projectId, setProjectId] = React.useState("");
  const [packages, setPackages] = React.useState<VenuePackage[] | null>(null);
  const [peaks, setPeaks] = React.useState<VenuePeakWindow[]>([]);
  const [spaces, setSpaces] = React.useState<UnitRow[]>([]);

  const [createOpen, setCreateOpen] = React.useState(false);
  const [name, setName] = React.useState("");
  const [code, setCode] = React.useState("");
  const [rental, setRental] = React.useState("");
  const [perPlate, setPerPlate] = React.useState("");
  const [selected, setSelected] = React.useState<Set<number>>(new Set());
  const [saving, setSaving] = React.useState(false);

  const [peakStart, setPeakStart] = React.useState("");
  const [peakEnd, setPeakEnd] = React.useState("");
  const [peakLabel, setPeakLabel] = React.useState("Peak season");
  const [peakPremium, setPeakPremium] = React.useState("25");

  React.useEffect(() => {
    inventoryApi.projects().then((rows) => {
      setProjects(rows);
      const preferred = rows.find((p) => p.totalUnits > 0) ?? rows[0];
      if (preferred) setProjectId(String(preferred.id));
    });
  }, []);

  const reload = React.useCallback(() => {
    if (!projectId) return;
    const id = Number(projectId);
    packagesApi.list(id).then(setPackages).catch(() => setPackages([]));
    packagesApi.peakDates(id).then(setPeaks).catch(() => setPeaks([]));
    inventoryApi
      .query({
        filter: {
          conjunction: "and",
          children: [
            {
              field: "projectId",
              operator: "equals",
              value: String(id),
            },
          ],
        },
        page: 1,
        pageSize: 200,
        sort: [],
      })
      .then((r) => setSpaces(r.items))
      .catch(() => setSpaces([]));
  }, [projectId]);

  React.useEffect(() => {
    reload();
  }, [reload]);

  async function createPackage() {
    if (!projectId || !name.trim() || !code.trim() || selected.size === 0) {
      toast.error("Name, code and at least one space are required.");
      return;
    }
    setSaving(true);
    try {
      await packagesApi.create({
        projectId: Number(projectId),
        name: name.trim(),
        code: code.trim(),
        indicativeRental: rental ? Number(rental) : null,
        indicativePerPlate: perPlate ? Number(perPlate) : null,
        isActive: true,
        unitIds: [...selected],
      });
      toast.success("Package created");
      setCreateOpen(false);
      setName("");
      setCode("");
      setSelected(new Set());
      reload();
    } catch (error) {
      toast.error("Could not create package", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  async function addPeak() {
    if (!projectId || !peakStart || !peakEnd) {
      toast.error("Peak start and end dates are required.");
      return;
    }
    try {
      await packagesApi.createPeak({
        projectId: Number(projectId),
        startDate: peakStart,
        endDate: peakEnd,
        label: peakLabel.trim() || "Peak",
        premiumFraction: (Number(peakPremium) || 0) / 100,
      });
      toast.success("Peak window added");
      reload();
    } catch (error) {
      toast.error("Could not add peak window", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-4 p-4">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-lg font-semibold">
            <Package className="size-4.5 text-primary" />
            Venue packages
          </h1>
          <p className="text-[12.5px] text-muted-foreground">
            Multi-space bundles for proposals, plus peak date premiums for the diary.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Select value={projectId} onValueChange={setProjectId}>
            <SelectTrigger className="h-8 w-52 text-[12.5px]">
              <SelectValue placeholder="Venue" />
            </SelectTrigger>
            <SelectContent>
              {projects.map((p) => (
                <SelectItem key={p.id} value={String(p.id)}>
                  {p.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button size="sm" className="h-8" onClick={() => setCreateOpen(true)}>
            <Plus className="size-3.5" /> New package
          </Button>
        </div>
      </header>

      <section className="rounded-lg border bg-card p-4">
        <h2 className="mb-3 text-sm font-semibold">Packages</h2>
        {packages === null ? (
          <Skeleton className="h-24 w-full" />
        ) : packages.length === 0 ? (
          <p className="text-[13px] text-muted-foreground">
            No packages yet — create a lawn + banquet bundle to quote faster.
          </p>
        ) : (
          <ul className="flex flex-col gap-2">
            {packages.map((pkg) => (
              <li
                key={pkg.id}
                className="flex flex-wrap items-start justify-between gap-3 rounded border px-3 py-2.5"
              >
                <div>
                  <p className="text-[13px] font-medium">
                    {pkg.name}{" "}
                    <span className="text-muted-foreground">· {pkg.code}</span>
                  </p>
                  <p className="text-[12px] text-muted-foreground">
                    {pkg.spaces.map((s) => s.unitName).join(" + ") || "No spaces"}
                    {pkg.indicativeRental
                      ? ` · rental ${formatMoney(pkg.indicativeRental)}`
                      : ""}
                    {pkg.indicativePerPlate
                      ? ` · ${formatMoney(pkg.indicativePerPlate)}/plate`
                      : ""}
                  </p>
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={async () => {
                    await packagesApi.remove(pkg.id);
                    toast.success("Package removed");
                    reload();
                  }}
                >
                  <Trash2 className="size-3.5" />
                </Button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="rounded-lg border bg-card p-4">
        <h2 className="mb-3 text-sm font-semibold">Peak windows</h2>
        <div className="mb-3 flex flex-wrap items-end gap-2">
          <div className="space-y-1">
            <Label className="text-[11px]">From</Label>
            <Input
              type="date"
              className="h-8"
              value={peakStart}
              onChange={(e) => setPeakStart(e.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label className="text-[11px]">To</Label>
            <Input
              type="date"
              className="h-8"
              value={peakEnd}
              onChange={(e) => setPeakEnd(e.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label className="text-[11px]">Label</Label>
            <Input
              className="h-8 w-36"
              value={peakLabel}
              onChange={(e) => setPeakLabel(e.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label className="text-[11px]">Premium %</Label>
            <Input
              className="h-8 w-20"
              type="number"
              value={peakPremium}
              onChange={(e) => setPeakPremium(e.target.value)}
            />
          </div>
          <Button size="sm" className="h-8" onClick={() => void addPeak()}>
            Add peak
          </Button>
        </div>
        {peaks.length === 0 ? (
          <p className="text-[13px] text-muted-foreground">No peak windows configured.</p>
        ) : (
          <ul className="flex flex-col gap-1.5">
            {peaks.map((p) => (
              <li
                key={p.id}
                className="flex items-center justify-between rounded border px-3 py-2 text-[12.5px]"
              >
                <span>
                  {p.label}: {p.startDate} → {p.endDate} (+
                  {Math.round(p.premiumFraction * 100)}%)
                </span>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={async () => {
                    await packagesApi.removePeak(p.id);
                    reload();
                  }}
                >
                  <Trash2 className="size-3.5" />
                </Button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>New venue package</DialogTitle>
          </DialogHeader>
          <div className="flex flex-col gap-3">
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label>Name</Label>
                <Input value={name} onChange={(e) => setName(e.target.value)} />
              </div>
              <div className="space-y-1">
                <Label>Code</Label>
                <Input value={code} onChange={(e) => setCode(e.target.value)} />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <div className="space-y-1">
                <Label>Indicative rental</Label>
                <Input
                  type="number"
                  value={rental}
                  onChange={(e) => setRental(e.target.value)}
                />
              </div>
              <div className="space-y-1">
                <Label>Per plate</Label>
                <Input
                  type="number"
                  value={perPlate}
                  onChange={(e) => setPerPlate(e.target.value)}
                />
              </div>
            </div>
            <div className="space-y-1">
              <Label>Spaces</Label>
              <div className="max-h-48 space-y-1 overflow-auto rounded border p-2">
                {spaces.map((s) => {
                  const on = selected.has(s.id);
                  return (
                    <label
                      key={s.id}
                      className="flex items-center gap-2 text-[12.5px]"
                    >
                      <Checkbox
                        checked={on}
                        onCheckedChange={(v) => {
                          const next = new Set(selected);
                          if (v === true) next.add(s.id);
                          else next.delete(s.id);
                          setSelected(next);
                        }}
                      />
                      {s.unitNumber} · {s.configuration} · {s.seatingCapacity} seats
                    </label>
                  );
                })}
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button onClick={() => void createPackage()} disabled={saving}>
              {saving ? <Loader2 className="size-4 animate-spin" /> : null}
              Create
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
