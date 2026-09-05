"use client";

import * as React from "react";
import { Mail, MessageCircle, Send, Loader2, ExternalLink } from "lucide-react";
import { toast } from "sonner";

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
import { Textarea } from "@/components/ui/textarea";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { quoteBuilderApi } from "@/lib/inventory-api";

interface SendQuotationDialogProps {
  quotationId: number;
  quoteNumber: string;
  customerName?: string | null;
  customerEmail?: string | null;
  customerPhone?: string | null;
  grandTotal?: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSent?: () => void;
}

export function SendQuotationDialog({
  quotationId,
  quoteNumber,
  customerName,
  customerEmail,
  customerPhone,
  grandTotal,
  open,
  onOpenChange,
  onSent,
}: SendQuotationDialogProps) {
  const [tab, setTab] = React.useState<"email" | "whatsapp">("email");

  // Email form state
  const [emailTo, setEmailTo] = React.useState(customerEmail ?? "");
  const [emailCc, setEmailCc] = React.useState("");
  const [emailSubject, setEmailSubject] = React.useState(
    `Quotation ${quoteNumber} — Bull Events`
  );
  const [emailBody, setEmailBody] = React.useState(
    `Dear ${customerName || "Customer"},\n\nPlease find attached the quotation (${quoteNumber}) for your review. Let us know if you have any questions.\n\nBest regards,\nBull Events Team`
  );

  // WhatsApp form state
  const [phone, setPhone] = React.useState(customerPhone ?? "");
  const [waMessage, setWaMessage] = React.useState(
    `Hi ${customerName || "there"}, here is the quotation ${quoteNumber} from Bull Events. You can view the full proposal and confirm online. Please let us know if you'd like any modifications.`
  );

  const [sending, setSending] = React.useState(false);

  React.useEffect(() => {
    if (open) {
      if (customerEmail) setEmailTo(customerEmail);
      if (customerPhone) setPhone(customerPhone);
      setEmailSubject(`Quotation ${quoteNumber} — Bull Events`);
      setEmailBody(
        `Dear ${customerName || "Customer"},\n\nPlease find attached the quotation (${quoteNumber}) for your review. Let us know if you have any questions.\n\nBest regards,\nBull Events Team`
      );
      setWaMessage(
        `Hi ${customerName || "there"}, here is the quotation ${quoteNumber} from Bull Events. Please let us know if you'd like to proceed or have any questions!`
      );
    }
  }, [open, customerEmail, customerPhone, customerName, quoteNumber]);

  async function handleSendEmail() {
    if (!emailTo.trim()) {
      toast.error("Please enter a recipient email address.");
      return;
    }
    setSending(true);
    try {
      const res = await quoteBuilderApi.sendEmail(quotationId, {
        to: emailTo.trim(),
        cc: emailCc.trim() || null,
        subject: emailSubject.trim(),
        body: emailBody.trim(),
        attachPdf: true,
      });
      toast.success(res.message);
      onSent?.();
      onOpenChange(false);
    } catch (err: any) {
      toast.error(err.message || "Failed to send email.");
    } finally {
      setSending(false);
    }
  }

  async function handleSendWhatsApp() {
    if (!phone.trim()) {
      toast.error("Please enter a recipient phone number.");
      return;
    }
    setSending(true);
    try {
      const cleanPhone = phone.replace(/[^0-9]/g, "");
      const res = await quoteBuilderApi.sendWhatsApp(quotationId, {
        phone: cleanPhone,
        message: waMessage.trim(),
      });
      toast.success(res.message);

      // Open WhatsApp web in new tab for direct engagement
      const encodedMsg = encodeURIComponent(waMessage.trim());
      const waUrl = `https://wa.me/${cleanPhone}?text=${encodedMsg}`;
      window.open(waUrl, "_blank");

      onSent?.();
      onOpenChange(false);
    } catch (err: any) {
      toast.error(err.message || "Failed to log WhatsApp send.");
    } finally {
      setSending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Send Quotation {quoteNumber}</DialogTitle>
          <DialogDescription>
            Deliver quotation document directly to {customerName || "the customer"} via Email or WhatsApp.
          </DialogDescription>
        </DialogHeader>

        <Tabs value={tab} onValueChange={(v) => setTab(v as any)} className="w-full">
          <TabsList className="grid w-full grid-cols-2">
            <TabsTrigger value="email" className="flex items-center gap-2">
              <Mail className="h-4 w-4" /> Email (with PDF)
            </TabsTrigger>
            <TabsTrigger value="whatsapp" className="flex items-center gap-2">
              <MessageCircle className="h-4 w-4" /> WhatsApp
            </TabsTrigger>
          </TabsList>

          {/* Email Tab */}
          <TabsContent value="email" className="space-y-4 pt-4">
            <div className="space-y-2">
              <Label htmlFor="email-to">To (Customer Email)</Label>
              <Input
                id="email-to"
                type="email"
                placeholder="customer@example.com"
                value={emailTo}
                onChange={(e) => setEmailTo(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="email-cc">CC (Optional)</Label>
              <Input
                id="email-cc"
                type="email"
                placeholder="manager@company.com"
                value={emailCc}
                onChange={(e) => setEmailCc(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="email-subject">Subject</Label>
              <Input
                id="email-subject"
                value={emailSubject}
                onChange={(e) => setEmailSubject(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="email-body">Message</Label>
              <Textarea
                id="email-body"
                rows={4}
                value={emailBody}
                onChange={(e) => setEmailBody(e.target.value)}
              />
            </div>
            <div className="rounded bg-muted p-2.5 text-xs text-muted-foreground flex items-center gap-2">
              <span>📎</span>
              <span>Quotation PDF document ({quoteNumber}.pdf) will be attached automatically.</span>
            </div>
          </TabsContent>

          {/* WhatsApp Tab */}
          <TabsContent value="whatsapp" className="space-y-4 pt-4">
            <div className="space-y-2">
              <Label htmlFor="wa-phone">Recipient Phone (with country code)</Label>
              <Input
                id="wa-phone"
                placeholder="+91 98765 43210"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="wa-msg">Message</Label>
              <Textarea
                id="wa-msg"
                rows={5}
                value={waMessage}
                onChange={(e) => setWaMessage(e.target.value)}
              />
            </div>
            <p className="text-xs text-muted-foreground">
              Clicking send will record the delivery activity on the CRM timeline and open WhatsApp Web with your pre-filled message.
            </p>
          </TabsContent>
        </Tabs>

        <DialogFooter className="mt-4">
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={sending}>
            Cancel
          </Button>
          {tab === "email" ? (
            <Button onClick={handleSendEmail} disabled={sending}>
              {sending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}
              Send Email
            </Button>
          ) : (
            <Button onClick={handleSendWhatsApp} disabled={sending} className="bg-emerald-600 hover:bg-emerald-700">
              {sending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <ExternalLink className="mr-2 h-4 w-4" />}
              Open WhatsApp
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
