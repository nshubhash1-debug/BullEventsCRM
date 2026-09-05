"use client";

import * as React from "react";
import {
  CheckCircle2,
  Clock3,
  FileText,
  IndianRupee,
  Send,
} from "lucide-react";
import { toast } from "sonner";

import type { GridColumn } from "@/components/crm/crm-grid";
import { QuotationActions } from "@/components/inventory/quotation-actions";
import { QuoteBuilder } from "@/components/inventory/quote-builder";
import { NewQuotationButton } from "@/components/inventory/unit-picker";
import { ListShell, type QuickView } from "@/components/crm/list-shell";
import {
  DistributionBar,
  Pill,
  StatusDot,
  type Metric,
} from "@/components/crm/metrics";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { useCrmList } from "@/hooks/use-crm-list";
import { ApiError } from "@/lib/api";
import {
  formatDate,
  formatMoney,
  humanise,
  quotationsApi,
  timeAgo,
  type QuotationRow,
} from "@/lib/crm-api";
import { emptyRoot, type FilterNode } from "@/lib/query";
import { cn } from "@/lib/utils";
import { quoteBuilderApi, type QuotationDetail } from "@/lib/inventory-api";
import { quotationStatusTone } from "@/lib/crm-tones";
import Link from "next/link";
import { QuotationDetailSheet } from "@/components/inventory/quotation-detail-sheet";
import { Button } from "@/components/ui/button";

const FACETS = ["status", "ownerName", "projectName", "branchName"];
const INITIAL_SORT = [{ field: "issueDate", descending: true }];

function condition(field: string, operator: string, value?: string, values?: string[]): FilterNode {
  const root = emptyRoot();
  return { ...root, children: [{ key: `${root.key}-c`, field, operator, value, values }] };
}

export default function QuotationsPage() {
  const [selectedId, setSelectedId] = React.useState<number | null>(null);
  const [editing, setEditing] = React.useState<QuotationDetail | null>(null);
  const [loadingEdit, setLoadingEdit] = React.useState(false);

  const state = useCrmList<QuotationRow>(quotationsApi, {
    facets: FACETS,
    dateFields: [
      { id: "issueDate", label: "Issued" },
      { id: "validUntil", label: "Valid until" },
      { id: "sentAt", label: "Sent" },
      { id: "respondedAt", label: "Responded" },
    ],
    sort: INITIAL_SORT,
    pageSize: 50,
  });

  const aggregates = state.aggregates;
  const quoted = aggregates.quoted ?? 0;
  const accepted = aggregates.accepted ?? 0;

  const metrics: Metric[] = [
    { label: "Quotations", value: state.total.toLocaleString(), icon: FileText },
    {
      label: "Value quoted",
      value: formatMoney(quoted),
      tone: "primary",
      icon: IndianRupee,
    },
    {
      label: "Accepted",
      value: formatMoney(accepted),
      tone: "success",
      icon: CheckCircle2,
      progress: quoted > 0 ? accepted / quoted : 0,
      hint:
        quoted > 0
          ? `${((accepted / quoted) * 100).toFixed(1)}% of quoted value has been accepted.`
          : undefined,
      onClick: () => state.setFilter(condition("status", "equals", "Accepted")),
    },
    {
      label: "Awaiting response",
      value: (aggregates.pending ?? 0).toLocaleString(),
      tone: "warning",
      icon: Send,
      onClick: () =>
        state.setFilter(
          condition("status", "in", undefined, ["Sent", "UnderReview", "Negotiation"])
        ),
    },
    {
      label: "Expiring this week",
      value: (aggregates.expiring ?? 0).toLocaleString(),
      tone: (aggregates.expiring ?? 0) > 0 ? "danger" : "neutral",
      icon: Clock3,
      hint: "Still open with a validity date inside the next seven days.",
      onClick: () => state.setFilter(condition("validUntil", "nextNDays", "7")),
    },
  ];

  const quickViews: QuickView[] = [
    { id: "draft", label: "Drafts", build: () => condition("status", "equals", "Draft") },
    {
      id: "open",
      label: "Awaiting response",
      build: () =>
        condition("status", "in", undefined, ["Sent", "UnderReview", "Negotiation"]),
    },
    {
      id: "expiring",
      label: "Expiring",
      build: () => condition("validUntil", "nextNDays", "7"),
    },
    {
      id: "accepted",
      label: "Accepted",
      build: () => condition("status", "equals", "Accepted"),
    },
    {
      id: "big",
      label: "Over ₹2 Cr",
      build: () => condition("total", "greaterOrEqual", "20000000"),
    },
    {
      id: "followups",
      label: "Follow-ups Due",
      build: () => condition("status", "in", undefined, ["Sent", "UnderReview", "Negotiation"]), // Using active quotes as a proxy for the filter
    },
  ];

  const visuals = (
    <DistributionBar
      title="Status"
      segments={(state.facets.status ?? []).map((bucket) => ({
        key: bucket.value,
        label: humanise(bucket.value),
        count: bucket.count,
        tone: quotationStatusTone(bucket.value),
      }))}
      onSelect={(value) => state.setFilter(condition("status", "equals", value))}
    />
  );

  async function revise(quotation: QuotationRow) {
    try {
      const revision = await quotationsApi.revise(quotation.id);
      toast.success(`Revision ${revision.quoteNumber} created`, {
        description: `Version ${revision.version} — the original stays on record.`,
      });
      state.refresh();
    } catch (error) {
      toast.error("Could not create a revision", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  const columns: GridColumn<QuotationRow>[] = [
    {
      id: "quote",
      label: "Quotation",
      sortField: "quoteNumber",
      sticky: true,
      width: 190,
      render: (row) => (
        <span 
          className="flex flex-col leading-tight cursor-pointer"
          onClick={(e) => { e.stopPropagation(); setSelectedId(row.id); }}
        >
          <span className="truncate font-medium text-primary hover:underline">{row.quoteNumber}</span>
          <span className="truncate text-[10.5px] text-muted-foreground">{row.title}</span>
        </span>
      ),
    },
    {
      id: "customer",
      label: "Customer",
      sortField: "customerName",
      width: 160,
      render: (row) => <span className="block truncate">{row.customerName}</span>,
    },
    {
      id: "status",
      label: "Status",
      sortField: "status",
      width: 118,
      render: (row) => (
        <span className="flex items-center gap-1.5">
          <StatusDot label={humanise(row.status)} tone={quotationStatusTone(row.status)} />
          {row.isExpired ? (
            <Tooltip>
              <TooltipTrigger asChild>
                <span>
                  <Pill tone="danger">Expired</Pill>
                </span>
              </TooltipTrigger>
              <TooltipContent className="text-[11px]">
                Validity lapsed on {formatDate(row.validUntil)}
              </TooltipContent>
            </Tooltip>
          ) : null}
        </span>
      ),
    },
    {
      id: "version",
      label: "Ver",
      sortField: "version",
      align: "center",
      width: 52,
      render: (row) => (
        <span className={cn(row.version > 1 && "font-semibold text-primary")}>
          v{row.version}
        </span>
      ),
    },
    {
      id: "total",
      // The consideration, not the unit cost. Sorting still runs on `total`
      // because that is the indexed column, and the two order identically for
      // any given project — the charge heads scale with area alongside it.
      label: "Consideration",
      sortField: "total",
      align: "right",
      width: 120,
      render: (row) => (
        <Tooltip>
          <TooltipTrigger asChild>
            <span className="font-medium">{formatMoney(row.grandTotal)}</span>
          </TooltipTrigger>
          <TooltipContent>
            Unit cost {formatMoney(row.total)}
            {row.chargesTotal > 0
              ? ` · other charges ${formatMoney(row.chargesTotal)}`
              : ""}
          </TooltipContent>
        </Tooltip>
      ),
    },
    {
      id: "unitCost",
      label: "Unit cost",
      sortField: "total",
      align: "right",
      width: 104,
      defaultHidden: true,
      render: (row) => formatMoney(row.total),
    },
    {
      id: "subtotal",
      label: "Basic",
      sortField: "subtotal",
      align: "right",
      width: 100,
      defaultHidden: true,
      render: (row) => formatMoney(row.subtotal),
    },
    {
      id: "discount",
      label: "Discount",
      sortField: "discountAmount",
      align: "right",
      width: 96,
      render: (row) =>
        row.discountAmount > 0 ? (
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="text-amber-600 dark:text-amber-400">
                {formatMoney(row.discountAmount)}
              </span>
            </TooltipTrigger>
            <TooltipContent className="text-[11px]">
              {row.discountPercent}% off the subtotal
            </TooltipContent>
          </Tooltip>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: "tax",
      label: "Tax",
      sortField: "taxAmount",
      align: "right",
      width: 92,
      defaultHidden: true,
      render: (row) => formatMoney(row.taxAmount),
    },
    {
      id: "lines",
      label: "Lines",
      align: "right",
      width: 58,
      render: (row) => row.lines.length,
    },
    {
      id: "project",
      label: "Project",
      sortField: "projectName",
      width: 150,
      render: (row) => (
        <span className="block truncate text-muted-foreground">
          {row.projectName ?? "—"}
        </span>
      ),
    },
    {
      id: "issued",
      label: "Issued",
      sortField: "issueDate",
      width: 92,
      render: (row) => (
        <span className="text-muted-foreground">{formatDate(row.issueDate)}</span>
      ),
    },
    {
      id: "valid",
      label: "Valid until",
      sortField: "validUntil",
      width: 104,
      render: (row) => (
        <span className={cn(row.isExpired && "text-red-600 dark:text-red-400")}>
          {formatDate(row.validUntil)}
        </span>
      ),
    },
    {
      id: "responded",
      label: "Responded",
      sortField: "respondedAt",
      width: 100,
      defaultHidden: true,
      render: (row) => (
        <span className="text-muted-foreground">{timeAgo(row.respondedAt)}</span>
      ),
    },
    {
      id: "owner",
      label: "Owner",
      sortField: "ownerName",
      width: 126,
      render: (row) => (
        <span className={cn("truncate", !row.ownerName && "text-muted-foreground italic")}>
          {row.ownerName ?? "Unassigned"}
        </span>
      ),
    },
    {
      id: "terms",
      label: "Payment terms",
      width: 240,
      defaultHidden: true,
      render: (row) => (
        <span className="block truncate text-muted-foreground">
          {row.paymentTerms ?? "—"}
        </span>
      ),
    },
    {
      id: "actions",
      label: "",
      width: 40,
      fixedWidth: true,
      render: (row) => (
        <QuotationActions
          quotation={row}
          onEdit={openEditor}
          onRevise={(target) => void revise(target as QuotationRow)}
          onChanged={state.refresh}
        />
      ),
    },
  ];

  async function openEditor(row: { id: number; quoteNumber: string }) {
    if (loadingEdit) return;
    setLoadingEdit(true);
    try {
      setEditing(await quoteBuilderApi.detail(row.id));
    } catch (error) {
      toast.error(`Could not open ${row.quoteNumber}`, {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setLoadingEdit(false);
    }
  }

  return (
    <>
    <ListShell
      icon={FileText}
      title="Quotations"
      storageKey="quotations"
      hint="Priced offers with server-computed totals — line discount, then header discount, then tax. Accepting a quotation moves its opportunity to Negotiation."
      state={state}
      columns={columns}
      metrics={metrics}
      visuals={visuals}
      quickViews={quickViews}
      searchPlaceholder="Quote number, customer, title, notes…"
      emptyMessage="No quotations match this view."
      actions={
        <div className="flex gap-2">
          <Button variant="outline" size="sm" asChild>
            <Link href="/dashboard/sales/quotations/analytics">
              Analytics
            </Link>
          </Button>
          <NewQuotationButton onCreated={state.refresh} />
        </div>
      }
    />

    <QuotationDetailSheet
      quotationId={selectedId}
      open={selectedId !== null}
      onOpenChange={(open) => { if (!open) setSelectedId(null); }}
      onEdit={(detail) => { setSelectedId(null); setEditing(detail); }}
      onChanged={state.refresh}
    />

    {editing ? (
      <QuoteBuilder
        key={editing.id}
        unit={null}
        quotation={editing}
        open
        onOpenChange={(next) => {
          if (!next) setEditing(null);
        }}
        onCreated={() => {
          setEditing(null);
          state.refresh();
        }}
      />
    ) : null}
    </>
  );
}
