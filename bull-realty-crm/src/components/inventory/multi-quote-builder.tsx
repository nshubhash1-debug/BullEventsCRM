"use client";

import * as React from "react";
import { Building2, Check, Loader2, Sparkles } from "lucide-react";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
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
import { ScrollArea } from "@/components/ui/scroll-area";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import {
  quoteBuilderApi,
  formatRupees,
  formatIndian,
  formatPercent,
  type BoardUnit,
  type PaymentPlan,
  type MultiUnitPreview,
} from "@/lib/inventory-api";

interface MultiQuoteBuilderProps {
  units: BoardUnit[];
  projectId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: () => void;
}

export function MultiQuoteBuilder({
  units,
  projectId,
  open,
  onOpenChange,
  onCreated,
}: MultiQuoteBuilderProps) {
  const [plans, setPlans] = React.useState<PaymentPlan[] | null>(null);
  const [planId, setPlanId] = React.useState<string>("");
  const [discountInput, setDiscountInput] = React.useState("");
  const [customerName, setCustomerName] = React.useState("");
  const [customerEmail, setCustomerEmail] = React.useState("");
  const [customerPhone, setCustomerPhone] = React.useState("");
  const [notes, setNotes] = React.useState("");
  const [preview, setPreview] = React.useState<MultiUnitPreview | null>(null);
  const [calculating, setCalculating] = React.useState(false);
  const [saving, setSaving] = React.useState(false);

  React.useEffect(() => {
    if (!open) return;
    quoteBuilderApi
      .plans(projectId)
      .then((loaded) => {
        setPlans(loaded);
        if (loaded.length > 0) {
          setPlanId(String(loaded[0].id));
          setDiscountInput(String(+(loaded[0].standardDiscount * 100).toFixed(2)));
        }
      })
      .catch(() => setPlans([]));
  }, [open, projectId]);

  React.useEffect(() => {
    if (!open || !planId || units.length === 0) return;

    const discountVal = discountInput.trim() ? Number(discountInput) / 100 : null;
    setCalculating(true);

    quoteBuilderApi
      .previewMulti({
        unitIds: units.map((u) => u.id),
        paymentPlanId: Number(planId),
        discount: discountVal,
      })
      .then(setPreview)
      .catch(() => setPreview(null))
      .finally(() => setCalculating(false));
  }, [open, planId, discountInput, units]);

  async function handleSave() {
    if (!customerName.trim()) {
      toast.error("Customer name is required.");
      return;
    }
    if (!planId) {
      toast.error("Please select a payment plan.");
      return;
    }

    setSaving(true);
    try {
      const discountVal = discountInput.trim() ? Number(discountInput) / 100 : null;
      const res = await quoteBuilderApi.createFromUnits({
        unitIds: units.map((u) => u.id),
        paymentPlanId: Number(planId),
        discount: discountVal,
        customerName: customerName.trim(),
        customerEmail: customerEmail.trim() || null,
        customerPhone: customerPhone.trim() || null,
        notes: notes.trim() || null,
      });

      toast.success(res.message);
      onCreated();
      onOpenChange(false);
    } catch (err: any) {
      toast.error(err.message || "Failed to create combo quotation.");
    } finally {
      setSaving(false);
    }
  }

  const selectedPlan = plans?.find((p) => String(p.id) === planId);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] max-w-3xl overflow-hidden p-0">
        <DialogHeader className="border-b p-5 pb-4">
          <div className="flex items-center gap-2">
            <span className="flex size-8 items-center justify-center rounded-lg bg-primary/10 text-primary">
              <Sparkles className="size-4" />
            </span>
            <div>
              <DialogTitle>Multi-space event proposal</DialogTitle>
              <DialogDescription>
                Bundle {units.length} units ({units.map((u) => u.unitNumber).join(", ")}) into a single proposal with consolidated pricing.
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        <ScrollArea className="max-h-[60vh] p-5">
          <div className="space-y-6">
            {/* Selected Units Summary */}
            <div>
              <Label className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Selected Units ({units.length})
              </Label>
              <div className="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-3">
                {units.map((u) => (
                  <div key={u.id} className="rounded-md border p-2.5 text-xs bg-muted/20">
                    <div className="font-semibold text-primary">{u.unitNumber}</div>
                    <div className="text-muted-foreground">
                      Floor {u.floor} · {u.configuration}
                    </div>
                    <div className="mt-1 font-medium">{formatIndian(u.superArea ?? 0)} sq ft</div>
                  </div>
                ))}
              </div>
            </div>

            {/* Pricing Parameters */}
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="plan-select">Payment Plan</Label>
                <Select value={planId} onValueChange={(val) => {
                  setPlanId(val);
                  const p = plans?.find((x) => String(x.id) === val);
                  if (p) setDiscountInput(String(+(p.standardDiscount * 100).toFixed(2)));
                }}>
                  <SelectTrigger id="plan-select">
                    <SelectValue placeholder="Select plan" />
                  </SelectTrigger>
                  <SelectContent>
                    {plans?.map((p) => (
                      <SelectItem key={p.id} value={String(p.id)}>
                        {p.name} ({(p.standardDiscount * 100).toFixed(0)}% standard)
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label htmlFor="combo-discount">Combo Discount (%)</Label>
                <Input
                  id="combo-discount"
                  type="number"
                  step="0.1"
                  min="0"
                  max="90"
                  value={discountInput}
                  onChange={(e) => setDiscountInput(e.target.value)}
                  placeholder="e.g. 15"
                />
              </div>
            </div>

            {/* Live Pricing Preview */}
            <div className="rounded-lg border bg-card p-4 space-y-3">
              <div className="flex items-center justify-between border-b pb-2">
                <span className="font-medium text-sm">Consolidated Consideration</span>
                {calculating ? (
                  <Loader2 className="size-4 animate-spin text-muted-foreground" />
                ) : (
                  <Badge variant="outline" className="text-xs">
                    {preview?.units.length ?? units.length} Units Total
                  </Badge>
                )}
              </div>

              <div className="grid grid-cols-3 gap-2 text-xs">
                <div>
                  <span className="text-muted-foreground block">Combined Area</span>
                  <span className="font-medium">{formatIndian(preview?.combinedSaleableArea ?? 0)} sq ft</span>
                </div>
                <div>
                  <span className="text-muted-foreground block">Base Unit Cost</span>
                  <span className="font-medium">{formatRupees(preview?.combinedBasicAmount ?? 0)}</span>
                </div>
                <div>
                  <span className="text-muted-foreground block">Grand Total</span>
                  <span className="font-bold text-primary text-sm">{formatRupees(preview?.combinedGrandTotal ?? 0)}</span>
                </div>
              </div>
            </div>

            {/* Customer Details */}
            <div className="space-y-3">
              <Label className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Customer Information
              </Label>
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                <Input
                  placeholder="Customer Name *"
                  value={customerName}
                  onChange={(e) => setCustomerName(e.target.value)}
                />
                <Input
                  placeholder="Email (Optional)"
                  type="email"
                  value={customerEmail}
                  onChange={(e) => setCustomerEmail(e.target.value)}
                />
                <Input
                  placeholder="Phone (Optional)"
                  value={customerPhone}
                  onChange={(e) => setCustomerPhone(e.target.value)}
                />
              </div>
              <Textarea
                placeholder="Notes / package terms (optional)"
                rows={2}
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
              />
            </div>
          </div>
        </ScrollArea>

        <DialogFooter className="border-t p-4">
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={handleSave} disabled={saving || !preview}>
            {saving ? <Loader2 className="mr-2 size-4 animate-spin" /> : null}
            Create multi-space proposal
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
