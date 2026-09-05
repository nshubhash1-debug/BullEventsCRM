"use client";

import * as React from "react";
import { FileText, IndianRupee, Plus, Star, Trash2 } from "lucide-react";
import { toast } from "sonner";

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
import { formatMoney } from "@/lib/crm-api";
import {
  SERVICE_CATEGORIES,
  VENDOR_RATE_BASES,
  humaniseLabel,
  vendorsApi,
  type Vendor,
} from "@/lib/resources-api";
import { cn } from "@/lib/utils";

function Fact({ label, value }: { label: string; value: React.ReactNode }) {
  if (value === null || value === undefined || value === "" || value === "—") return null;

  return (
    <div className="flex items-baseline justify-between gap-3 border-b py-1.5 text-[13px] last:border-0">
      <span className="shrink-0 text-muted-foreground">{label}</span>
      <span className="text-right font-medium">{value}</span>
    </div>
  );
}

export function VendorSheet({
  vendorId,
  open,
  onOpenChange,
  onChanged,
}: {
  vendorId: number | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onChanged?: () => void;
}) {
  const [vendor, setVendor] = React.useState<Vendor | null>(null);
  const [busy, setBusy] = React.useState(false);

  const [rate, setRate] = React.useState({
    service: "Catering",
    name: "",
    basis: "PerEvent",
    rate: "",
    sellRate: "",
  });

  const [doc, setDoc] = React.useState({ type: "", fileName: "", expiry: "" });

  const load = React.useCallback(() => {
    if (vendorId === null) return;
    vendorsApi.get(vendorId).then(setVendor).catch(() => setVendor(null));
  }, [vendorId]);

  // Mounted under a key of the vendor id, so a different vendor is a fresh sheet.
  React.useEffect(() => load(), [load]);

  async function act(work: () => Promise<Vendor>, success: string) {
    setBusy(true);
    try {
      setVendor(await work());
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

  if (!vendor) {
    return (
      <Sheet open={open} onOpenChange={onOpenChange}>
        <SheetContent className="w-full sm:max-w-xl">
          <SheetHeader>
            <SheetTitle>Vendor</SheetTitle>
            <SheetDescription>Loading…</SheetDescription>
          </SheetHeader>
        </SheetContent>
      </Sheet>
    );
  }

  const seedService = vendor.services[0] ?? "Catering";

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="flex w-full flex-col gap-0 overflow-y-auto p-0 sm:max-w-xl">
        <SheetHeader className="border-b px-5 py-4">
          <SheetTitle className="flex items-center gap-2 text-[15px]">
            {vendor.name}
            {vendor.rating ? (
              <span className="inline-flex items-center gap-0.5 text-[12px] font-normal text-muted-foreground">
                <Star className="size-3 fill-amber-400 text-amber-400" />
                {vendor.rating} · {vendor.completedEvents} events
              </span>
            ) : null}
          </SheetTitle>
          <SheetDescription className="text-xs">
            <span className="font-mono">{vendor.code}</span>
            {" · "}
            {vendor.services.map((s) => humaniseLabel(s)).join(", ")}
            {vendor.city ? ` · ${vendor.city}` : ""}
          </SheetDescription>
        </SheetHeader>

        <Tabs defaultValue="rates" className="flex-1">
          <TabsList className="mx-5 mt-3">
            <TabsTrigger value="rates">Rate card</TabsTrigger>
            <TabsTrigger value="details">Details</TabsTrigger>
            <TabsTrigger value="documents">
              Documents
              {vendor.expiringDocuments > 0 ? (
                <span className="ml-1 rounded bg-amber-100 px-1 text-[10px] text-amber-800 dark:bg-amber-950/50 dark:text-amber-300">
                  {vendor.expiringDocuments}
                </span>
              ) : null}
            </TabsTrigger>
          </TabsList>

          {/* ---------------- rate card ---------------- */}
          <TabsContent value="rates" className="px-5 pb-6">
            {vendor.rates.length === 0 ? (
              <p className="py-6 text-center text-[13px] text-muted-foreground">
                No rates yet. Without them a quote has nothing to price against.
              </p>
            ) : (
              <table className="w-full text-[12.5px]">
                <thead>
                  <tr className="border-b text-left text-[10.5px] uppercase tracking-wide text-muted-foreground">
                    <th className="py-1.5 pr-2 font-medium">Item</th>
                    <th className="py-1.5 px-1 text-right font-medium">Cost</th>
                    <th className="py-1.5 px-1 text-right font-medium">Sell</th>
                    <th className="py-1.5 px-1 text-right font-medium">Margin</th>
                    <th className="w-8" />
                  </tr>
                </thead>
                <tbody>
                  {vendor.rates.map((r) => (
                    <tr key={r.id} className="border-b last:border-0">
                      <td className="py-1.5 pr-2">
                        <div className="font-medium">{r.name}</div>
                        <div className="text-[10.5px] text-muted-foreground">
                          {humaniseLabel(r.service)} · {humaniseLabel(r.basis)}
                        </div>
                      </td>
                      <td className="px-1 py-1.5 text-right tabular-nums">
                        {formatMoney(r.rate)}
                      </td>
                      <td className="px-1 py-1.5 text-right tabular-nums">
                        {r.sellRate ? formatMoney(r.sellRate) : "—"}
                      </td>
                      <td
                        className={cn(
                          "px-1 py-1.5 text-right tabular-nums",
                          (r.marginPerUnit ?? 0) < 0
                            ? "text-rose-600"
                            : "text-emerald-600 dark:text-emerald-400"
                        )}
                      >
                        {r.marginPerUnit !== null ? formatMoney(r.marginPerUnit) : "—"}
                      </td>
                      <td className="py-1.5">
                        <button
                          type="button"
                          className="text-muted-foreground hover:text-destructive"
                          onClick={() =>
                            void act(() => vendorsApi.deleteRate(r.id), "Rate removed.")
                          }
                        >
                          <Trash2 className="size-3.5" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}

            <div className="mt-4 rounded-lg border p-3">
              <div className="mb-2 flex items-center gap-1.5 text-[13px] font-medium">
                <IndianRupee className="size-3.5 text-muted-foreground" />
                Add a rate
              </div>

              <div className="grid gap-2">
                <Input
                  className="h-9"
                  placeholder="Veg thali — silver menu"
                  value={rate.name}
                  onChange={(e) => setRate((r) => ({ ...r, name: e.target.value }))}
                />

                <div className="grid grid-cols-2 gap-2">
                  <select
                    className="h-9 rounded-md border bg-background px-2 text-sm"
                    value={rate.service || seedService}
                    onChange={(e) => setRate((r) => ({ ...r, service: e.target.value }))}
                  >
                    {SERVICE_CATEGORIES.map((s) => (
                      <option key={s} value={s}>
                        {humaniseLabel(s)}
                      </option>
                    ))}
                  </select>
                  <select
                    className="h-9 rounded-md border bg-background px-2 text-sm"
                    value={rate.basis}
                    onChange={(e) => setRate((r) => ({ ...r, basis: e.target.value }))}
                  >
                    {VENDOR_RATE_BASES.map((b) => (
                      <option key={b} value={b}>
                        {humaniseLabel(b)}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="grid grid-cols-2 gap-2">
                  <div>
                    <Label className="text-[11px] text-muted-foreground">They charge</Label>
                    <Input
                      className="mt-1 h-9"
                      value={rate.rate}
                      onChange={(e) => setRate((r) => ({ ...r, rate: e.target.value }))}
                    />
                  </div>
                  <div>
                    <Label className="text-[11px] text-muted-foreground">We charge</Label>
                    <Input
                      className="mt-1 h-9"
                      value={rate.sellRate}
                      onChange={(e) => setRate((r) => ({ ...r, sellRate: e.target.value }))}
                    />
                  </div>
                </div>

                <Button
                  className="h-9"
                  disabled={busy}
                  onClick={() => {
                    if (!rate.name.trim() || !rate.rate) {
                      toast.error("A rate needs a name and a cost.");
                      return;
                    }
                    void act(
                      () =>
                        vendorsApi.addRate(vendor.id, {
                          service: rate.service || seedService,
                          name: rate.name.trim(),
                          basis: rate.basis,
                          rate: Number(rate.rate) || 0,
                          sellRate: rate.sellRate ? Number(rate.sellRate) : null,
                        }),
                      "Rate added."
                    ).then((ok) => {
                      if (ok) setRate((r) => ({ ...r, name: "", rate: "", sellRate: "" }));
                    });
                  }}
                >
                  <Plus className="mr-1 size-3.5" />
                  Add rate
                </Button>
              </div>
            </div>
          </TabsContent>

          {/* ---------------- details ---------------- */}
          <TabsContent value="details" className="px-5 pb-6">
            <Fact label="Contact" value={vendor.contactPerson} />
            <Fact label="Phone" value={vendor.phone} />
            <Fact label="Alt phone" value={vendor.altPhone} />
            <Fact label="Email" value={vendor.email} />
            <Fact label="City" value={vendor.city} />
            <Fact label="Covers" value={vendor.coverageAreas} />
            <Fact label="Address" value={vendor.address} />
            <Fact label="GST" value={vendor.gstNumber} />
            <Fact label="PAN" value={vendor.panNumber} />
            <Fact label="Bank" value={vendor.bankAccountName} />
            <Fact label="Account" value={vendor.bankAccountNumber} />
            <Fact label="IFSC" value={vendor.bankIfsc} />
            <Fact label="Payment terms" value={`${vendor.paymentTermDays} days`} />
            <Fact
              label="Advance"
              value={
                vendor.advanceFraction
                  ? `${Math.round(vendor.advanceFraction * 100)}%`
                  : null
              }
            />
            <Fact label="Jobs per date" value={vendor.concurrentEventCapacity} />
            <Fact label="Events completed" value={vendor.completedEvents} />
            <Fact label="Owner" value={vendor.ownerName} />

            {vendor.notes ? (
              <p className="mt-3 rounded border bg-muted/30 p-2.5 text-[12.5px] text-muted-foreground">
                {vendor.notes}
              </p>
            ) : null}
          </TabsContent>

          {/* ---------------- documents ---------------- */}
          <TabsContent value="documents" className="px-5 pb-6">
            {vendor.documents.length === 0 ? (
              <p className="py-6 text-center text-[13px] text-muted-foreground">
                No documents on file. A lapsed FSSAI licence is the kind of thing
                nobody notices until an inspector does.
              </p>
            ) : (
              <ul className="space-y-1">
                {vendor.documents.map((d) => (
                  <li
                    key={d.id}
                    className="flex items-center justify-between gap-2 rounded border px-3 py-2 text-[12.5px]"
                  >
                    <div className="min-w-0">
                      <div className="font-medium">{d.documentType}</div>
                      <div className="truncate text-[11px] text-muted-foreground">
                        {d.fileName}
                      </div>
                    </div>
                    <div className="shrink-0 text-right">
                      {d.expiryDate ? (
                        <span
                          className={cn(
                            "text-[11.5px] tabular-nums",
                            d.isExpired ? "font-semibold text-rose-600" : "text-muted-foreground"
                          )}
                        >
                          {d.isExpired ? "expired " : "expires "}
                          {d.expiryDate.slice(0, 10)}
                        </span>
                      ) : (
                        <span className="text-[11.5px] text-muted-foreground">no expiry</span>
                      )}
                    </div>
                  </li>
                ))}
              </ul>
            )}

            <div className="mt-4 rounded-lg border p-3">
              <div className="mb-2 flex items-center gap-1.5 text-[13px] font-medium">
                <FileText className="size-3.5 text-muted-foreground" />
                Record a document
              </div>

              <div className="grid gap-2">
                <div className="grid grid-cols-2 gap-2">
                  <Input
                    className="h-9"
                    placeholder="FSSAI licence"
                    value={doc.type}
                    onChange={(e) => setDoc((d) => ({ ...d, type: e.target.value }))}
                  />
                  <Input
                    className="h-9"
                    placeholder="File name"
                    value={doc.fileName}
                    onChange={(e) => setDoc((d) => ({ ...d, fileName: e.target.value }))}
                  />
                </div>
                <div>
                  <Label className="text-[11px] text-muted-foreground">Expires</Label>
                  <Input
                    type="date"
                    className="mt-1 h-9"
                    value={doc.expiry}
                    onChange={(e) => setDoc((d) => ({ ...d, expiry: e.target.value }))}
                  />
                </div>
                <Button
                  className="h-9"
                  disabled={busy}
                  onClick={() => {
                    if (!doc.type.trim()) {
                      toast.error("Say what the document is.");
                      return;
                    }
                    void act(
                      () =>
                        vendorsApi.addDocument(vendor.id, {
                          documentType: doc.type.trim(),
                          fileName: doc.fileName.trim() || doc.type.trim(),
                          expiryDate: doc.expiry || null,
                        }),
                      "Document recorded."
                    ).then((ok) => {
                      if (ok) setDoc({ type: "", fileName: "", expiry: "" });
                    });
                  }}
                >
                  <Plus className="mr-1 size-3.5" />
                  Add document
                </Button>
              </div>
            </div>
          </TabsContent>
        </Tabs>
      </SheetContent>
    </Sheet>
  );
}
