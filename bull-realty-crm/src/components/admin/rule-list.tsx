"use client";

import * as React from "react";
import { Loader2, Pencil, Trash2, type LucideIcon } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { cn } from "@/lib/utils";

/**
 * The card every rule list in the admin console is drawn on.
 *
 * Six screens here show the same thing — an ordered set of rules, each of which
 * can be switched off, edited or deleted — and the only real difference between
 * them is the sentence in the middle. Sharing the chrome keeps that difference
 * visible instead of burying it in six copies of the same table markup, and it
 * means a change to how "off" looks lands everywhere at once.
 */
export function RuleCard({
  icon: Icon,
  title,
  count,
  hint,
  actions,
  children,
  className,
}: {
  icon?: LucideIcon;
  title: string;
  count?: number;
  hint?: string;
  actions?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <section className={cn("overflow-hidden rounded-xl border bg-card shadow-xs", className)}>
      <header className="flex flex-wrap items-center justify-between gap-2 border-b px-4 py-2.5">
        <h2 className="flex items-center gap-2 text-[13.5px] font-semibold">
          {Icon ? <Icon className="size-4 text-primary" /> : null}
          {title}
          {count !== undefined ? (
            <span className="rounded bg-muted px-1.5 py-px text-[11px] font-medium text-muted-foreground tabular-nums">
              {count}
            </span>
          ) : null}
          {hint ? (
            <span className="text-[11.5px] font-normal text-muted-foreground">{hint}</span>
          ) : null}
        </h2>

        {actions ? <div className="flex items-center gap-1.5">{actions}</div> : null}
      </header>

      {children}
    </section>
  );
}

/**
 * One rule: a name, the sentence that says what it does, and the three controls
 * every rule has.
 *
 * The sentence is the point. A rule spread across six labelled columns is one
 * nobody reads carefully enough to notice it is wrong; the same rule written as
 * a line of English is checked at a glance.
 */
export function RuleRow({
  name,
  sentence,
  note,
  badges,
  active,
  busy,
  order,
  onToggle,
  onEdit,
  onDelete,
}: {
  name: string;
  sentence: React.ReactNode;
  note?: string | null;
  badges?: React.ReactNode;
  active: boolean;
  busy?: boolean;
  /** Shown when the order the rules run in is part of their meaning. */
  order?: number;
  onToggle?: () => void;
  onEdit?: () => void;
  onDelete?: () => void;
}) {
  return (
    <div
      className={cn(
        "flex flex-wrap items-start gap-3 border-b px-4 py-3 last:border-0",
        !active && "opacity-55"
      )}
    >
      {order !== undefined ? (
        <span className="mt-0.5 grid size-6 shrink-0 place-items-center rounded bg-muted text-[11px] font-medium text-muted-foreground tabular-nums">
          {order}
        </span>
      ) : null}

      <div className="min-w-0 flex-1">
        <p className="flex flex-wrap items-center gap-2 text-[13px] font-medium">
          {name}
          {badges}
        </p>
        <p className="mt-0.5 text-[12.5px] text-muted-foreground">{sentence}</p>
        {note ? (
          <p className="mt-0.5 text-[11.5px] text-muted-foreground/80">{note}</p>
        ) : null}
      </div>

      <div className="flex shrink-0 items-center gap-1.5">
        {busy ? (
          <Loader2 className="size-3.5 animate-spin text-muted-foreground" />
        ) : (
          <>
            {onToggle ? (
              <Switch
                checked={active}
                onCheckedChange={onToggle}
                aria-label={active ? "Switch off" : "Switch on"}
              />
            ) : null}

            {onEdit ? (
              <Button size="sm" variant="ghost" className="h-7 px-2 text-[11px]" onClick={onEdit}>
                <Pencil className="size-3.5" />
              </Button>
            ) : null}

            {onDelete ? (
              <Button
                size="sm"
                variant="ghost"
                className="h-7 px-1.5 text-muted-foreground"
                onClick={onDelete}
              >
                <Trash2 className="size-3.5" />
              </Button>
            ) : null}
          </>
        )}
      </div>
    </div>
  );
}

/**
 * What a rule list says when it is empty.
 *
 * Two sentences rather than one: what is not there, and what the absence means.
 * "No assignment rule" alone leaves somebody wondering whether that is a
 * problem; saying leads stay unassigned answers it.
 */
export function RuleEmpty({
  title,
  meaning,
  action,
}: {
  title: string;
  meaning: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="px-4 py-12 text-center">
      <p className="text-[13px] text-muted-foreground">{title}</p>
      <p className="mx-auto mt-1 max-w-md text-[12.5px] text-muted-foreground">{meaning}</p>
      {action ? <div className="mt-3">{action}</div> : null}
    </div>
  );
}
