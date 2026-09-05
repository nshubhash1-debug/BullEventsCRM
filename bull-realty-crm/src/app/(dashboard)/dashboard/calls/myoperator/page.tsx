"use client";

import * as React from "react";
import {
  ArrowUpRight,
  Clock,
  Gauge,
  Headphones,
  PhoneCall,
  PhoneMissed,
  Timer,
} from "lucide-react";

import { IntegrationNotice } from "@/components/crm/integration-notice";
import {
  DistributionBar,
  MetricStrip,
  MiniBars,
  Sparkline,
  TONE_FILL,
  type Metric,
} from "@/components/crm/metrics";
import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  callsApi,
  formatDuration,
  type CallAnalytics,
  type CallScorecard,
} from "@/lib/crm-api";
import { callOutcomeTone, dispositionTone, sentimentTone } from "@/lib/crm-tones";
import { cn } from "@/lib/utils";

const WINDOWS = [7, 30, 90] as const;

export default function MyOperatorDashboardPage() {
  const [days, setDays] = React.useState<number>(30);

  // Keyed by the window that produced them, so "loading" is derived rather
  // than a second piece of state set inside the effect.
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
        {
          label: "Total calls",
          value: analytics.totalCalls.toLocaleString(),
          icon: PhoneCall,
          spark: analytics.daily.map((point) => point.calls),
        },
        {
          label: "Connect rate",
          value: `${analytics.connectRate.toFixed(1)}%`,
          tone: analytics.connectRate >= 60 ? "success" : "warning",
          icon: Gauge,
          progress: analytics.connectRate / 100,
          hint: `${analytics.connected.toLocaleString()} of ${analytics.totalCalls.toLocaleString()} calls connected.`,
        },
        {
          label: "Talk time",
          value: `${Math.round(analytics.talkMinutes).toLocaleString()}m`,
          tone: "primary",
          icon: Timer,
          spark: analytics.daily.map((point) => point.talkMinutes),
        },
        {
          label: "Avg call",
          value: formatDuration(Math.round(analytics.averageCallSeconds)),
          tone: "info",
        },
        {
          label: "Avg wait",
          value: `${analytics.averageWaitSeconds.toFixed(0)}s`,
          tone: analytics.averageWaitSeconds > 20 ? "warning" : "success",
          icon: Clock,
        },
        {
          label: "Missed",
          value: analytics.missed.toLocaleString(),
          tone: analytics.missed > 0 ? "danger" : "success",
          icon: PhoneMissed,
        },
      ]
    : [];

  const busiestHour = analytics
    ? analytics.hourly.reduce(
        (best, point) => (point.calls > best.calls ? point : best),
        analytics.hourly[0] ?? { hour: 0, calls: 0, connected: 0 }
      )
    : null;

  return (
    <PagePanel
      icon={Headphones}
      title="MyOperator call dashboard"
      hint="Live telephony health: volume, connect rate, wait time and the hour-of-day pattern. Built over the call log, so it reads the same whether calls arrive from a dialler or are logged by hand."
      actions={
        <div className="flex items-center gap-1">
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
      }
    >
      <div className="flex flex-col gap-3">
        <IntegrationNotice
          provider="MyOperator"
          showing="No cloud-telephony account is linked, so these figures come from calls logged inside the CRM. Connecting MyOperator would add recordings, IVR routing and live agent status."
        />

        <MetricStrip metrics={metrics} loading={loading} />

        {loading || !analytics ? (
          <Skeleton className="h-64 w-full" />
        ) : (
          <>
            <div className="grid gap-3 lg:grid-cols-[1.4fr_1fr]">
              {/* Daily volume */}
              <div className="rounded border p-2.5">
                <div className="flex items-baseline justify-between">
                  <span className="text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
                    Daily volume · last {analytics.windowDays} days
                  </span>
                  <span className="text-[11px] text-muted-foreground tabular-nums">
                    peak{" "}
                    {Math.max(...analytics.daily.map((point) => point.calls)).toLocaleString()}
                  </span>
                </div>

                <MiniBars
                  values={analytics.daily.map((point) => point.calls)}
                  labels={analytics.daily.map((point) => point.date)}
                  tone="primary"
                  height={72}
                  className="mt-2"
                />

                <div className="mt-1 flex items-center justify-between text-[10.5px] text-muted-foreground">
                  <span>{analytics.daily[0]?.date}</span>
                  <span className="inline-flex items-center gap-1">
                    connected
                    <Sparkline
                      values={analytics.daily.map((point) => point.connected)}
                      tone="success"
                      width={70}
                      height={12}
                    />
                  </span>
                  <span>{analytics.daily.at(-1)?.date}</span>
                </div>
              </div>

              {/* Hour of day */}
              <div className="rounded border p-2.5">
                <div className="flex items-baseline justify-between">
                  <span className="text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
                    Hour of day
                  </span>
                  {busiestHour ? (
                    <span className="text-[11px] text-muted-foreground tabular-nums">
                      busiest {busiestHour.hour.toString().padStart(2, "0")}:00
                    </span>
                  ) : null}
                </div>

                <MiniBars
                  values={analytics.hourly.map((point) => point.calls)}
                  labels={analytics.hourly.map(
                    (point) => `${point.hour.toString().padStart(2, "0")}:00`
                  )}
                  tone="violet"
                  height={72}
                  className="mt-2"
                />

                <div className="mt-1 flex justify-between text-[10.5px] text-muted-foreground tabular-nums">
                  <span>00:00</span>
                  <span>12:00</span>
                  <span>23:00</span>
                </div>
              </div>
            </div>

            <div className="grid gap-3 lg:grid-cols-3">
              <DistributionBar
                title="Direction"
                segments={[
                  { key: "out", label: "Outbound", count: analytics.outbound, tone: "primary" },
                  { key: "in", label: "Inbound", count: analytics.inbound, tone: "info" },
                  { key: "missed", label: "Missed", count: analytics.missed, tone: "danger" },
                ]}
              />
              <DistributionBar
                title="Outcome"
                segments={analytics.byOutcome.map((bucket) => ({
                  key: bucket.key,
                  label: bucket.label,
                  count: bucket.count,
                  tone: callOutcomeTone(bucket.key),
                }))}
              />
              <DistributionBar
                title="Disposition"
                segments={analytics.byDisposition.map((bucket) => ({
                  key: bucket.key,
                  label: bucket.label,
                  count: bucket.count,
                  tone: dispositionTone(bucket.key),
                }))}
              />
            </div>

            {/* Agent board */}
            <div className="rounded border">
              <div className="border-b px-2.5 py-1.5 text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
                Agent activity
              </div>
              <div className="flex flex-col divide-y">
                {scorecard.map((agent) => (
                  <div
                    key={agent.agentName}
                    className="flex flex-wrap items-center gap-3 px-2.5 py-1.5 text-[12px]"
                  >
                    <span className="w-36 truncate font-medium">{agent.agentName}</span>

                    <span className="flex items-center gap-1 text-muted-foreground">
                      <ArrowUpRight className="size-3" />
                      <span className="w-9 text-right tabular-nums">{agent.calls}</span>
                    </span>

                    <Tooltip>
                      <TooltipTrigger asChild>
                        <span className="h-2 min-w-24 flex-1 overflow-hidden rounded-full bg-muted">
                          <span
                            className={cn("block h-full", TONE_FILL.success)}
                            style={{ width: `${agent.connectRate}%` }}
                          />
                        </span>
                      </TooltipTrigger>
                      <TooltipContent className="text-[11px]">
                        {agent.connected} connected · {agent.progressed} moved a record
                        forward
                      </TooltipContent>
                    </Tooltip>

                    <span className="w-12 text-right tabular-nums">
                      {agent.connectRate.toFixed(0)}%
                    </span>
                    <span className="w-16 text-right tabular-nums text-muted-foreground">
                      {agent.talkMinutes.toFixed(0)}m
                    </span>
                    <span className="w-16 text-right tabular-nums text-muted-foreground">
                      {formatDuration(Math.round(agent.averageCallSeconds))}
                    </span>
                  </div>
                ))}

                {scorecard.length === 0 ? (
                  <p className="px-2.5 py-6 text-center text-[12.5px] text-muted-foreground">
                    No calls logged in this window.
                  </p>
                ) : null}
              </div>
            </div>

            {analytics.bySentiment.length > 0 ? (
              <DistributionBar
                title="Note sentiment · scored locally"
                segments={analytics.bySentiment.map((bucket) => ({
                  key: bucket.key,
                  label: bucket.label,
                  count: bucket.count,
                  tone: sentimentTone(bucket.key),
                }))}
              />
            ) : null}
          </>
        )}
      </div>
    </PagePanel>
  );
}
