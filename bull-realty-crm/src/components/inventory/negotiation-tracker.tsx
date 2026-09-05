"use client";

import * as React from "react";
import { Loader2, HandshakeIcon } from "lucide-react";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";

import { ApiError } from "@/lib/api";
import { formatPercent, formatRupees, quoteBuilderApi, type QuotationNegotiation } from "@/lib/inventory-api";

export function NegotiationTracker({ 
  quotationId, 
  negotiations, 
  onUpdated 
}: { 
  quotationId: number; 
  negotiations: QuotationNegotiation[]; 
  onUpdated: () => void;
}) {
  const [open, setOpen] = React.useState(false);

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <h3 className="font-medium text-sm">Negotiation Rounds</h3>
        <Button size="sm" onClick={() => setOpen(true)}>
          <HandshakeIcon className="size-3 mr-1" /> Log Round
        </Button>
      </div>

      {negotiations.length === 0 ? (
        <p className="text-sm text-muted-foreground py-4 text-center">No negotiation history.</p>
      ) : (
        <div className="space-y-3">
          {negotiations.map(n => (
            <div key={n.id} className="border rounded-lg p-3 text-sm bg-card">
              <div className="flex justify-between items-start mb-3">
                <div className="flex gap-2 items-center">
                  <span className="font-semibold text-muted-foreground">Round {n.round}</span>
                  <Badge variant="outline">{n.type}</Badge>
                </div>
                <span className="text-xs text-muted-foreground">By {n.createdByName}</span>
              </div>

              <div className="grid grid-cols-2 gap-4">
                {n.customerDemand && (
                  <div className="bg-red-500/10 p-2 rounded text-red-900 dark:text-red-200">
                    <div className="text-xs font-semibold mb-1 opacity-70">Customer Requested</div>
                    <p>{n.customerDemand}</p>
                    {n.requestedDiscount && <div className="mt-1 font-medium">{formatPercent(n.requestedDiscount / 100, 2)} discount</div>}
                  </div>
                )}
                
                {n.ourResponse && (
                  <div className="bg-emerald-500/10 p-2 rounded text-emerald-900 dark:text-emerald-200">
                    <div className="text-xs font-semibold mb-1 opacity-70">Our Offer</div>
                    <p>{n.ourResponse}</p>
                    {n.offeredDiscount && <div className="mt-1 font-medium">{formatPercent(n.offeredDiscount / 100, 2)} discount</div>}
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      <LogNegotiationDialog 
        open={open} 
        onOpenChange={setOpen} 
        quotationId={quotationId} 
        onSaved={onUpdated} 
      />
    </div>
  );
}

function LogNegotiationDialog({ 
  open, 
  onOpenChange, 
  quotationId, 
  onSaved 
}: { 
  open: boolean; 
  onOpenChange: (open: boolean) => void; 
  quotationId: number; 
  onSaved: () => void;
}) {
  const [busy, setBusy] = React.useState(false);
  const [type, setType] = React.useState("CounterOffer");
  const [requestedDiscount, setRequestedDiscount] = React.useState("");
  const [offeredDiscount, setOfferedDiscount] = React.useState("");
  const [customerDemand, setCustomerDemand] = React.useState("");
  const [ourResponse, setOurResponse] = React.useState("");

  React.useEffect(() => {
    if (open) {
      setType("CounterOffer");
      setRequestedDiscount("");
      setOfferedDiscount("");
      setCustomerDemand("");
      setOurResponse("");
    }
  }, [open]);

  async function handleSave() {
    setBusy(true);
    try {
      await quoteBuilderApi.addNegotiation(quotationId, {
        type,
        requestedDiscount: requestedDiscount ? parseFloat(requestedDiscount) : null,
        offeredDiscount: offeredDiscount ? parseFloat(offeredDiscount) : null,
        customerDemand: customerDemand.trim() || null,
        ourResponse: ourResponse.trim() || null,
      });
      toast.success("Negotiation logged");
      onSaved();
      onOpenChange(false);
    } catch (error) {
      toast.error("Could not save negotiation", {
        description: error instanceof ApiError ? error.message : "Network error",
      });
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Log Negotiation Round</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-2">
          <div className="space-y-2">
            <Label>Type</Label>
            <Select value={type} onValueChange={setType}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="CounterOffer">Counter Offer</SelectItem>
                <SelectItem value="Concession">Concession Given</SelectItem>
                <SelectItem value="FinalOffer">Final Offer</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Customer Request (Notes)</Label>
              <Textarea
                value={customerDemand}
                onChange={(e) => setCustomerDemand(e.target.value)}
                placeholder="What they asked for..."
                rows={2}
              />
            </div>
            <div className="space-y-2">
              <Label>Requested Discount (%)</Label>
              <Input
                type="number"
                step="0.01"
                value={requestedDiscount}
                onChange={(e) => setRequestedDiscount(e.target.value)}
                placeholder="e.g. 5"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Our Offer (Notes)</Label>
              <Textarea
                value={ourResponse}
                onChange={(e) => setOurResponse(e.target.value)}
                placeholder="What we countered with..."
                rows={2}
              />
            </div>
            <div className="space-y-2">
              <Label>Offered Discount (%)</Label>
              <Input
                type="number"
                step="0.01"
                value={offeredDiscount}
                onChange={(e) => setOfferedDiscount(e.target.value)}
                placeholder="e.g. 3.5"
              />
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            Cancel
          </Button>
          <Button onClick={handleSave} disabled={busy}>
            {busy && <Loader2 className="size-4 animate-spin mr-2" />}
            Save Round
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
