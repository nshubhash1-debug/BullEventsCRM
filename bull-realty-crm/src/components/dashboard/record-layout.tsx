"use client";

import { useState, type ReactNode } from "react";
import {
  Check,
  ChevronRight,
  Loader2,
  Pencil,
  X,
  type LucideIcon,
} from "lucide-react";

import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/utils";

/* ------------------------------------------------------------------ *
 * Highlights panel
 * ------------------------------------------------------------------ */

export interface HighlightField {
  label: string;
  value: ReactNode;
}

interface HighlightsPanelProps {
  icon: LucideIcon;
  /** The object name, shown small and uppercase above the record name. */
  objectLabel: string;
  recordName: string;
  /** Optional line under the record name — company, account, etc. */
  subtitle?: ReactNode;
  /** The compact key/value strip along the bottom. */
  fields: HighlightField[];
  actions?: ReactNode;
  breadcrumb?: ReactNode;
}

/**
 * Salesforce's highlights panel: object type and record name at the top-left,
 * the record's actions at the top-right, and a compact strip of the fields you
 * need at a glance running along the bottom.
 */
export function HighlightsPanel({
  icon: Icon,
  objectLabel,
  recordName,
  subtitle,
  fields,
  actions,
  breadcrumb,
}: HighlightsPanelProps) {
  return (
    <div className="rounded-lg border bg-card shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3 px-4 pt-3 pb-3">
        <div className="flex min-w-0 items-center gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded bg-primary text-primary-foreground">
            <Icon className="size-5" />
          </span>
          <div className="min-w-0">
            {breadcrumb ? (
              <div className="mb-0.5 text-[11px] text-muted-foreground">
                {breadcrumb}
              </div>
            ) : null}
            <p className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
              {objectLabel}
            </p>
            <h1 className="truncate text-[20px] leading-tight font-bold tracking-tight">
              {recordName}
            </h1>
            {subtitle ? (
              <div className="mt-0.5 truncate text-[12.5px] text-muted-foreground">
                {subtitle}
              </div>
            ) : null}
          </div>
        </div>

        {actions ? (
          <div className="flex flex-wrap items-center gap-1.5">{actions}</div>
        ) : null}
      </div>

      <dl className="grid grid-cols-2 border-t sm:grid-cols-3 lg:grid-cols-5">
        {fields.map((field) => (
          <div
            key={field.label}
            className="min-w-0 border-r px-4 py-2.5 last:border-r-0"
          >
            <dt className="truncate text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
              {field.label}
            </dt>
            <dd className="truncate text-[13px] font-medium">{field.value}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Record field grid — the dense "Details" layout
 * ------------------------------------------------------------------ */

export type RecordFieldType = "text" | "textarea" | "select" | "date" | "number";

export interface RecordField {
  label: string;
  value: ReactNode;
  /** Stretch across both columns — long text, addresses, remarks. */
  full?: boolean;
  /** Show the inline-edit affordance on hover. */
  editable?: boolean;

  /** Server-side field name. Required before a field can be edited in place. */
  name?: string;
  /** Raw value handed to the editor, for when the displayed value is decorated. */
  raw?: string | number | null;
  type?: RecordFieldType;
  options?: { label: string; value: string }[];
}

interface RecordFieldGridProps {
  fields: RecordField[];
  /**
   * Commits one field. Resolving closes the editor; rejecting leaves it open
   * with the typed value intact, since the caller has already said why.
   */
  onEditField?: (field: RecordField, value: string | null) => Promise<void>;
  className?: string;
}

/**
 * Two-column field grid matching a Salesforce record's Details tab: a small
 * bold label with its value directly beneath, hairline dividers between rows,
 * and a pencil that appears on hover for fields that can be edited.
 *
 * The pencil swaps that one value for an editor and nothing else moves — so
 * correcting a phone number does not mean opening a fifty-field form.
 */
export function RecordFieldGrid({
  fields,
  onEditField,
  className,
}: RecordFieldGridProps) {
  const [editing, setEditing] = useState<string | null>(null);
  const [draft, setDraft] = useState("");
  const [saving, setSaving] = useState(false);

  function begin(field: RecordField) {
    setEditing(field.label);
    setDraft(
      field.raw === null || field.raw === undefined
        ? ""
        : field.type === "date"
          ? new Date(String(field.raw)).toISOString().slice(0, 10)
          : String(field.raw)
    );
  }

  async function commit(field: RecordField) {
    if (!onEditField) return;

    setSaving(true);
    try {
      await onEditField(field, draft.trim() === "" ? null : draft.trim());
      setEditing(null);
    } catch {
      // Deliberately kept open so the typed value survives a failed save.
    } finally {
      setSaving(false);
    }
  }

  return (
    <dl className={cn("grid grid-cols-1 gap-x-10 md:grid-cols-2", className)}>
      {fields.map((field) => {
        const isEditing = editing === field.label;
        const canEdit = field.editable && !!field.name && !!onEditField;

        return (
          <div
            key={field.label}
            className={cn(
              "group flex items-start justify-between gap-3 border-b py-1.5",
              field.full && "md:col-span-2"
            )}
          >
            <div className="min-w-0 flex-1">
              <dt className="text-[11.5px] font-semibold text-muted-foreground">
                {field.label}
              </dt>

              <dd className="mt-px text-[13px] break-words">
                {isEditing ? (
                  <div className="flex items-center gap-1">
                    {field.type === "textarea" ? (
                      <Textarea
                        autoFocus
                        value={draft}
                        onChange={(event) => setDraft(event.target.value)}
                        onKeyDown={(event) => {
                          if (event.key === "Escape") setEditing(null);
                          // Enter makes a newline here, so committing needs a
                          // modifier — the shortcut chat inputs already use.
                          if (
                            event.key === "Enter" &&
                            (event.metaKey || event.ctrlKey)
                          ) {
                            commit(field);
                          }
                        }}
                        rows={3}
                        className="min-h-16 text-[13px]"
                      />
                    ) : field.type === "select" ? (
                      <select
                        autoFocus
                        value={draft}
                        onChange={(event) => setDraft(event.target.value)}
                        onKeyDown={(event) => {
                          if (event.key === "Escape") setEditing(null);
                        }}
                        className="h-7 flex-1 rounded border bg-background px-1.5 text-[13px] outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
                      >
                        <option value="">—</option>
                        {field.options?.map((option) => (
                          <option key={option.value} value={option.value}>
                            {option.label}
                          </option>
                        ))}
                      </select>
                    ) : (
                      <Input
                        autoFocus
                        type={
                          field.type === "date"
                            ? "date"
                            : field.type === "number"
                              ? "number"
                              : "text"
                        }
                        value={draft}
                        onChange={(event) => setDraft(event.target.value)}
                        onKeyDown={(event) => {
                          if (event.key === "Enter") commit(field);
                          if (event.key === "Escape") setEditing(null);
                        }}
                        className="h-7 flex-1 text-[13px]"
                      />
                    )}

                    <button
                      type="button"
                      aria-label={`Save ${field.label}`}
                      disabled={saving}
                      onClick={() => commit(field)}
                      className="shrink-0 rounded p-1 text-emerald-600 hover:bg-emerald-500/10 disabled:opacity-50"
                    >
                      {saving ? (
                        <Loader2 className="size-3.5 animate-spin" />
                      ) : (
                        <Check className="size-3.5" />
                      )}
                    </button>
                    <button
                      type="button"
                      aria-label={`Cancel editing ${field.label}`}
                      disabled={saving}
                      onClick={() => setEditing(null)}
                      className="shrink-0 rounded p-1 text-muted-foreground hover:bg-accent disabled:opacity-50"
                    >
                      <X className="size-3.5" />
                    </button>
                  </div>
                ) : field.value === null ||
                  field.value === undefined ||
                  field.value === "" ? (
                  <span className="text-muted-foreground/60">—</span>
                ) : (
                  field.value
                )}
              </dd>
            </div>

            {canEdit && !isEditing ? (
              <button
                type="button"
                aria-label={`Edit ${field.label}`}
                onClick={() => begin(field)}
                className="mt-1 shrink-0 rounded p-1 text-muted-foreground opacity-0 transition-opacity hover:bg-accent hover:text-foreground focus-visible:opacity-100 group-hover:opacity-100"
              >
                <Pencil className="size-3.5" />
              </button>
            ) : null}
          </div>
        );
      })}
    </dl>
  );
}

/* ------------------------------------------------------------------ *
 * Record section — a titled block inside the Details tab
 * ------------------------------------------------------------------ */

/**
 * A titled block inside the Details tab, collapsible from its heading.
 *
 * The twistie is what makes a long record usable: a lead carries five sections
 * and forty fields, and the rep who only ever touches the contact details
 * should be able to fold the rest away rather than scroll past it every time.
 * Open by default, because a record that opens closed hides the thing the
 * reader came for.
 */
export function RecordSection({
  title,
  children,
  className,
  defaultOpen = true,
}: {
  title: string;
  children: ReactNode;
  className?: string;
  defaultOpen?: boolean;
}) {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <section className={cn("py-1", className)}>
      <h3 className="mb-1 border-b pb-1.5">
        <button
          type="button"
          aria-expanded={open}
          onClick={() => setOpen((current) => !current)}
          className="group/section flex w-full items-center gap-1.5 text-left text-[12px] font-bold tracking-wide text-muted-foreground uppercase transition-colors hover:text-foreground"
        >
          <ChevronRight
            aria-hidden
            className={cn(
              "size-3.5 shrink-0 transition-transform",
              open && "rotate-90"
            )}
          />
          {title}
        </button>
      </h3>

      {open ? children : null}
    </section>
  );
}
