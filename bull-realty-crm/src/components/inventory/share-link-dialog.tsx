"use client";

import * as React from "react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { quoteBuilderApi, ShareLink } from "@/lib/inventory-api";
import { toast } from "sonner";
import { Copy, Trash, Loader2 } from "lucide-react";
import { formatDate, formatDateTime } from "@/lib/crm-api";

interface ShareLinkDialogProps {
  quotationId: number;
  quoteNumber: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function ShareLinkDialog({
  quotationId,
  quoteNumber,
  open,
  onOpenChange,
}: ShareLinkDialogProps) {
  const [links, setLinks] = React.useState<ShareLink[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [generating, setGenerating] = React.useState(false);

  React.useEffect(() => {
    if (open) {
      loadLinks();
    }
  }, [open, quotationId]);

  async function loadLinks() {
    setLoading(true);
    try {
      const data = await quoteBuilderApi.shareLinks(quotationId);
      setLinks(data);
    } catch {
      toast.error("Failed to load share links");
    } finally {
      setLoading(false);
    }
  }

  async function handleGenerate() {
    setGenerating(true);
    try {
      await quoteBuilderApi.createShareLink(quotationId);
      toast.success("Link generated");
      await loadLinks();
    } catch {
      toast.error("Failed to generate link");
    } finally {
      setGenerating(false);
    }
  }

  async function handleRevoke(id: number) {
    try {
      await quoteBuilderApi.revokeShareLink(id);
      toast.success("Link revoked");
      await loadLinks();
    } catch {
      toast.error("Failed to revoke link");
    }
  }

  function handleCopy(token: string) {
    const url = window.location.origin + "/q/" + token;
    navigator.clipboard.writeText(url);
    toast.success("Link copied to clipboard");
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Share Quotation {quoteNumber}</DialogTitle>
          <DialogDescription>
            Generate a public link to share this quotation with the customer.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <Button
            onClick={handleGenerate}
            disabled={generating}
            className="w-full"
          >
            {generating ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            Generate New Link
          </Button>

          {loading ? (
            <div className="flex justify-center py-4">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : (
            <div className="space-y-3">
              {links.map((link) => (
                <div
                  key={link.id}
                  className="rounded-md border p-3 space-y-2 text-sm"
                >
                  <div className="flex items-center justify-between">
                    <span className="font-medium">
                      Expires {formatDate(link.expiresAt)}
                    </span>
                    <Badge variant={link.isActive ? "default" : "secondary"}>
                      {link.isActive ? "Active" : "Expired"}
                    </Badge>
                  </div>
                  <div className="flex items-center gap-2">
                    <Input
                      readOnly
                      value={window.location.origin + "/q/" + link.token}
                      className="h-8 text-xs"
                    />
                    <Button
                      variant="outline"
                      size="icon"
                      className="h-8 w-8 shrink-0"
                      onClick={() => handleCopy(link.token)}
                    >
                      <Copy className="h-4 w-4" />
                    </Button>
                    <Button
                      variant="destructive"
                      size="icon"
                      className="h-8 w-8 shrink-0"
                      onClick={() => handleRevoke(link.id)}
                    >
                      <Trash className="h-4 w-4" />
                    </Button>
                  </div>
                  <div className="text-xs text-muted-foreground flex justify-between">
                    <span>Views: {link.viewCount}</span>
                    {link.lastViewedAt && (
                      <span>Last viewed: {formatDateTime(link.lastViewedAt)}</span>
                    )}
                  </div>
                  {link.responseStatus && (
                    <div className="text-xs bg-muted p-2 rounded mt-2">
                      <span className="font-medium">Response: {link.responseStatus}</span>
                      {link.customerComment && (
                        <p className="mt-1 italic">"{link.customerComment}"</p>
                      )}
                    </div>
                  )}
                </div>
              ))}
              {links.length === 0 && (
                <p className="text-sm text-muted-foreground text-center py-2">
                  No active links
                </p>
              )}
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
