"use client";

import * as React from "react";
import { Download, FileBarChart } from "lucide-react";
import { toast } from "sonner";

import { IntegrationNotice } from "@/components/crm/integration-notice";
import { MetricStrip, Pill, TONE_FILL, type Metric } from "@/components/crm/metrics";
import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { ScrollArea, ScrollBar } from "@/components/ui/scroll-area";
import { Skeleton } from "@/components/ui/skeleton";
import {
  callsApi,
  formatDuration,
  type CallAnalytics,
  type CallScorecard,
} from "@/lib/crm-api";
import { cn } from "@/lib/utils";

const WINDOWS = [7, 30, 90] as const;
type ReportMode = "agent" | "daily";

/** RFC 4180 quoting — a comma or quote inside a cell must not break the file. */
function toCsv(headers: string[], rows: (string | number)[][]) {
  const escape = (cell: string | number) => {
    const text = String(cell);
    return /[",\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
  };

  return [headers, ...rows].map((row) => row.map(escape).join(",")).join("\r\n");
}

function download(filename: string, csv: string) {
  // A BOM keeps Excel from mangling the rupee sign and Indian names.
  const blob = new Blob([`﻿${csv}`], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);

  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  anchor.click();

  URL.revokeObjectURL(url);
}

export default function MyOperatorReportPage() {
  const [days, setDays] = React.useState<number>(30);
  const [mode, setMode] = React.useState<ReportMode>("agent");

  const [loaded, setLoaded] = React.useState<{
    key: number;
    analytics: CallAnalytics;
    scorecard: CallScorecard[];
  } | null>(null);

  React.useEffect(() => {
    let cancelled = false;

    Promise.all([callsApi.analytics(days), callsApi.scorecard(days)])
      .then(([analytics, scorecard]) => {
        if (!cancelled) setLoaded({ key: days, analytics, scorecard });
      })
      .catch(() => {
        if (!cancelled) setLoaded(null);
      });

    return () => {
      cancelled = true;
    };
  }, [days]);

  const analytics = loaded?.analytics ?? null;
  const scorecard = loaded?.scorecard ?? [];
  const loading = loaded === null || loaded.key !== days;

  const metrics: Metric[] = analytics
    ? [
        { label: "Calls", value: analytics.totalCalls.toLocaleString() },
        {
          label: "Connected",
          value: analytics.connected.toLocaleString(),
          tone: "success",
        },
        {
          label: "Connect rate",
          value: `${analytics.connectRate.toFixed(1)}%`,
          tone: analytics.connectRate >= 60 ? "success" : "warning",
        },
        {
          label: "Talk time",
          value: `${Math.round(analytics.talkMinutes).toLocaleString()}m`,
          tone: "primary",
        },
        {
          label: "Agents active",
          value: scorecard.length.toLocaleString(),
          tone: "info",
        },
      ]
    : [];

  function exportCsv() {
    if (!analytics) return;

    const stamp = new Date().toISOString().slice(0, 10);

    if (mode === "agent") {
      download(
        `call-report-by-agent-${days}d-${stamp}.csv`,
        toCsv(
          [
            "Agent",
            "Calls",
            "Connected",
            "Connect rate %",
            "Talk minutes",
            "Avg call seconds",
            "Progressed",
            "Progression rate %",
            "Positive sentiment",
          ],
          scorecard.map((agent) => [
            agent.agentName,
            agent.calls,
            agent.connected,
            agent.connectRate,
            agent.talkMinutes,
            agent.averageCallSeconds,
            agent.progressed,
            agent.progressionRate,
            agent.positiveSentiment,
          ])
        )
      );
    } else {
      download(
        `call-report-by-day-${days}d-${stamp}.csv`,
        toCsv(
          ["Date", "Calls", "Connected", "Connect rate %", "Talk minutes"],
          analytics.daily.map((point) => [
            point.date,
            point.calls,
            point.connected,
            point.calls === 0 ? 0 : Number(((point.connected / point.calls) * 100).toFixed(1)),
            point.talkMinutes,
          ])
        )
      );
    }

    toast.success("Report exported", {
      description: `${mode === "agent" ? scorecard.length : analytics.daily.length} rows written to CSV.`,
    });
  }

  const maxDailyCalls = analytics
    ? Math.max(1, ...analytics.daily.map((point) => point.calls))
    : 1;

  return (
    <PagePanel
      flush
      icon={FileBarChart}
      title="MyOperator call report"
      hint="The periodic call report: per-agent productivity or per-day volume, exportable to CSV for payroll, incentive and review cycles."
      actions={
        <>
          <Button
            variant="outline"
            size="sm"
            className="h-8"
            disabled={loading || !analytics}
            onClick={exportCsv}
          >
            <Download /> Export CSV
          </Button>
        </>
      }
      toolbar={
        <>
          <div className="flex items-center gap-1">
            {(["agent", "daily"] as const).map((option) => (
              <button
                key={option}
                type="button"
                onClick={() => setMode(option)}
                className={cn(
                  "h-8 rounded px-2.5 text-[12.5px] transition-colors",
                  mode === option
                    ? "bg-primary font-medium text-primary-foreground"
                    : "border text-foreground/75 hover:bg-accent"
                )}
              >
                {option === "agent" ? "By agent" : "By day"}
              </button>
            ))}
          </div>

          <div className="ml-auto flex items-center gap-1">
            {WINDOWS.map((window) => (
              <Button
                key={window}
                variant={days === window ? "secondary" : "outline"}
                size="sm"
                className="h-8 px-2.5 text-[12px]"
                onClick={() => setDays(window)}
              >
                {window}d
              </Button>
            ))}
          </div>
        </>
      }
    >
      <div className="flex min-h-0 flex-1 flex-col">
        <div className="flex flex-col gap-2.5 border-b p-2.5">
          <IntegrationNotice
            provider="MyOperator"
            showing="Figures are compiled from calls logged in the CRM. A connected account would add carrier-side records for calls that never reached an agent."
          />
          <MetricStrip metrics={metrics} loading={loading} />
        </div>

        {loading || !analytics ? (
          <div className="flex flex-col gap-1.5 p-3">
            {Array.from({ length: 10 }, (_, i) => (
              <Skeleton key={i} className="h-7 w-full" />
            ))}
          </div>
        ) : (
          <ScrollArea className="w-full flex-1">
            <table className="w-full caption-bottom border-separate border-spacing-0 text-[12px]">
              <thead className="sticky top-0 z-10 bg-muted/70 backdrop-blur">
                {mode === "agent" ? (
                  <tr>
                    {[
                      "Agent",
                      "Calls",
                      "Connected",
                      "Connect rate",
                      "Talk time",
                      "Avg call",
                      "Progressed",
                      "Progression",
                      "Positive tone",
                    ].map((header, index) => (
                      <th
                        key={header}
                        className={cn(
                          "h-8 border-r border-b px-2.5 text-[11.5px] font-medium whitespace-nowrap text-muted-foreground last:border-r-0",
                          index > 0 && "text-right"
                        )}
                      >
                        {header}
                      </th>
                    ))}
                  </tr>
                ) : (
                  <tr>
                    {["Date", "Calls", "Connected", "Connect rate", "Talk time", "Volume"].map(
                      (header, index) => (
                        <th
                          key={header}
                          className={cn(
                            "h-8 border-r border-b px-2.5 text-[11.5px] font-medium whitespace-nowrap text-muted-foreground last:border-r-0",
                            index > 0 && index < 5 && "text-right"
                          )}
                        >
                          {header}
                        </th>
                      )
                    )}
                  </tr>
                )}
              </thead>

              <tbody>
                {mode === "agent"
                  ? scorecard.map((agent) => (
                      <tr key={agent.agentName} className="hover:bg-muted/40">
                        <td className="h-8 border-r border-b px-2.5 font-medium whitespace-nowrap">
                          {agent.agentName}
                        </td>
                        <td className="h-8 border-r border-b px-2.5 text-right tabular-nums">
                          {agent.calls.toLocaleString()}
                        </td>
                        <td className="h-8 border-r border-b px-2.5 text-right tabular-nums">
                          {agent.connected.toLocaleString()}
                        </td>
                        <td className="h-8 border-r border-b px-2.5 text-right tabular-nums">
                          <Pill
                            tone={
                              agent.connectRate >= 65
                                ? "success"
                                : agent.connectRate >= 45
                                  ? "warning"
                                  : "danger"
                            }
                          >
                            {agent.connectRate.toFixed(1)}%
                          </Pill>
                        </td>
                        <td className="h-8 border-r border-b px-2.5 text-right tabular-nums">
                          {agent.talkMinutes.toFixed(0)}m
                        </td>
                        <td className="h-8 border-r border-b px-2.5 text-right tabular-nums">
                          {formatDuration(Math.round(agent.averageCallSeconds))}
                        </td>
                        <td className="h-8 border-r border-b px-2.5 text-right tabular-nums">
                          {agent.progressed.toLocaleString()}
                        </td>
                        <td className="h-8 border-r border-b px-2.5 text-right tabular-nums">
                          {agent.progressionRate.toFixed(1)}%
                        </td>
                        <td className="h-8 border-b px-2.5 text-right tabular-nums">
                          {agent.positiveSentiment.toLocaleString()}
                        </td>
                      </tr>
                    ))
                  : analytics.daily.map((point) => {
                      const rate =
                        point.calls === 0 ? 0 : (point.connected / point.calls) * 100;

                      return (
                        <tr key={point.date} className="hover:bg-muted/40">
                          <td className="h-8 border-r border-b px-2.5 whitespace-nowrap tabular-nums">
                            {point.date}
                          </td>
                          <td className="h-8 border-r border-b px-2.5 text-right tabular-nums">
                            {point.calls.toLocaleString()}
                          </td>
                          <td className="h-8 border-r border-b px-2.5 text-right tabular-nums">
                            {point.connected.toLocaleString()}
                          </td>
                          <td className="h-8 border-r border-b px-2.5 text-right tabular-nums">
                            {rate.toFixed(1)}%
                          </td>
                          <td className="h-8 border-r border-b px-2.5 text-right tabular-nums">
                            {point.talkMinutes.toFixed(0)}m
                          </td>
                          <td className="h-8 border-b px-2.5">
                            <span className="block h-1.5 w-full overflow-hidden rounded-full bg-muted">
                              <span
                                className={cn("block h-full", TONE_FILL.primary)}
                                style={{ width: `${(point.calls / maxDailyCalls) * 100}%` }}
                              />
                            </span>
                          </td>
                        </tr>
                      );
                    })}
              </tbody>
            </table>
            <ScrollBar orientation="horizontal" />
          </ScrollArea>
        )}
      </div>
    </PagePanel>
  );
}
