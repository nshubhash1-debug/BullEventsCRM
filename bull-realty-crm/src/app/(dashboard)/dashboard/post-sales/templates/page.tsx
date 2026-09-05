"use client";

import * as React from "react";
import { Copy, Eye, FileText, Loader2, Save, TriangleAlert } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Pill, TONE_TEXT } from "@/components/crm/metrics";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api";
import {
  documentsApi,
  TEMPLATE_LABELS,
  type DocumentTemplate,
  type MergeField,
  type Preview,
} from "@/lib/lifecycle-api";
import { postSalesApi, type BookingRow, type DemandRow } from "@/lib/post-sales-api";
import { cn } from "@/lib/utils";

/**
 * Document templates.
 *
 * The wording of a demand letter is a commercial and legal decision that
 * changes without a deployment — a developer should never be the bottleneck on
 * "add the GST number to the footer". So the letters live here, as editable
 * drafts, and the merge tokens are published rather than left to be discovered.
 *
 * The preview is the part that makes this usable. An author needs to see the
 * letter with a real booking's numbers in it, and the list of tokens that did
 * not resolve, before three hundred copies go out with a blank where the amount
 * should be.
 */
export default function TemplatesPage() {
  const [templates, setTemplates] = React.useState<DocumentTemplate[] | null>(null);
  const [fields, setFields] = React.useState<MergeField[]>([]);
  const [selected, setSelected] = React.useState<DocumentTemplate | null>(null);

  const load = React.useCallback(() => {
    documentsApi
      .templates()
      .then((rows) => {
        setTemplates(rows);
        setSelected((current) =>
          current ? (rows.find((r) => r.id === current.id) ?? rows[0] ?? null) : (rows[0] ?? null)
        );
      })
      .catch((error: unknown) => {
        toast.error("Could not load templates", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setTemplates([]);
      });
  }, []);

  React.useEffect(() => {
    load();
    documentsApi.reference().then((r) => setFields(r.mergeFields)).catch(() => setFields([]));
  }, [load]);

  if (!templates) {
    return (
      <PagePanel icon={FileText} title="Document templates">
        <CrmLoadingState label="Loading templates" />
      </PagePanel>
    );
  }

  return (
    <PagePanel
      icon={FileText}
      title="Document templates"
      hint="The letters this system writes. Edit the wording without a deployment."
    >
      <div className="flex min-h-0 flex-1 gap-4">
        {/* ---------------- the list ---------------- */}
        <aside className="w-64 shrink-0 overflow-y-auto rounded-xl border bg-card shadow-xs">
          {templates.map((template) => (
            <button
              key={template.id}
              type="button"
              onClick={() => setSelected(template)}
              className={cn(
                "flex w-full flex-col items-start gap-0.5 border-b px-3 py-2.5 text-left transition-colors last:border-0",
                selected?.id === template.id ? "bg-primary/10" : "hover:bg-accent/50"
              )}
            >
              <span className="flex w-full items-center gap-1.5 text-[12.5px] font-medium">
                <span className="min-w-0 flex-1 truncate">{template.name}</span>
                {template.isDefault ? <Pill tone="primary">default</Pill> : null}
              </span>
              <span className="text-[11px] text-muted-foreground">
                {TEMPLATE_LABELS[template.kind] ?? template.kind}
                {template.usedCount > 0 ? ` · used ${template.usedCount}×` : ""}
                {template.isActive ? "" : " · off"}
              </span>
            </button>
          ))}
        </aside>

        {/* ---------------- the editor ---------------- */}
        {selected ? (
          <Editor
            key={selected.id}
            template={selected}
            fields={fields}
            onSaved={load}
          />
        ) : (
          <p className="flex-1 py-12 text-center text-[13px] text-muted-foreground">
            No template yet.
          </p>
        )}
      </div>
    </PagePanel>
  );
}

function Editor({
  template,
  fields,
  onSaved,
}: {
  template: DocumentTemplate;
  fields: MergeField[];
  onSaved: () => void;
}) {
  const [name, setName] = React.useState(template.name);
  const [subject, setSubject] = React.useState(template.subject ?? "");
  const [body, setBody] = React.useState(template.body);
  const [isDefault, setIsDefault] = React.useState(template.isDefault);
  const [isActive, setIsActive] = React.useState(template.isActive);
  const [saving, setSaving] = React.useState(false);

  const [preview, setPreview] = React.useState<Preview | null>(null);
  const [previewing, setPreviewing] = React.useState(false);
  const [bookings, setBookings] = React.useState<BookingRow[]>([]);
  const [demands, setDemands] = React.useState<DemandRow[]>([]);

  const bodyRef = React.useRef<HTMLTextAreaElement>(null);

  React.useEffect(() => {
    postSalesApi.bookings().then(setBookings).catch(() => setBookings([]));
    postSalesApi.demands().then(setDemands).catch(() => setDemands([]));
  }, []);

  const grouped = React.useMemo(() => {
    const map = new Map<string, MergeField[]>();
    for (const field of fields) {
      map.set(field.group, [...(map.get(field.group) ?? []), field]);
    }
    return [...map.entries()];
  }, [fields]);

  /** Drops a token in at the cursor, which is where an author is looking. */
  function insert(token: string) {
    const area = bodyRef.current;

    if (!area) {
      setBody((current) => current + token);
      return;
    }

    const start = area.selectionStart;
    const end = area.selectionEnd;

    setBody((current) => current.slice(0, start) + token + current.slice(end));

    // Restored after React has repainted, or the caret jumps to the end.
    requestAnimationFrame(() => {
      area.focus();
      area.setSelectionRange(start + token.length, start + token.length);
    });
  }

  async function save() {
    setSaving(true);
    try {
      await documentsApi.updateTemplate(template.id, {
        kind: template.kind,
        name,
        subject: subject || null,
        body,
        isDefault,
        isActive,
      });

      toast.success("Template saved", {
        description: "Letters already written keep the wording they went out with.",
      });

      onSaved();
    } catch (error) {
      toast.error("Could not save the template", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  async function runPreview() {
    const booking = bookings[0];

    if (!booking) {
      toast.error("There is no booking to preview against yet.");
      return;
    }

    const demand = demands.find((d) => d.bookingId === booking.id);

    setPreviewing(true);
    try {
      setPreview(
        await documentsApi.preview({
          body,
          subject,
          bookingId: booking.id,
          demandId: demand?.id ?? null,
        })
      );
    } catch (error) {
      toast.error("Could not render the preview", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setPreviewing(false);
    }
  }

  return (
    <div className="flex min-w-0 flex-1 flex-col gap-3">
      <div className="flex flex-wrap items-end gap-3 rounded-xl border bg-card p-4 shadow-xs">
        <div className="grid min-w-[200px] flex-1 gap-1.5">
          <Label htmlFor="t-name">Name</Label>
          <Input id="t-name" value={name} onChange={(e) => setName(e.target.value)} />
        </div>

        <div className="grid min-w-[260px] flex-[2] gap-1.5">
          <Label htmlFor="t-subject">Subject line</Label>
          <Input
            id="t-subject"
            value={subject}
            onChange={(e) => setSubject(e.target.value)}
            placeholder="Used when this goes out by email"
          />
        </div>

        <label className="flex items-center gap-2 pb-2 text-[12.5px]">
          <Switch checked={isDefault} onCheckedChange={setIsDefault} />
          Default for this kind
        </label>

        <label className="flex items-center gap-2 pb-2 text-[12.5px]">
          <Switch checked={isActive} onCheckedChange={setIsActive} />
          Active
        </label>

        <Button className="mb-1 gap-1.5" disabled={saving} onClick={save}>
          {saving ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
          Save
        </Button>
      </div>

      <div className="grid min-h-0 flex-1 gap-3 lg:grid-cols-[1fr_220px]">
        {/* ---------------- body ---------------- */}
        <div className="flex min-h-0 flex-col gap-2">
          <div className="flex items-center justify-between">
            <Label htmlFor="t-body">Body</Label>
            <Button
              size="sm"
              variant="outline"
              className="h-7 gap-1 text-[11.5px]"
              disabled={previewing}
              onClick={runPreview}
            >
              {previewing ? (
                <Loader2 className="size-3 animate-spin" />
              ) : (
                <Eye className="size-3" />
              )}
              Preview with real data
            </Button>
          </div>

          <Textarea
            id="t-body"
            ref={bodyRef}
            value={body}
            onChange={(e) => setBody(e.target.value)}
            className="min-h-[320px] flex-1 font-mono text-[11.5px] leading-relaxed"
            spellCheck={false}
          />
        </div>

        {/* ---------------- tokens ---------------- */}
        <aside className="min-h-0 overflow-y-auto rounded-xl border bg-card p-3 shadow-xs">
          <p className="mb-2 text-[11px] font-semibold tracking-wide text-muted-foreground uppercase">
            Merge fields
          </p>
          <p className="mb-2.5 text-[11px] text-muted-foreground">
            Click one to drop it in at the cursor.
          </p>

          {grouped.map(([group, list]) => (
            <div key={group} className="mb-3">
              <p className="mb-1 text-[10.5px] font-medium tracking-wide text-muted-foreground uppercase">
                {group}
              </p>
              <div className="flex flex-col gap-0.5">
                {list.map((field) => (
                  <button
                    key={field.token}
                    type="button"
                    title={`${field.label} — e.g. ${field.example}`}
                    onClick={() => insert(field.token)}
                    className="flex items-center gap-1 rounded px-1.5 py-1 text-left font-mono text-[10.5px] hover:bg-accent"
                  >
                    <Copy className="size-2.5 shrink-0 text-muted-foreground/50" />
                    <span className="truncate">{field.token}</span>
                  </button>
                ))}
              </div>
            </div>
          ))}
        </aside>
      </div>

      {/* ---------------- preview ---------------- */}
      {preview ? (
        <div className="overflow-hidden rounded-xl border bg-card shadow-xs">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b px-4 py-2.5">
            <p className="text-[13px] font-semibold">
              Preview
              <span className="ml-2 font-normal text-muted-foreground">{preview.subject}</span>
            </p>

            {preview.unresolved.length > 0 ? (
              <p className={cn("flex items-center gap-1.5 text-[12px]", TONE_TEXT.danger)}>
                <TriangleAlert className="size-3.5" />
                {preview.unresolved.length} token
                {preview.unresolved.length === 1 ? "" : "s"} did not resolve:{" "}
                <span className="font-mono">{preview.unresolved.join(", ")}</span>
              </p>
            ) : (
              <Pill tone="success">every token resolved</Pill>
            )}
          </div>

          <iframe
            title="Template preview"
            srcDoc={preview.body}
            className="h-[420px] w-full border-0 bg-white"
            sandbox="allow-same-origin"
          />
        </div>
      ) : null}
    </div>
  );
}
