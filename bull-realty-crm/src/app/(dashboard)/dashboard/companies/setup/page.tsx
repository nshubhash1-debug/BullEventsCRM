"use client";

import * as React from "react";
import { ArrowRight, Check, ListChecks } from "lucide-react";
import Link from "next/link";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Button } from "@/components/ui/button";
import { ApiError, customisationApi, type SetupStep } from "@/lib/api";
import { cn } from "@/lib/utils";

/**
 * What a new workspace still has to do.
 *
 * Every step is counted from the data rather than from a stored "wizard
 * completed" flag, which means two things worth having: a company that arrived
 * through an import starts with most of it already ticked, and the list becomes
 * honest again if somebody later deletes their only branch.
 */
export default function SetupPage() {
  const [steps, setSteps] = React.useState<SetupStep[] | null>(null);

  React.useEffect(() => {
    customisationApi
      .setup()
      .then(setSteps)
      .catch((error: unknown) => {
        toast.error("Could not load the checklist", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setSteps([]);
      });
  }, []);

  const done = (steps ?? []).filter((step) => step.done).length;
  const total = steps?.length ?? 0;

  return (
    <PagePanel
      icon={ListChecks}
      title="Set up your workspace"
      hint="Counted from what is actually in the CRM, not from a checkbox somebody ticked. Delete your last branch and this list notices."
      actions={
        steps ? (
          <span className="text-[12px] text-muted-foreground tabular-nums">
            {done} of {total} done
          </span>
        ) : null
      }
    >
      {steps === null ? (
        <CrmLoadingState label="Checking your workspace" />
      ) : (
        <div className="flex max-w-3xl flex-col gap-4">
          <div className="h-1.5 overflow-hidden rounded-full bg-muted">
            <div
              className="h-full rounded-full bg-primary transition-[width]"
              style={{ width: `${total === 0 ? 0 : (done / total) * 100}%` }}
            />
          </div>

          <div className="flex flex-col">
            {steps.map((step) => (
              <div
                key={step.key}
                className="flex flex-wrap items-center gap-3 border-b py-3 last:border-b-0"
              >
                <span
                  className={cn(
                    "flex size-6 shrink-0 items-center justify-center rounded-full border text-[11px]",
                    step.done
                      ? "border-emerald-600/40 bg-emerald-600/10 text-emerald-600 dark:text-emerald-400"
                      : "text-muted-foreground"
                  )}
                >
                  {step.done ? <Check className="size-3.5" /> : null}
                </span>

                <div className="min-w-0 flex-1">
                  <p
                    className={cn(
                      "text-[13.5px] font-medium",
                      step.done && "text-muted-foreground"
                    )}
                  >
                    {step.title}
                  </p>
                  <p className="text-[12px] text-muted-foreground">
                    {step.description}
                  </p>
                </div>

                {step.progress > 0 ? (
                  <span className="shrink-0 text-[12px] text-muted-foreground tabular-nums">
                    {step.progress.toLocaleString("en-IN")}
                  </span>
                ) : null}

                {step.href ? (
                  <Button
                    size="sm"
                    variant={step.done ? "ghost" : "outline"}
                    className="h-7 shrink-0"
                    asChild
                  >
                    <Link href={step.href}>
                      {step.done ? "Review" : "Do it"}
                      <ArrowRight />
                    </Link>
                  </Button>
                ) : null}
              </div>
            ))}
          </div>

          {done === total && total > 0 ? (
            <p className="rounded-lg border border-dashed p-4 text-[12.5px] text-muted-foreground">
              Everything on the list is done. This page stays available — it is a
              health check as much as a first-run wizard.
            </p>
          ) : null}
        </div>
      )}
    </PagePanel>
  );
}
