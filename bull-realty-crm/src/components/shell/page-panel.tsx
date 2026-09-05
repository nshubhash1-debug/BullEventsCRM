"use client";

import type { ReactNode } from "react";
import { CircleHelp, type LucideIcon } from "lucide-react";

import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { cn } from "@/lib/utils";

interface PagePanelProps {
  icon: LucideIcon;
  title: string;
  /** Rendered in a tooltip behind the "?" next to the title. */
  hint?: string;
  /** Primary/secondary buttons on the right of the title row. */
  actions?: ReactNode;
  /** Filter/search strip rendered under the title row. */
  toolbar?: ReactNode;
  /** Status strip rendered under the toolbar (record counts, applied filters). */
  subToolbar?: ReactNode;
  children: ReactNode;
  className?: string;
  /** Drop the panel's inner padding — used when the body is a full-bleed table. */
  flush?: boolean;
}

/**
 * The white working surface every module page sits on: a title row with its
 * actions, an optional filter toolbar, then the body — the pattern used by
 * dense enterprise CRMs so the chrome stays fixed while the body scrolls.
 */
export function PagePanel({
  icon: Icon,
  title,
  hint,
  actions,
  toolbar,
  subToolbar,
  children,
  className,
  flush = false,
}: PagePanelProps) {
  return (
    <section
      className={cn(
        "flex min-w-0 flex-1 flex-col overflow-hidden rounded-xl border bg-card shadow-xs",
        className
      )}
    >
      <div className="flex flex-wrap items-center justify-between gap-2 px-5 py-3.5">
        <h1 className="flex items-center gap-2 text-[17px] font-semibold tracking-tight">
          <Icon className="size-[18px] text-primary" />
          {title}
          {hint ? (
            <Tooltip>
              <TooltipTrigger asChild>
                <button
                  type="button"
                  aria-label={`About ${title}`}
                  className="text-muted-foreground/60 hover:text-muted-foreground"
                >
                  <CircleHelp className="size-3.5" />
                </button>
              </TooltipTrigger>
              <TooltipContent className="max-w-xs">{hint}</TooltipContent>
            </Tooltip>
          ) : null}
        </h1>

        {actions ? (
          <div className="flex flex-wrap items-center gap-1.5">{actions}</div>
        ) : null}
      </div>

      {toolbar ? (
        <div className="flex flex-wrap items-center gap-2 border-t px-5 py-2.5">
          {toolbar}
        </div>
      ) : null}

      {subToolbar ? (
        <div className="flex flex-wrap items-center gap-2 border-t bg-muted/40 px-5 py-2 text-[12px] text-muted-foreground">
          {subToolbar}
        </div>
      ) : null}

      <div
        className={cn(
          "min-w-0 flex-1 border-t",
          !flush && "p-5"
        )}
      >
        {children}
      </div>
    </section>
  );
}
