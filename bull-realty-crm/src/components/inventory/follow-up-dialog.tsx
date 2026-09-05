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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";

import { ApiError } from "@/lib/api";
import { quoteBuilderApi } from "@/lib/inventory-api";

interface FollowUpDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  quotationId: number;
  onSaved: () => void;
}

export function FollowUpDialog({ open, onOpenChange, quotationId, onSaved }: FollowUpDialogProps) {
  const [busy, setBusy] = React.useState(false);
  const [note, setNote] = React.useState("");
  const [channel, setChannel] = React.useState("Call");
  const [outcome, setOutcome] = React.useState("");
  const [nextFollowUp, setNextFollowUp] = React.useState("");

  React.useEffect(() => {
    if (open) {
      setNote("");
      setChannel("Call");
      setOutcome("");
      setNextFollowUp("");
    }
  }, [open]);

  async function handleSave() {
    if (!note.trim()) {
      toast.error("Please enter a note.");
      return;
    }

    setBusy(true);
    try {
      await quoteBuilderApi.addFollowUp(quotationId, {
        note: note.trim(),
        channel,
        outcome: outcome.trim() || undefined,
        nextFollowUpAt: nextFollowUp || null,
      });
      toast.success("Follow-up saved");
      onSaved();
      onOpenChange(false);
    } catch (error) {
      toast.error("Could not save follow-up", {
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
          <DialogTitle>Log Follow-up</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label>Note</Label>
            <Textarea
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder="What was discussed?"
              rows={3}
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Channel</Label>
              <Select value={channel} onValueChange={setChannel}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Call">Call</SelectItem>
                  <SelectItem value="Email">Email</SelectItem>
                  <SelectItem value="WhatsApp">WhatsApp</SelectItem>
                  <SelectItem value="Meeting">Meeting</SelectItem>
                  <SelectItem value="SiteVisit">Venue Visit</SelectItem>
                </SelectContent>
              </Select>
            </div>
            
            <div className="space-y-2">
              <Label>Outcome</Label>
              <Input
                value={outcome}
                onChange={(e) => setOutcome(e.target.value)}
                placeholder="e.g. Sent revised quote"
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label>Next Follow-up (Optional)</Label>
            <Input
              type="datetime-local"
              value={nextFollowUp}
              onChange={(e) => setNextFollowUp(e.target.value)}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            Cancel
          </Button>
          <Button onClick={handleSave} disabled={busy || !note.trim()}>
            {busy && <Loader2 className="size-4 animate-spin mr-2" />}
            Save Follow-up
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
