"use client";

import * as React from "react";
import {
  Ban,
  CalendarClock,
  CheckCircle2,
  Clock,
  Download,
  FileText,
  HandshakeIcon,
  History,
  MessageSquare,
  Pencil,
  Send,
  Loader2,
} from "lucide-react";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ScrollArea } from "@/components/ui/scroll-area";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { QuotationActions } from "@/components/inventory/quotation-actions";
import { FollowUpDialog } from "@/components/inventory/follow-up-dialog";
import { NegotiationTracker } from "@/components/inventory/negotiation-tracker";
import { ExtendValidityDialog } from "@/components/inventory/extend-validity-dialog";
import { QuotationResourcePlan } from "@/components/resources/quotation-resource-plan";

import { ApiError } from "@/lib/api";
import { quoteBuilderApi, type QuotationDetail, type QuotationActivity, type QuotationFollowUp, type QuotationNegotiation } from "@/lib/inventory-api";
import { formatDate, timeAgo } from "@/lib/crm-api";
import { formatMoney } from "@/lib/crm-api";
import { formatRupees, formatPercent } from "@/lib/inventory-api";
import { cn } from "@/lib/utils";

interface QuotationDetailSheetProps {
  quotationId: number | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onEdit?: (detail: QuotationDetail) => void;
  onChanged: () => void;
}

export function QuotationDetailSheet({
  quotationId,
  open,
  onOpenChange,
  onEdit,
  onChanged,
}: QuotationDetailSheetProps) {
  const [detail, setDetail] = React.useState<QuotationDetail | null>(null);
  const [timeline, setTimeline] = React.useState<QuotationActivity[] | null>(null);
  const [followUps, setFollowUps] = React.useState<QuotationFollowUp[] | null>(null);
  const [negotiations, setNegotiations] = React.useState<QuotationNegotiation[] | null>(null);
  const [loading, setLoading] = React.useState(false);
  
  const [followUpOpen, setFollowUpOpen] = React.useState(false);
  const [extendOpen, setExtendOpen] = React.useState(false);

  React.useEffect(() => {
    if (!open || !quotationId) {
      setDetail(null);
      setTimeline(null);
      setFollowUps(null);
      setNegotiations(null);
      return;
    }

    let active = true;
    setLoading(true);
    
    Promise.all([
      quoteBuilderApi.detail(quotationId),
      quoteBuilderApi.timeline(quotationId),
      quoteBuilderApi.followUps(quotationId),
      quoteBuilderApi.negotiations(quotationId)
    ]).then(([d, t, f, n]) => {
      if (!active) return;
      setDetail(d);
      setTimeline(t);
      setFollowUps(f);
      setNegotiations(n);
    }).catch(error => {
      toast.error("Failed to load quotation details", {
        description: error instanceof ApiError ? error.message : "Network error"
      });
    }).finally(() => {
      if (active) setLoading(false);
    });

    return () => { active = false; };
  }, [open, quotationId]);

  const refreshFollowUps = async () => {
    if (!quotationId) return;
    try {
      const data = await quoteBuilderApi.followUps(quotationId);
      setFollowUps(data);
      onChanged();
    } catch (e) {}
  };

  const refreshNegotiations = async () => {
    if (!quotationId) return;
    try {
      const data = await quoteBuilderApi.negotiations(quotationId);
      setNegotiations(data);
      onChanged();
    } catch (e) {}
  };

  const refreshDetail = async () => {
    if (!quotationId) return;
    try {
      const d = await quoteBuilderApi.detail(quotationId);
      setDetail(d);
      onChanged();
    } catch (e) {}
  };

  if (!open) return null;

  return (
    <>
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="sm:max-w-2xl w-full flex flex-col p-0">
        {loading || !detail ? (
          <div className="p-6 space-y-6">
            <Skeleton className="h-8 w-1/2" />
            <Skeleton className="h-4 w-1/3" />
            <Skeleton className="h-[400px] w-full" />
          </div>
        ) : (
          <>
            <SheetHeader className="p-6 border-b text-left">
              <div className="flex items-start justify-between">
                <div>
                  <div className="flex items-center gap-2 mb-1">
                    <SheetTitle className="text-xl">{detail.quoteNumber}</SheetTitle>
                    <Badge variant="outline">v{detail.version}</Badge>
                    <Badge className={cn(
                      detail.status === "Accepted" && "bg-emerald-500",
                      detail.status === "Draft" && "bg-neutral-500",
                      detail.status === "Declined" && "bg-red-500"
                    )}>
                      {detail.status}
                    </Badge>
                  </div>
                  <div className="text-sm text-muted-foreground">
                    {detail.projectName} • {detail.unitNumber} • {detail.customerName}
                  </div>
                </div>
                <div className="text-right text-sm">
                  <div className="font-medium">Valid until</div>
                  <div className={cn(
                    new Date(detail.validUntil) < new Date() && "text-red-500 font-semibold"
                  )}>
                    {formatDate(detail.validUntil)}
                  </div>
                  <Button variant="link" size="sm" className="h-auto p-0 text-[10px]" onClick={() => setExtendOpen(true)}>
                    Extend
                  </Button>
                </div>
              </div>
            </SheetHeader>

            <Tabs defaultValue="overview" className="flex-1 flex flex-col min-h-0">
              <div className="px-6 pt-2 border-b">
                <TabsList className="bg-transparent space-x-2">
                  <TabsTrigger value="overview" className="data-[state=active]:bg-muted">Overview</TabsTrigger>
                  <TabsTrigger value="activity" className="data-[state=active]:bg-muted">Activity</TabsTrigger>
                  <TabsTrigger value="followups" className="data-[state=active]:bg-muted">Follow-ups</TabsTrigger>
                  <TabsTrigger value="negotiations" className="data-[state=active]:bg-muted">Negotiation</TabsTrigger>
                  <TabsTrigger value="resources" className="data-[state=active]:bg-muted">Cost &amp; margin</TabsTrigger>
                </TabsList>
              </div>

              <ScrollArea className="flex-1 p-6">
                <TabsContent value="overview" className="m-0 space-y-6">
                  <div className="grid grid-cols-2 gap-4 text-sm">
                    <div>
                      <div className="text-muted-foreground mb-1">Rate / sqft</div>
                      <div className="font-medium">{formatRupees(detail.ratePerSqft)}</div>
                    </div>
                    <div>
                      <div className="text-muted-foreground mb-1">Effective Rate</div>
                      <div className="font-medium">{formatRupees(detail.effectiveRatePerSqft)}</div>
                    </div>
                    <div>
                      <div className="text-muted-foreground mb-1">Discount</div>
                      <div className="font-medium text-amber-600">{formatPercent(detail.discountPercent / 100, 2)}</div>
                    </div>
                    <div>
                      <div className="text-muted-foreground mb-1">Tax</div>
                      <div className="font-medium">{formatRupees(detail.taxAmount)}</div>
                    </div>
                  </div>

                  <div className="rounded-lg border bg-muted/30 p-4 space-y-3">
                    <div className="flex justify-between text-sm">
                      <span className="text-muted-foreground">Basic Amount</span>
                      <span>{formatRupees(detail.subtotal)}</span>
                    </div>
                    <div className="flex justify-between text-sm">
                      <span className="text-muted-foreground">Other Charges</span>
                      <span>{formatRupees(detail.chargesTotal)}</span>
                    </div>
                    <div className="flex justify-between text-sm font-medium border-t pt-3">
                      <span>Grand Total</span>
                      <span>{formatRupees(detail.grandTotal)}</span>
                    </div>
                  </div>

                  <div>
                    <h4 className="font-medium text-sm mb-3">Milestone Schedule</h4>
                    <div className="space-y-2">
                      {detail.milestones.map((m, i) => (
                        <div key={i} className="flex justify-between text-sm p-2 rounded border">
                          <div>
                            <div className="font-medium">{m.label}</div>
                            <div className="text-muted-foreground text-[11px]">{formatPercent(m.percent / 100, 2)}</div>
                          </div>
                          <div className="text-right">
                            <div className="font-medium">{formatRupees(m.totalAmount)}</div>
                            {m.dueDate && <div className="text-muted-foreground text-[11px]">Due: {formatDate(m.dueDate)}</div>}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                </TabsContent>

                <TabsContent value="activity" className="m-0 space-y-4">
                  {!timeline ? <Loader2 className="animate-spin size-4 mx-auto" /> : 
                   timeline.length === 0 ? <p className="text-sm text-muted-foreground">No activity recorded.</p> : (
                    <div className="space-y-4 relative before:absolute before:inset-0 before:ml-2 before:-translate-x-px md:before:mx-auto md:before:translate-x-0 before:h-full before:w-0.5 before:bg-gradient-to-b before:from-transparent before:via-muted-foreground/20 before:to-transparent">
                      {timeline.map((item) => (
                        <div key={item.id} className="relative flex items-center justify-between md:justify-normal md:odd:flex-row-reverse group is-active">
                          <div className="flex items-center justify-center w-5 h-5 rounded-full border border-background bg-muted-foreground/10 text-muted-foreground shadow shrink-0 md:order-1 md:group-odd:-translate-x-1/2 md:group-even:translate-x-1/2">
                            <History className="size-3" />
                          </div>
                          <div className="w-[calc(100%-2.5rem)] md:w-[calc(50%-1.25rem)] p-3 rounded border bg-card shadow-sm">
                            <div className="flex items-center justify-between mb-1">
                              <span className="font-medium text-sm">{item.type}</span>
                              <span className="text-[10px] text-muted-foreground">{timeAgo(item.createdAt)}</span>
                            </div>
                            <p className="text-sm text-muted-foreground">{item.description}</p>
                            <p className="text-[10px] mt-1 opacity-70">By {item.actorName}</p>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </TabsContent>

                <TabsContent value="followups" className="m-0 space-y-4">
                  <div className="flex justify-end">
                    <Button size="sm" onClick={() => setFollowUpOpen(true)}>
                      <MessageSquare className="size-3 mr-1" /> Add Follow-up
                    </Button>
                  </div>
                  {!followUps ? <Loader2 className="animate-spin size-4 mx-auto" /> : 
                   followUps.length === 0 ? <p className="text-sm text-muted-foreground text-center py-4">No follow-ups recorded.</p> : (
                    <div className="space-y-3">
                      {followUps.map(f => (
                        <div key={f.id} className="p-3 border rounded-lg text-sm">
                          <div className="flex justify-between items-start mb-2">
                            <Badge variant="secondary">{f.channel}</Badge>
                            <span className="text-xs text-muted-foreground">{formatDate(f.createdAt)}</span>
                          </div>
                          <p className="mb-2 whitespace-pre-wrap">{f.note}</p>
                          {f.outcome && <p className="text-xs font-medium text-muted-foreground bg-muted p-1.5 rounded">Outcome: {f.outcome}</p>}
                          {f.nextFollowUpAt && <p className="text-xs mt-2 text-primary">Next: {formatDate(f.nextFollowUpAt)}</p>}
                        </div>
                      ))}
                    </div>
                  )}
                </TabsContent>

                <TabsContent value="negotiations" className="m-0 space-y-4">
                  <NegotiationTracker
                    quotationId={detail.id}
                    negotiations={negotiations || []}
                    onUpdated={refreshNegotiations}
                  />
                </TabsContent>

                {/* What delivering this proposal actually takes, and what it
                    leaves. Mounted under the quotation's id so opening a second
                    proposal starts its plan clean. */}
                <TabsContent value="resources" className="m-0">
                  <QuotationResourcePlan
                    key={detail.id}
                    quotationId={detail.id}
                    quotationStatus={detail.status}
                  />
                </TabsContent>
              </ScrollArea>
              
              <div className="p-4 border-t bg-muted/20 flex justify-between items-center">
                <div className="flex gap-2">
                  <QuotationActions 
                    quotation={{ id: detail.id, quoteNumber: detail.quoteNumber, status: detail.status, approvalStatus: detail.approvalStatus, customerName: detail.customerName }} 
                    onChanged={refreshDetail} 
                  />
                  <Button variant="outline" size="sm" onClick={() => window.open(quoteBuilderApi.pdfUrl(detail.id), "_blank")}>
                    <Download className="size-3 mr-1.5" /> PDF
                  </Button>
                  {onEdit && (
                    <Button variant="outline" size="sm" disabled={detail.status === "Accepted"} onClick={() => onEdit(detail)}>
                      <Pencil className="size-3 mr-1.5" /> Edit
                    </Button>
                  )}
                </div>
              </div>
            </Tabs>
          </>
        )}
      </SheetContent>
    </Sheet>

    {detail && (
      <FollowUpDialog 
        open={followUpOpen} 
        onOpenChange={setFollowUpOpen} 
        quotationId={detail.id} 
        onSaved={refreshFollowUps} 
      />
    )}

    {detail && (
      <ExtendValidityDialog
        open={extendOpen}
        onOpenChange={setExtendOpen}
        quotationId={detail.id}
        currentValidUntil={detail.validUntil}
        onSaved={refreshDetail}
      />
    )}
    </>
  );
}
