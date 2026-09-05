"use client";

import { PlugZap } from "lucide-react";

import { cn } from "@/lib/utils";

/**
 * Says plainly that a third-party provider is not wired up yet, and what the
 * screen is showing instead.
 *
 * Worth its own component because the alternative is worse: a dashboard that
 * looks connected but is really rendering internal data would leave someone
 * believing their dialler or WhatsApp account is live when nothing is sending.
 */
export function IntegrationNotice({
  provider,
  showing,
  className,
}: {
  provider: string;
  /** What the screen renders in the meantime. */
  showing: string;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "flex flex-wrap items-center gap-2 rounded border border-amber-500/30 bg-amber-500/10 px-2.5 py-1.5 text-[12px]",
        className
      )}
    >
      <PlugZap className="size-3.5 shrink-0 text-amber-600 dark:text-amber-400" />
      <span className="font-medium text-amber-700 dark:text-amber-400">
        {provider} is not connected.
      </span>
      <span className="text-amber-700/80 dark:text-amber-400/80">{showing}</span>
    </div>
  );
}
