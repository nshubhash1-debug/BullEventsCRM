"use client";

import * as React from "react";
import Link from "next/link";
import {
  BarChart3,
  Brain,
  CalendarCheck2,
  Handshake,
  PhoneCall,
  Route,
  TrendingDown,
  TrendingUp,
  Waypoints,
} from "lucide-react";

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
  formatMoney,
  intelligenceApi,
  obmVisitsApi,
  opportunitiesApi,
  siteVisitsApi,
  type AnomalyReport,
  type CallAnalytics,
  type FieldProductivity,
  type ForecastResult,
  type PipelineBoard,
  type SiteVisitInsights,
} from "@/lib/crm-api";
import { opportunityStageTone } from "@/lib/crm-tones";
import { cn } from "@/lib/utils";

interface ReportsData {
  pipeline: PipelineBoard;
  forecast: ForecastResult;
  leadForecast: ForecastResult;
  calls: CallAnalytics;
  visits: SiteVisitInsights;
  field: FieldProductivity[];
  anomalies: AnomalyReport;
}

/** A titled panel — every report on this page sits in one. */
function ReportCard({
  title,
  subtitle,
  href,
  linkLabel,
  children,
  className,
}: {
  title: string;
  subtitle?: string;
  href?: string;
  linkLabel?: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <section className={cn("flex min-w-0 flex-col rounded border", className)}>
      <header className="flex items-baseline justify-between gap-2 border-b px-2.5 py-1.5">
        <div className="min-w-0">
          <h2 className="truncate text-[12.5px] font-semibold">{title}</h2>
          {subtitle ? (
            <p className="truncate text-[10.5px] text-muted-foreground">{subtitle}</p>
          ) : null}
        </div>
        {href ? (
          <Button asChild variant="ghost" size="sm" className="h-6 shrink-0 text-[11px]">
            <Link href={href}>{linkLabel ?? "Open"}</Link>
          </Button>
        ) : null}
      </header>
      <div className="min-w-0 flex-1 p-2.5">{children}</div>
    </section>
  );
}

export default function ReportsPage() {
  const [data, setData] = React.useState<ReportsData | null>(null);
  const [failed, setFailed] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;

    Promise.all([
      opportunitiesApi.pipeline({}),
      intelligenceApi.forecast("revenue", 3),
      intelligenceApi.forecast("leads", 3),
      callsApi.analytics(30),
      siteVisitsApi.insights(90),
      obmVisitsApi.productivity(60),
      intelligenceApi.anomalies("leads", 90),
    ])
      .then(([pipeline, forecast, leadForecast, calls, visits, field, anomalies]) => {
        if (!cancelled) {
          setData({ pipeline, forecast, leadForecast, calls, visits, field, anomalies });
        }
      })
      .catch(() => {
        if (!cancelled) setFailed(true);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const metrics: Metric[] = data
    ? [
        {
          label: "Open pipeline",
          value: formatMoney(data.pipeline.openValue),
          tone: "primary",
          icon: Handshake,
        },
        {
          label: "Weighted",
          value: formatMoney(data.pipeline.weightedValue),
          tone: "violet",
          progress:
            data.pipeline.openValue > 0
              ? data.pipeline.weightedValue / data.pipeline.openValue
              : 0,
        },
        {
          label: "Win rate",
          value: `${data.pipeline.winRate.toFixed(1)}%`,
          tone: data.pipeline.winRate >= 50 ? "success" : "warning",
          progress: data.pipeline.winRate / 100,
        },
        {
          label: "Next month revenue",
          value: formatMoney(data.forecast.nextPeriodValue),
          tone: "info",
          icon: TrendingUp,
          spark: data.forecast.series.slice(-12).map((point) => Number(point.value)),
          hint: data.forecast.message,
        },
        {
          label: "Calls · 30d",
          value: data.calls.totalCalls.toLocaleString(),
          icon: PhoneCall,
          spark: data.calls.daily.map((point) => point.calls),
        },
        {
          label: "Visit completion",
          value: `${data.visits.completionRate.toFixed(0)}%`,
          tone: data.visits.completionRate >= 60 ? "success" : "warning",
          icon: CalendarCheck2,
          progress: data.visits.completionRate / 100,
        },
      ]
    : [];

  if (failed) {
    return (
      <PagePanel icon={BarChart3} title="Reports">
        <p className="py-10 text-center text-[13px] text-muted-foreground">
          Could not load the reporting data. Check that the API is running and try again.
        </p>
      </PagePanel>
    );
  }

  return (
    <PagePanel
      icon={BarChart3}
      title="Reports"
      hint="Cross-module reporting built on the same aggregates the list views use, so a number here always agrees with the number on the screen it came from."
    >
      <div className="flex flex-col gap-3">
        <MetricStrip metrics={metrics} loading={!data} />

        {!data ? (
          <div className="grid gap-3 lg:grid-cols-2">
            {Array.from({ length: 4 }, (_, i) => (
              <Skeleton key={i} className="h-56 w-full" />
            ))}
          </div>
        ) : (
          <div className="grid gap-3 lg:grid-cols-2">
            {/* ---- Revenue forecast ---- */}
            <ReportCard
              title="Revenue forecast"
              subtitle={data.forecast.message}
              href="/dashboard/leads/opportunities"
              linkLabel="Opportunities"
            >
              <Sparkline
                values={data.forecast.series.map((point) => Number(point.value))}
                tone="primary"
                width={520}
                height={70}
                className="w-full"
              />
              <div className="mt-1 flex items-center justify-between text-[10.5px] text-muted-foreground tabular-nums">
                <span>{data.forecast.series[0]?.period}</span>
                <span className="inline-flex items-center gap-1">
                  <Brain className="size-3" />
                  {data.forecast.engine}
                </span>
                <span>{data.forecast.series.at(-1)?.period}</span>
              </div>
              <p className="mt-1.5 text-[11.5px]">
                Next three months:{" "}
                <strong className="tabular-nums">
                  {formatMoney(data.forecast.horizonTotal)}
                </strong>
              </p>
            </ReportCard>

            {/* ---- Pipeline by stage ---- */}
            <ReportCard
              title="Pipeline by stage"
              subtitle={`${data.pipeline.totalDeals} deals · avg age ${data.pipeline.averageAgeDays.toFixed(0)} days`}
              href="/dashboard/leads/pipeline"
              linkLabel="Board"
            >
              <div className="flex flex-col gap-1">
                {data.pipeline.columns.map((column) => {
                  const max = Math.max(
                    1,
                    ...data.pipeline.columns.map((entry) => entry.value)
                  );

                  return (
                    <Tooltip key={column.stage}>
                      <TooltipTrigger asChild>
                        <div className="flex items-center gap-2 text-[11.5px]">
                          <span className="w-28 truncate">{column.label}</span>
                          <span className="w-8 text-right tabular-nums text-muted-foreground">
                            {column.count}
                          </span>
                          <span className="h-2.5 min-w-0 flex-1 overflow-hidden rounded-full bg-muted">
                            <span
                              className={cn(
                                "block h-full",
                                TONE_FILL[opportunityStageTone(column.stage)]
                              )}
                              style={{ width: `${(column.value / max) * 100}%` }}
                            />
                          </span>
                          <span className="w-16 text-right tabular-nums">
                            {formatMoney(column.value)}
                          </span>
                        </div>
                      </TooltipTrigger>
                      <TooltipContent className="text-[11px]">
                        {formatMoney(column.weightedValue)} weighted · avg{" "}
                        {column.averageDaysInStage.toFixed(0)} days in stage
                      </TooltipContent>
                    </Tooltip>
                  );
                })}
              </div>
            </ReportCard>

            {/* ---- Lead intake ---- */}
            <ReportCard
              title="Lead intake"
              subtitle={data.leadForecast.message}
              href="/dashboard/leads"
              linkLabel="Leads"
            >
              <MiniBars
                values={data.leadForecast.series.map((point) => Number(point.value))}
                labels={data.leadForecast.series.map((point) => point.period)}
                tone="violet"
                height={70}
              />
              <p className="mt-1.5 text-[11.5px] text-muted-foreground">
                Next month:{" "}
                <strong className="text-foreground tabular-nums">
                  {Math.round(Number(data.leadForecast.nextPeriodValue)).toLocaleString()}
                </strong>{" "}
                leads projected
              </p>
            </ReportCard>

            {/* ---- Anomalies ---- */}
            <ReportCard
              title="Unusual movement"
              subtitle={data.anomalies.message}
            >
              <MiniBars
                values={data.anomalies.points.map((point) => Number(point.value))}
                labels={data.anomalies.points.map((point) => point.period)}
                tone="info"
                height={54}
              />

              <div className="mt-2 flex flex-col gap-0.5">
                {data.anomalies.points
                  .filter((point) => point.isAnomaly)
                  .slice(-4)
                  .map((point) => (
                    <div
                      key={point.period}
                      className="flex items-center gap-1.5 text-[11px]"
                    >
                      {Number(point.value) >= 0 ? (
                        <TrendingUp className="size-3 shrink-0 text-amber-500" />
                      ) : (
                        <TrendingDown className="size-3 shrink-0 text-red-500" />
                      )}
                      <span className="w-20 shrink-0 tabular-nums">{point.period}</span>
                      <span className="truncate text-muted-foreground">{point.note}</span>
                    </div>
                  ))}

                {data.anomalies.anomalyCount === 0 ? (
                  <p className="text-[11.5px] text-muted-foreground">
                    Nothing outside the normal range in this window.
                  </p>
                ) : null}
              </div>
            </ReportCard>

            {/* ---- Site visits ---- */}
            <ReportCard
              title="Site visits by project"
              subtitle={`${data.visits.total} visits · ${data.visits.highInterest} rated high interest · last 90 days`}
              href="/dashboard/engagement/site-visits"
              linkLabel="Visits"
            >
              <div className="flex flex-col gap-1">
                {data.visits.byProject.slice(0, 6).map((project) => {
                  const max = Math.max(
                    1,
                    ...data.visits.byProject.map((entry) => entry.visits)
                  );

                  return (
                    <Tooltip key={project.projectName}>
                      <TooltipTrigger asChild>
                        <div className="flex items-center gap-2 text-[11.5px]">
                          <span className="w-32 truncate">{project.projectName}</span>
                          <span className="h-2 min-w-0 flex-1 overflow-hidden rounded-full bg-muted">
                            <span
                              className={cn("block h-full", TONE_FILL.primary)}
                              style={{ width: `${(project.visits / max) * 100}%` }}
                            />
                          </span>
                          <span className="w-8 text-right tabular-nums">
                            {project.visits}
                          </span>
                        </div>
                      </TooltipTrigger>
                      <TooltipContent className="text-[11px]">
                        {project.completed} completed · {project.highInterest} high
                        interest · avg rating {project.averageRating.toFixed(1)}
                      </TooltipContent>
                    </Tooltip>
                  );
                })}
              </div>

              <DistributionBar
                className="mt-2.5"
                title="Weekday pattern"
                segments={data.visits.byWeekday.map((day, index) => ({
                  key: day.key,
                  label: day.label,
                  count: day.count,
                  tone: (
                    ["primary", "info", "violet", "success", "warning", "danger", "neutral"] as const
                  )[index % 7],
                }))}
              />
            </ReportCard>

            {/* ---- Field productivity ---- */}
            <ReportCard
              title="Field productivity"
              subtitle="OBM meetings · last 60 days"
              href="/dashboard/engagement/obm-visits"
              linkLabel="OBM visits"
            >
              <div className="flex flex-col gap-1">
                {data.field.slice(0, 6).map((agent) => {
                  const max = Math.max(
                    1,
                    ...data.field.map((entry) => entry.leadsGenerated)
                  );

                  return (
                    <Tooltip key={agent.agentName}>
                      <TooltipTrigger asChild>
                        <div className="flex items-center gap-2 text-[11.5px]">
                          <Route className="size-3 shrink-0 text-muted-foreground" />
                          <span className="w-28 truncate">{agent.agentName}</span>
                          <span className="h-2 min-w-0 flex-1 overflow-hidden rounded-full bg-muted">
                            <span
                              className={cn("block h-full", TONE_FILL.success)}
                              style={{
                                width: `${(agent.leadsGenerated / max) * 100}%`,
                              }}
                            />
                          </span>
                          <span className="w-7 text-right tabular-nums">
                            {agent.leadsGenerated}
                          </span>
                          <span className="w-16 text-right tabular-nums text-muted-foreground">
                            {agent.costPerLead > 0
                              ? formatMoney(agent.costPerLead)
                              : "—"}
                          </span>
                        </div>
                      </TooltipTrigger>
                      <TooltipContent className="text-[11px]">
                        {agent.visits} meetings · {agent.distanceKm.toFixed(0)} km ·{" "}
                        {formatMoney(agent.expense)} spend
                      </TooltipContent>
                    </Tooltip>
                  );
                })}

                {data.field.length === 0 ? (
                  <p className="text-[11.5px] text-muted-foreground">
                    No field meetings recorded in this window.
                  </p>
                ) : null}
              </div>
            </ReportCard>

            {/* ---- Telephony ---- */}
            <ReportCard
              title="Telephony"
              subtitle={`${data.calls.connectRate.toFixed(1)}% connect rate · ${Math.round(data.calls.talkMinutes).toLocaleString()} minutes talked · last 30 days`}
              href="/dashboard/calls/myoperator"
              linkLabel="Dashboard"
              className="lg:col-span-2"
            >
              <div className="grid gap-3 md:grid-cols-[1.5fr_1fr]">
                <div className="min-w-0">
                  <MiniBars
                    values={data.calls.daily.map((point) => point.calls)}
                    labels={data.calls.daily.map((point) => point.date)}
                    tone="primary"
                    height={56}
                  />
                  <div className="mt-1 flex justify-between text-[10.5px] text-muted-foreground tabular-nums">
                    <span>{data.calls.daily[0]?.date}</span>
                    <span>{data.calls.daily.at(-1)?.date}</span>
                  </div>
                </div>

                <div className="flex flex-col gap-2">
                  <DistributionBar
                    title="Direction"
                    segments={[
                      {
                        key: "out",
                        label: "Outbound",
                        count: data.calls.outbound,
                        tone: "primary",
                      },
                      {
                        key: "in",
                        label: "Inbound",
                        count: data.calls.inbound,
                        tone: "info",
                      },
                      {
                        key: "missed",
                        label: "Missed",
                        count: data.calls.missed,
                        tone: "danger",
                      },
                    ]}
                  />
                  <div className="flex items-center gap-1.5 text-[11px] text-muted-foreground">
                    <Waypoints className="size-3" />
                    Busiest hour{" "}
                    {data.calls.hourly
                      .reduce((best, point) => (point.calls > best.calls ? point : best))
                      .hour.toString()
                      .padStart(2, "0")}
                    :00
                  </div>
                </div>
              </div>
            </ReportCard>
          </div>
        )}
      </div>
    </PagePanel>
  );
}
