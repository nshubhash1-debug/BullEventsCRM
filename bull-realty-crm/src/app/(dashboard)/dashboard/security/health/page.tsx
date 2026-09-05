"use client";

import * as React from "react";
import Link from "next/link";
import {
  ArrowRight,
  CheckCircle2,
  RefreshCw,
  ShieldAlert,
  ShieldCheck,
  TriangleAlert,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { TONE_FILL, TONE_TEXT, type Tone } from "@/components/crm/metrics";
import { Button } from "@/components/ui/button";
import { accessApi, ApiError, type SecurityFinding, type SecurityHealth } from "@/lib/api";
import { cn } from "@/lib/utils";

const SEVERITY: Record<string, { tone: Tone; label: string; icon: typeof ShieldAlert }> = {
  Risk: { tone: "danger", label: "Risk", icon: ShieldAlert },
  Warning: { tone: "warning", label: "Worth fixing", icon: TriangleAlert },
  Ok: { tone: "success", label: "Passing", icon: CheckCircle2 },
};

/**
 * Security health check.
 *
 * Every finding on this page is computed from live rows — accounts, sessions,
 * API keys, field rules, the running sign-in configuration — rather than from a
 * stored assessment. That is the difference between a health check and a
 * checklist: this one cannot drift from the thing it describes, and it goes
 * green on its own the moment somebody fixes the underlying cause.
 *
 * Each finding says what was measured, why it matters, and links to the screen
 * that fixes it. A dashboard that only says "warning" makes work rather than
 * removing it.
 */
export default function SecurityHealthPage() {
  const [data, setData] = React.useState<SecurityHealth | null>(null);
  const [refreshing, setRefreshing] = React.useState(false);

  const load = React.useCallback(() => {
    accessApi
      .securityHealth()
      .then(setData)
      .catch((error: unknown) => {
        toast.error("Could not run the health check", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
      })
      .finally(() => setRefreshing(false));
  }, []);

  React.useEffect(load, [load]);

  if (!data) {
    return (
      <PagePanel icon={ShieldCheck} title="Security health check">
        <CrmLoadingState label="Checking" detail="Accounts, sessions, keys and field rules." />
      </PagePanel>
    );
  }

  const scoreTone: Tone =
    data.score >= 90 ? "success" : data.score >= 75 ? "info" : data.score >= 50 ? "warning" : "danger";

  const attention = data.findings.filter((f) => f.severity !== "Ok");
  const passing = data.findings.filter((f) => f.severity === "Ok");

  return (
    <PagePanel
      icon={ShieldCheck}
      title="Security health check"
      hint="Measured from live data every time this page is opened, not from a saved assessment."
      actions={
        <Button
          variant="outline"
          size="sm"
          className="gap-1.5"
          disabled={refreshing}
          onClick={() => {
            setRefreshing(true);
            load();
          }}
        >
          <RefreshCw className={cn("size-3.5", refreshing && "animate-spin")} />
          Re-check
        </Button>
      }
    >
      <div className="flex flex-col gap-4">
        {/* ---------------- the score ---------------- */}
        <section className="flex flex-wrap items-center gap-6 rounded-xl border bg-card p-5 shadow-xs">
          <Dial score={data.score} tone={scoreTone} />

          <div className="min-w-0 flex-1">
            <p className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
              Overall
            </p>
            <p className={cn("text-2xl font-semibold tracking-tight", TONE_TEXT[scoreTone])}>
              {data.grade}
            </p>
            <p className="mt-1 text-[12.5px] text-muted-foreground">
              {data.risks > 0
                ? `${data.risks} thing${data.risks === 1 ? "" : "s"} to fix now, ${data.warnings} worth tidying.`
                : data.warnings > 0
                  ? `Nothing urgent. ${data.warnings} worth tidying.`
                  : "Every check passed."}
            </p>
          </div>

          <div className="grid grid-cols-3 gap-px overflow-hidden rounded-lg border bg-border text-center">
            {[
              { label: "Risks", value: data.risks, tone: "danger" as Tone },
              { label: "Warnings", value: data.warnings, tone: "warning" as Tone },
              { label: "Passing", value: data.passed, tone: "success" as Tone },
            ].map((cell) => (
              <div key={cell.label} className="bg-card px-5 py-2.5">
                <p className={cn("text-xl font-semibold tabular-nums", TONE_TEXT[cell.tone])}>
                  {cell.value}
                </p>
                <p className="text-[10.5px] tracking-wide text-muted-foreground uppercase">
                  {cell.label}
                </p>
              </div>
            ))}
          </div>
        </section>

        {/* ---------------- what needs doing ---------------- */}
        {attention.length > 0 ? (
          <section className="flex flex-col gap-2">
            <h2 className="text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
              Needs attention
            </h2>
            {attention.map((finding) => (
              <FindingCard key={finding.key} finding={finding} />
            ))}
          </section>
        ) : null}

        {/* ---------------- what is fine ---------------- */}
        {passing.length > 0 ? (
          <section className="flex flex-col gap-2">
            <h2 className="text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
              Passing
            </h2>
            <div className="overflow-hidden rounded-xl border bg-card shadow-xs">
              {passing.map((finding, index) => (
                <div
                  key={finding.key}
                  className={cn(
                    "flex items-start gap-3 px-4 py-2.5",
                    index > 0 && "border-t"
                  )}
                >
                  <CheckCircle2 className={cn("mt-0.5 size-4 shrink-0", TONE_TEXT.success)} />
                  <div className="min-w-0">
                    <p className="text-[13px] font-medium">{finding.title}</p>
                    <p className="text-[12px] text-muted-foreground">{finding.finding}</p>
                  </div>
                </div>
              ))}
            </div>
          </section>
        ) : null}

        <p className="text-[11.5px] text-muted-foreground">
          Checked {new Date(data.checkedAt).toLocaleString("en-IN")}.
        </p>
      </div>
    </PagePanel>
  );
}

/**
 * One finding, with the reason it matters kept alongside it.
 *
 * The "why" line is not decoration: an administrator who does not know why a
 * dormant account is dangerous will not prioritise it, and a health check whose
 * items are never prioritised is a health check nobody reads twice.
 */
function FindingCard({ finding }: { finding: SecurityFinding }) {
  const spec = SEVERITY[finding.severity] ?? SEVERITY.Ok;
  const Icon = spec.icon;

  return (
    <article
      className={cn(
        "flex flex-wrap items-start gap-3 rounded-xl border bg-card p-4 shadow-xs",
        finding.severity === "Risk" && "border-red-500/30"
      )}
    >
      <span
        className={cn(
          "mt-0.5 grid size-8 shrink-0 place-items-center rounded-lg",
          finding.severity === "Risk"
            ? "bg-red-500/10"
            : finding.severity === "Warning"
              ? "bg-amber-500/10"
              : "bg-emerald-500/10"
        )}
      >
        <Icon className={cn("size-4", TONE_TEXT[spec.tone])} />
      </span>

      <div className="min-w-0 flex-1">
        <p className="flex flex-wrap items-center gap-2 text-[13.5px] font-semibold">
          {finding.title}
          <span
            className={cn(
              "rounded border px-1.5 py-px text-[10.5px] font-normal",
              finding.severity === "Risk"
                ? "border-red-500/25 bg-red-500/10 text-red-700 dark:text-red-400"
                : "border-amber-500/25 bg-amber-500/10 text-amber-700 dark:text-amber-400"
            )}
          >
            {spec.label}
          </span>
        </p>

        <p className="mt-0.5 text-[13px]">{finding.finding}</p>
        <p className="mt-1 text-[12px] text-muted-foreground">{finding.why}</p>
      </div>

      {finding.fix ? (
        <div className="shrink-0">
          {finding.fixHref ? (
            <Button asChild variant="outline" size="sm" className="gap-1.5 text-[12px]">
              <Link href={finding.fixHref}>
                {finding.fix}
                <ArrowRight className="size-3.5" />
              </Link>
            </Button>
          ) : (
            <p className="max-w-[220px] text-right text-[12px] text-muted-foreground">
              {finding.fix}
            </p>
          )}
        </div>
      ) : null}
    </article>
  );
}

/** The score as a ring, because a number alone does not read as a verdict. */
function Dial({ score, tone }: { score: number; tone: Tone }) {
  const radius = 34;
  const circumference = 2 * Math.PI * radius;
  const filled = (score / 100) * circumference;

  return (
    <div className="relative size-[88px] shrink-0">
      <svg viewBox="0 0 80 80" className="size-full -rotate-90">
        <circle
          cx="40"
          cy="40"
          r={radius}
          fill="none"
          strokeWidth="7"
          className="stroke-muted"
        />
        <circle
          cx="40"
          cy="40"
          r={radius}
          fill="none"
          strokeWidth="7"
          strokeLinecap="round"
          strokeDasharray={`${filled} ${circumference}`}
          className={cn(TONE_FILL[tone].replace("bg-", "stroke-"))}
        />
      </svg>

      <div className="absolute inset-0 grid place-items-center">
        <span className={cn("text-xl font-semibold tabular-nums", TONE_TEXT[tone])}>{score}</span>
      </div>
    </div>
  );
}
