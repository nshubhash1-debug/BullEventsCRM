"use client";

import * as React from "react";
import Link from "next/link";
import { ChevronRight, type LucideIcon } from "lucide-react";

import { TONE_SOFT, TONE_TEXT, type Tone } from "@/components/crm/metrics";
import { cn } from "@/lib/utils";

/* ------------------------------------------------------------------ *
 * The record page furniture.
 *
 * These are the three pieces a Salesforce record page is built from — the
 * path, the highlights panel and the related list — kept here rather than
 * inline so a booking, and later a customer or a possession, are laid out by
 * the same rules instead of by whoever wrote that screen.
 * ------------------------------------------------------------------ */

/**
 * The chevron path across the top of a record.
 *
 * It answers "where is this in its life" before any number is read, and it is
 * the one control on the page that shows the stages a record has not reached
 * yet — a status badge alone can only ever show the current one.
 */
export function RecordPath({
  stages,
  current,
  labelOf,
  className,
}: {
  stages: readonly string[];
  current: string;
  labelOf?: (stage: string) => string;
  className?: string;
}) {
  const index = stages.indexOf(current);

  // A terminal state is not a step on the road, so the path collapses to a
  // single banner rather than pretending the record is mid-journey.
  if (index < 0) {
    return (
      <div
        className={cn(
          "flex items-center gap-2 rounded-md border px-3 py-2 text-[12.5px] font-medium",
          TONE_SOFT.danger,
          className
        )}
      >
        <span className="size-1.5 rounded-full bg-current" />
        {labelOf?.(current) ?? current}
      </div>
    );
  }

  return (
    <ol className={cn("flex min-w-0 overflow-x-auto", className)}>
      {stages.map((stage, i) => {
        const done = i < index;
        const here = i === index;

        // The notch: a chevron cut out of the right edge and a matching one out
        // of the left, so the steps interlock the way the Salesforce path does.
        // The ends are squared off, because a path that points into nothing on
        // the left reads as though a step is missing.
        const shape =
          i === 0
            ? "[clip-path:polygon(0_0,calc(100%-10px)_0,100%_50%,calc(100%-10px)_100%,0_100%)]"
            : i === stages.length - 1
              ? "[clip-path:polygon(0_0,100%_0,100%_100%,0_100%,10px_50%)]"
              : "[clip-path:polygon(0_0,calc(100%-10px)_0,100%_50%,calc(100%-10px)_100%,0_100%,10px_50%)]";

        return (
          <li key={stage} className="min-w-0 flex-1">
            <div
              className={cn(
                "flex h-8 items-center justify-center px-4 text-[11.5px] font-medium whitespace-nowrap",
                i === 0 ? "pl-3" : "pl-5",
                shape,
                done && "bg-emerald-600 text-white",
                here && "bg-primary text-primary-foreground",
                !done && !here && "bg-muted text-muted-foreground"
              )}
              title={labelOf?.(stage) ?? stage}
            >
              {labelOf?.(stage) ?? stage}
            </div>
          </li>
        );
      })}
    </ol>
  );
}

export interface HighlightField {
  label: string;
  value: React.ReactNode;
  tone?: Tone;
  /** Rendered small and quiet under the value. */
  hint?: string;
}

/**
 * The highlights panel: who this record is, and the four or five numbers
 * somebody would otherwise scroll to find.
 *
 * Everything here is read-only on purpose. The panel is what a person checks
 * mid-phone-call, so it must never be something they can change by mistake.
 */
export function HighlightsPanel({
  icon: Icon,
  objectLabel,
  title,
  subtitle,
  breadcrumb,
  fields,
  actions,
  path,
  className,
}: {
  icon: LucideIcon;
  objectLabel: string;
  title: string;
  subtitle?: React.ReactNode;
  breadcrumb?: { label: string; href: string };
  fields: HighlightField[];
  actions?: React.ReactNode;
  path?: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("rounded-xl border bg-card shadow-xs", className)}>
      <div className="flex flex-wrap items-start justify-between gap-3 px-5 pt-4">
        <div className="flex min-w-0 items-start gap-3">
          <span className="mt-0.5 grid size-10 shrink-0 place-items-center rounded-lg bg-emerald-600 text-white">
            <Icon className="size-5" />
          </span>

          <div className="min-w-0">
            <p className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
              {breadcrumb ? (
                <>
                  <Link
                    href={breadcrumb.href}
                    className="hover:text-foreground hover:underline"
                  >
                    {breadcrumb.label}
                  </Link>
                  <ChevronRight className="mx-0.5 inline size-3 align-[-1px]" />
                </>
              ) : null}
              {objectLabel}
            </p>

            <h1 className="truncate text-xl font-semibold tracking-tight">{title}</h1>

            {subtitle ? (
              <div className="mt-0.5 truncate text-[12.5px] text-muted-foreground">
                {subtitle}
              </div>
            ) : null}
          </div>
        </div>

        {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
      </div>

      {path ? <div className="px-5 pt-3.5">{path}</div> : null}

      {/* Hairlines on the cells rather than a gap over a grey ground: with five
          fields the last row is short on most breakpoints, and a gap-based grid
          paints that leftover space as a slab of border colour. */}
      <div className="mt-3.5 grid border-t sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-5">
        {fields.map((field) => (
          <div key={field.label} className="border-r border-b px-5 py-2.5">
            <p className="text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
              {field.label}
            </p>
            <p
              className={cn(
                "mt-0.5 truncate text-[15px] font-semibold tabular-nums",
                field.tone ? TONE_TEXT[field.tone] : "text-foreground"
              )}
            >
              {field.value}
            </p>
            {field.hint ? (
              <p className="truncate text-[11px] text-muted-foreground">{field.hint}</p>
            ) : null}
          </div>
        ))}
      </div>
    </div>
  );
}

/**
 * A related list: one card per collection hanging off the record.
 *
 * The count sits in the header rather than being left for the reader to work
 * out, because "3 applicants" and "no applicants" are different facts and an
 * empty table only shows one of them.
 */
export function RelatedList({
  icon: Icon,
  title,
  count,
  hint,
  actions,
  children,
  empty,
  className,
}: {
  icon: LucideIcon;
  title: string;
  count?: number;
  hint?: string;
  actions?: React.ReactNode;
  children?: React.ReactNode;
  empty?: string;
  className?: string;
}) {
  return (
    <section className={cn("overflow-hidden rounded-xl border bg-card shadow-xs", className)}>
      <header className="flex flex-wrap items-center justify-between gap-2 border-b px-4 py-2.5">
        <h2 className="flex items-center gap-2 text-[13.5px] font-semibold">
          <Icon className="size-4 text-primary" />
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

      {count === 0 ? (
        <p className="px-4 py-6 text-center text-[12.5px] text-muted-foreground">
          {empty ?? "Nothing here yet."}
        </p>
      ) : (
        <div className="overflow-x-auto">{children}</div>
      )}
    </section>
  );
}

/**
 * The Details tab field grid — label above value, two columns on a wide screen
 * and one on a narrow one.
 */
export function FieldGrid({
  fields,
  columns = 2,
  className,
}: {
  fields: Array<{ label: string; value: React.ReactNode }>;
  columns?: 2 | 3;
  className?: string;
}) {
  return (
    <dl
      className={cn(
        "grid gap-x-8 gap-y-3.5",
        columns === 3 ? "sm:grid-cols-2 lg:grid-cols-3" : "sm:grid-cols-2",
        className
      )}
    >
      {fields.map((field) => (
        <div key={field.label} className="min-w-0 border-b pb-2.5">
          <dt className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
            {field.label}
          </dt>
          <dd className="mt-0.5 text-[13.5px] break-words">{field.value}</dd>
        </div>
      ))}
    </dl>
  );
}
