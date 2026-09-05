"use client";

import * as React from "react";
import { Loader2 } from "lucide-react";
import { toast } from "sonner";

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
import { Textarea } from "@/components/ui/textarea";

import { ApiError } from "@/lib/api";
import { quoteBuilderApi } from "@/lib/inventory-api";

interface ExtendValidityDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  quotationId: number;
  currentValidUntil: string;
  onSaved: () => void;
}

export function ExtendValidityDialog({
  open,
  onOpenChange,
  quotationId,
  currentValidUntil,
  onSaved,
}: ExtendValidityDialogProps) {
  const [busy, setBusy] = React.useState(false);
  const [newValidUntil, setNewValidUntil] = React.useState("");
  const [reason, setReason] = React.useState("");

  React.useEffect(() => {
    if (open) {
      // Default to +7 days from current or today, whichever is later
      const base = Math.max(new Date(currentValidUntil).getTime(), Date.now());
      const nextWeek = new Date(base + 7 * 24 * 60 * 60 * 1000);
      setNewValidUntil(nextWeek.toISOString().slice(0, 16));
      setReason("");
    }
  }, [open, currentValidUntil]);

  async function handleSave() {
    if (!newValidUntil) return;

    setBusy(true);
    try {
      await quoteBuilderApi.extendValidity(quotationId, {
        newValidUntil,
        reason: reason.trim() || null,
      });
      toast.success("Validity extended");
      onSaved();
      onOpenChange(false);
    } catch (error) {
      toast.error("Could not extend validity", {
        description: error instanceof ApiError ? error.message : "Network error",
      });
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Extend Validity</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label>New Valid Until</Label>
            <Input
              type="datetime-local"
              value={newValidUntil}
              onChange={(e) => setNewValidUntil(e.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label>Reason (Optional)</Label>
            <Textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Why are we extending this?"
              rows={3}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            Cancel
          </Button>
          <Button onClick={handleSave} disabled={busy || !newValidUntil}>
            {busy && <Loader2 className="size-4 animate-spin mr-2" />}
            Extend
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
