"use client";

import * as React from "react";
import { ChevronLeft, Loader2, ArrowRight } from "lucide-react";
import Link from "next/link";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError } from "@/lib/api";
import { formatMoney } from "@/lib/crm-api";
import { quoteBuilderApi, type QuotationAnalytics } from "@/lib/inventory-api";
import { formatPercent } from "@/lib/inventory-api";

export default function QuotationAnalyticsPage() {
  const [data, setData] = React.useState<QuotationAnalytics | null>(null);
  const [loading, setLoading] = React.useState(true);

  React.useEffect(() => {
    let active = true;
    quoteBuilderApi.analytics()
      .then((res) => {
        if (active) setData(res);
      })
      .catch((err) => {
        toast.error("Failed to load analytics", {
          description: err instanceof ApiError ? err.message : "Network error"
        });
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => { active = false; };
  }, []);

  if (loading) {
    return (
      <div className="p-6 space-y-6">
        <div className="flex items-center gap-4 mb-6">
          <Skeleton className="h-8 w-8 rounded-md" />
          <Skeleton className="h-8 w-48" />
        </div>
        <div className="grid grid-cols-4 gap-4">
          <Skeleton className="h-24 rounded-lg" />
          <Skeleton className="h-24 rounded-lg" />
          <Skeleton className="h-24 rounded-lg" />
          <Skeleton className="h-24 rounded-lg" />
        </div>
        <Skeleton className="h-64 rounded-lg" />
        <Skeleton className="h-64 rounded-lg" />
      </div>
    );
  }

  if (!data) return null;

  const funnelMax = Math.max(
    data.funnel.draft, 
    data.funnel.sent, 
    data.funnel.underReview, 
    data.funnel.negotiation, 
    data.funnel.accepted + data.funnel.rejected
  );

  return (
    <div className="flex-1 overflow-auto bg-muted/10">
      <div className="flex items-center border-b bg-background px-6 h-14">
        <Button variant="ghost" size="icon" className="-ml-2 mr-2" asChild>
          <Link href="/dashboard/sales/quotations">
            <ChevronLeft className="size-4" />
          </Link>
        </Button>
        <h1 className="text-lg font-semibold">Quotation Analytics</h1>
      </div>

      <div className="p-6 max-w-6xl mx-auto space-y-6">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div className="p-4 bg-background border rounded-lg shadow-sm">
            <div className="text-sm text-muted-foreground mb-1">Conversion Rate</div>
            <div className="text-2xl font-semibold text-emerald-600 dark:text-emerald-400">
              {formatPercent(data.conversionRate)}
            </div>
            <div className="text-xs text-muted-foreground mt-1">
              {data.winLoss.won} won / {data.winLoss.won + data.winLoss.lost} closed
            </div>
          </div>
          <div className="p-4 bg-background border rounded-lg shadow-sm">
            <div className="text-sm text-muted-foreground mb-1">Avg Deal Size</div>
            <div className="text-2xl font-semibold text-primary">
              {formatMoney(data.avgDealSize)}
            </div>
          </div>
          <div className="p-4 bg-background border rounded-lg shadow-sm">
            <div className="text-sm text-muted-foreground mb-1">Time to Close</div>
            <div className="text-2xl font-semibold">
              {data.avgDaysToClose.toFixed(1)} <span className="text-sm font-normal text-muted-foreground">days</span>
            </div>
          </div>
          <div className="p-4 bg-background border rounded-lg shadow-sm">
            <div className="text-sm text-muted-foreground mb-1">Avg Validity</div>
            <div className="text-2xl font-semibold">
              {data.avgDaysToExpiry.toFixed(1)} <span className="text-sm font-normal text-muted-foreground">days</span>
            </div>
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <div className="lg:col-span-2 p-6 bg-background border rounded-lg shadow-sm">
            <h2 className="text-base font-semibold mb-6">Conversion Funnel</h2>
            <div className="space-y-4">
              {[
                { label: "Draft", value: data.funnel.draft, color: "bg-neutral-500" },
                { label: "Sent", value: data.funnel.sent, color: "bg-blue-500" },
                { label: "Under Review", value: data.funnel.underReview, color: "bg-indigo-500" },
                { label: "Negotiation", value: data.funnel.negotiation, color: "bg-amber-500" },
                { label: "Accepted", value: data.funnel.accepted, color: "bg-emerald-500" },
                { label: "Rejected/Expired", value: data.funnel.rejected + data.funnel.expired, color: "bg-red-500" },
              ].map(step => (
                <div key={step.label} className="flex items-center text-sm">
                  <div className="w-32 text-right pr-4 font-medium text-muted-foreground">{step.label}</div>
                  <div className="flex-1 flex items-center gap-3">
                    <div className="h-6 rounded-r bg-muted w-full relative">
                      <div 
                        className={`absolute left-0 top-0 h-full rounded-r ${step.color} opacity-80`} 
                        style={{ width: `${funnelMax > 0 ? (step.value / funnelMax) * 100 : 0}%` }}
                      />
                    </div>
                    <div className="w-12 font-medium">{step.value}</div>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="p-6 bg-background border rounded-lg shadow-sm">
            <h2 className="text-base font-semibold mb-4">Top Loss Reasons</h2>
            {data.winLoss.topLossReasons.length === 0 ? (
              <p className="text-sm text-muted-foreground">No lost quotes yet.</p>
            ) : (
              <div className="space-y-3">
                {data.winLoss.topLossReasons.map((lr, i) => (
                  <div key={i} className="flex justify-between text-sm">
                    <span className="truncate pr-2">{lr.reason}</span>
                    <span className="font-medium bg-muted px-2 py-0.5 rounded text-xs">{lr.count}</span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        <div className="p-6 bg-background border rounded-lg shadow-sm">
          <h2 className="text-base font-semibold mb-4">Monthly Trend</h2>
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead className="text-xs text-muted-foreground bg-muted/50">
                <tr>
                  <th className="px-4 py-3 font-medium rounded-l">Month</th>
                  <th className="px-4 py-3 font-medium">Count</th>
                  <th className="px-4 py-3 font-medium text-right">Value Quoted</th>
                  <th className="px-4 py-3 font-medium text-right rounded-r">Value Accepted</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {data.monthlyTrend.map((m, i) => (
                  <tr key={i}>
                    <td className="px-4 py-3 font-medium">{m.month}</td>
                    <td className="px-4 py-3">{m.count}</td>
                    <td className="px-4 py-3 text-right">{formatMoney(m.quoted)}</td>
                    <td className="px-4 py-3 text-right text-emerald-600">{formatMoney(m.accepted)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <div className="p-6 bg-background border rounded-lg shadow-sm">
            <h2 className="text-base font-semibold mb-4">Rep Performance</h2>
            <div className="overflow-x-auto">
              <table className="w-full text-sm text-left">
                <thead className="text-xs text-muted-foreground bg-muted/50">
                  <tr>
                    <th className="px-4 py-3 font-medium rounded-l">Rep</th>
                    <th className="px-4 py-3 font-medium text-center">Quoted</th>
                    <th className="px-4 py-3 font-medium text-center rounded-r">Accepted</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {data.repPerformance.map((r, i) => (
                    <tr key={i}>
                      <td className="px-4 py-3 font-medium">{r.repName}</td>
                      <td className="px-4 py-3 text-center">{r.quoted}</td>
                      <td className="px-4 py-3 text-center">{r.accepted}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div className="p-6 bg-background border rounded-lg shadow-sm">
            <h2 className="text-base font-semibold mb-4">Project Breakdown</h2>
            <div className="overflow-x-auto">
              <table className="w-full text-sm text-left">
                <thead className="text-xs text-muted-foreground bg-muted/50">
                  <tr>
                    <th className="px-4 py-3 font-medium rounded-l">Project</th>
                    <th className="px-4 py-3 font-medium text-center">Quoted</th>
                    <th className="px-4 py-3 font-medium text-center">Accepted</th>
                    <th className="px-4 py-3 font-medium text-right rounded-r">Avg Discount</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {data.projectBreakdown.map((p, i) => (
                    <tr key={i}>
                      <td className="px-4 py-3 font-medium">{p.project}</td>
                      <td className="px-4 py-3 text-center">{p.quoted}</td>
                      <td className="px-4 py-3 text-center">{p.accepted}</td>
                      <td className="px-4 py-3 text-right">{formatPercent(p.avgDiscount / 100, 2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
