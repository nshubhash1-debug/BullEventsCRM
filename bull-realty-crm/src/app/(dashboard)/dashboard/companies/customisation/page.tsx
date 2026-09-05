"use client";

import * as React from "react";
import {
  Blocks,
  Check,
  GripVertical,
  Loader2,
  Palette,
  Plus,
  Trash2,
  X,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import {
  ApiError,
  CUSTOM_FIELD_TYPES,
  customisationApi,
  type Branding,
  type CustomField,
  type PickList,
  type PickListEntry,
  type SaveCustomFieldInput,
} from "@/lib/api";
import { cn } from "@/lib/utils";

const OBJECT_LABELS: Record<string, string> = {
  Lead: "Leads",
  Contact: "Contacts",
  Opportunity: "Opportunities",
};

export default function CustomisationPage() {
  return (
    <PagePanel
      icon={Palette}
      title="Customisation"
      hint="The parts of the CRM that are yours rather than the product's: the fields you added, the words you use for things, and what a customer sees on a quotation."
    >
      <Tabs defaultValue="fields" className="min-h-0 flex-1">
        <TabsList>
          <TabsTrigger value="fields">Custom fields</TabsTrigger>
          <TabsTrigger value="lists">Lists</TabsTrigger>
          <TabsTrigger value="branding">Branding</TabsTrigger>
        </TabsList>

        <TabsContent value="fields">
          <CustomFields />
        </TabsContent>

        <TabsContent value="lists">
          <PickListsPanel />
        </TabsContent>

        <TabsContent value="branding">
          <BrandingPanel />
        </TabsContent>
      </Tabs>
    </PagePanel>
  );
}

/* ------------------------------------------------------------------ *
 * Custom fields
 * ------------------------------------------------------------------ */

function CustomFields() {
  const [fields, setFields] = React.useState<CustomField[] | null>(null);
  const [editing, setEditing] = React.useState<CustomField | null>(null);
  const [creating, setCreating] = React.useState<string | null>(null);

  const load = React.useCallback(() => {
    customisationApi
      .fields()
      .then(setFields)
      .catch((error: unknown) => {
        toast.error("Could not load custom fields", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setFields([]);
      });
  }, []);

  React.useEffect(load, [load]);

  async function remove(field: CustomField) {
    try {
      await customisationApi.deleteField(field.id);
      setFields((current) => (current ?? []).filter((f) => f.id !== field.id));
      toast.success(`${field.label} removed`);
    } catch (error) {
      toast.error("Could not remove that field", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  if (fields === null) return <CrmLoadingState label="Loading custom fields" />;

  return (
    <div className="flex flex-col gap-6">
      <p className="max-w-prose text-[12.5px] text-muted-foreground">
        A field you add here appears on the record&apos;s form and travels with
        it through the API. The key it stores under is fixed when you create it,
        so the values already captured never come unstuck from their field.
      </p>

      {Object.keys(OBJECT_LABELS).map((object) => {
        const own = fields.filter((f) => f.object === object);

        return (
          <div key={object} className="flex flex-col gap-2">
            <div className="flex items-center justify-between gap-2 border-b pb-1.5">
              <h3 className="text-[14px] font-semibold">
                {OBJECT_LABELS[object]}
              </h3>
              <Button
                size="sm"
                variant="outline"
                className="h-7"
                onClick={() => setCreating(object)}
              >
                <Plus /> Add field
              </Button>
            </div>

            {own.length === 0 ? (
              <p className="py-1 text-[12.5px] text-muted-foreground">
                No fields added to {OBJECT_LABELS[object].toLowerCase()} yet.
              </p>
            ) : (
              <div className="flex flex-col">
                {own.map((field) => (
                  <div
                    key={field.id}
                    className="flex flex-wrap items-center gap-2 border-b py-2 last:border-b-0"
                  >
                    <GripVertical className="size-3.5 shrink-0 text-muted-foreground/40" />

                    <div className="min-w-0 flex-1">
                      <p className="flex items-center gap-1.5 truncate text-[13px] font-medium">
                        {field.label}
                        {field.required ? (
                          <Badge
                            variant="outline"
                            className="h-4 px-1 text-[9px] font-normal text-amber-600 dark:text-amber-400"
                          >
                            Required
                          </Badge>
                        ) : null}
                        {!field.isActive ? (
                          <Badge
                            variant="outline"
                            className="h-4 px-1 text-[9px] font-normal text-muted-foreground"
                          >
                            Retired
                          </Badge>
                        ) : null}
                      </p>
                      <p className="truncate font-mono text-[11px] text-muted-foreground">
                        {field.key} · {typeLabel(field.type)}
                        {field.usedBy > 0
                          ? ` · used by ${field.usedBy.toLocaleString("en-IN")} records`
                          : ""}
                      </p>
                    </div>

                    <Button
                      size="sm"
                      variant="outline"
                      className="h-7"
                      onClick={() => setEditing(field)}
                    >
                      Edit
                    </Button>
                    <Button
                      size="icon"
                      variant="ghost"
                      aria-label={`Remove ${field.label}`}
                      className="size-7 text-muted-foreground"
                      onClick={() => void remove(field)}
                    >
                      <Trash2 className="size-3.5" />
                    </Button>
                  </div>
                ))}
              </div>
            )}
          </div>
        );
      })}

      <FieldDialog
        object={creating ?? editing?.object ?? "Lead"}
        field={editing}
        open={creating !== null || editing !== null}
        onOpenChange={(open) => {
          if (!open) {
            setCreating(null);
            setEditing(null);
          }
        }}
        onSaved={load}
      />
    </div>
  );
}

function FieldDialog({
  object,
  field,
  open,
  onOpenChange,
  onSaved,
}: {
  object: string;
  field: CustomField | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
}) {
  const [draft, setDraft] = React.useState<SaveCustomFieldInput>(() => blank(object));
  const [optionsText, setOptionsText] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  // Reset during render on the way open, so a reopened dialog never shows the
  // previous field for a frame.
  const [wasOpen, setWasOpen] = React.useState(open);
  if (open !== wasOpen) {
    setWasOpen(open);
    if (open) {
      setDraft(
        field
          ? {
              object: field.object,
              label: field.label,
              helpText: field.helpText,
              type: field.type,
              options: field.options,
              required: field.required,
              showInList: field.showInList,
              sortOrder: field.sortOrder,
              isActive: field.isActive,
            }
          : blank(object)
      );
      setOptionsText(field?.options.join("\n") ?? "");
    }
  }

  async function save() {
    setSaving(true);
    try {
      const options = optionsText
        .split("\n")
        .map((line) => line.trim())
        .filter(Boolean);

      const input: SaveCustomFieldInput = { ...draft, options };

      if (field) await customisationApi.updateField(field.id, input);
      else await customisationApi.createField(input);

      toast.success(field ? "Field updated" : "Field added");
      onSaved();
      onOpenChange(false);
    } catch (error) {
      toast.error("Could not save that field", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{field ? "Edit field" : "Add field"}</DialogTitle>
          <DialogDescription>
            On {OBJECT_LABELS[draft.object]?.toLowerCase() ?? draft.object}.
            {field
              ? " The key and the object cannot change — records already carry values under them."
              : " The label becomes the storage key, once."}
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-4 py-4">
          <div className="space-y-1">
            <Label htmlFor="field-label">Label</Label>
            <Input
              id="field-label"
              value={draft.label}
              placeholder="e.g. Possession preference"
              onChange={(event) =>
                setDraft({ ...draft, label: event.target.value })
              }
            />
          </div>

          <div className="space-y-1">
            <Label>Type</Label>
            <Select
              value={draft.type}
              disabled={Boolean(field && field.usedBy > 0)}
              onValueChange={(value) => setDraft({ ...draft, type: value })}
            >
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {CUSTOM_FIELD_TYPES.map((type) => (
                  <SelectItem key={type.value} value={type.value}>
                    <span className="flex flex-col">
                      <span>{type.label}</span>
                      <span className="text-[11px] text-muted-foreground">
                        {type.hint}
                      </span>
                    </span>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {field && field.usedBy > 0 ? (
              <p className="text-[11px] text-muted-foreground">
                Locked — {field.usedBy.toLocaleString("en-IN")} records already
                hold a value here. Retire this field and add a new one to change
                the type.
              </p>
            ) : null}
          </div>

          {draft.type === "Select" ? (
            <div className="space-y-1">
              <Label htmlFor="field-options">Choices</Label>
              <Textarea
                id="field-options"
                rows={4}
                value={optionsText}
                placeholder={"Ready to move\nUnder construction\nEither"}
                onChange={(event) => setOptionsText(event.target.value)}
              />
              <p className="text-[11px] text-muted-foreground">One per line.</p>
            </div>
          ) : null}

          <div className="space-y-1">
            <Label htmlFor="field-help">Help text</Label>
            <Input
              id="field-help"
              value={draft.helpText ?? ""}
              placeholder="Shown under the field on the form"
              onChange={(event) =>
                setDraft({ ...draft, helpText: event.target.value })
              }
            />
          </div>

          <Toggle
            id="field-required"
            label="Required"
            hint="The record cannot be saved without it."
            checked={draft.required}
            onChange={(required) => setDraft({ ...draft, required })}
          />

          <Toggle
            id="field-list"
            label="Offer as a grid column"
            hint="Appears in the column picker on the record list."
            checked={draft.showInList}
            onChange={(showInList) => setDraft({ ...draft, showInList })}
          />

          {field ? (
            <Toggle
              id="field-active"
              label="Active"
              hint="A retired field stops appearing. Values already captured are kept."
              checked={draft.isActive}
              onChange={(isActive) => setDraft({ ...draft, isActive })}
            />
          ) : null}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            disabled={saving || draft.label.trim().length < 2}
            onClick={() => void save()}
          >
            {saving ? <Loader2 className="animate-spin" /> : <Check />}
            {field ? "Save changes" : "Add field"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function Toggle({
  id,
  label,
  hint,
  checked,
  onChange,
}: {
  id: string;
  label: string;
  hint: string;
  checked: boolean;
  onChange: (value: boolean) => void;
}) {
  return (
    <div className="flex items-center justify-between rounded-lg border p-3">
      <div className="min-w-0 pr-3">
        <Label htmlFor={id}>{label}</Label>
        <p className="text-xs text-muted-foreground">{hint}</p>
      </div>
      <Switch id={id} checked={checked} onCheckedChange={onChange} />
    </div>
  );
}

function blank(object: string): SaveCustomFieldInput {
  return {
    object,
    label: "",
    helpText: "",
    type: "Text",
    options: [],
    required: false,
    showInList: false,
    sortOrder: 0,
    isActive: true,
  };
}

function typeLabel(type: string) {
  return CUSTOM_FIELD_TYPES.find((t) => t.value === type)?.label ?? type;
}

/* ------------------------------------------------------------------ *
 * Pick lists
 * ------------------------------------------------------------------ */

function PickListsPanel() {
  const [lists, setLists] = React.useState<PickList[] | null>(null);
  const [selected, setSelected] = React.useState<string | null>(null);
  const [adding, setAdding] = React.useState("");
  const [busy, setBusy] = React.useState(false);

  const load = React.useCallback(() => {
    customisationApi
      .pickLists()
      .then((all) => {
        setLists(all);
        setSelected((current) => current ?? all[0]?.key ?? null);
      })
      .catch((error: unknown) => {
        toast.error("Could not load the lists", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setLists([]);
      });
  }, []);

  React.useEffect(load, [load]);

  const list = lists?.find((l) => l.key === selected) ?? null;

  async function add() {
    if (!list || adding.trim().length === 0) return;

    setBusy(true);
    try {
      await customisationApi.addValue(list.key, adding.trim(), list.values.length);
      setAdding("");
      load();
      toast.success("Added");
    } catch (error) {
      toast.error("Could not add that entry", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(false);
    }
  }

  async function rename(entry: PickListEntry, label: string) {
    if (label.trim() === entry.label) return;

    try {
      await customisationApi.updateValue(entry.id, {
        label: label.trim(),
        sortOrder: entry.sortOrder,
        isActive: entry.isActive,
      });
      load();
    } catch (error) {
      toast.error("Could not rename that entry", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  async function toggle(entry: PickListEntry, isActive: boolean) {
    try {
      await customisationApi.updateValue(entry.id, {
        label: entry.label,
        sortOrder: entry.sortOrder,
        isActive,
      });
      load();
    } catch (error) {
      toast.error("Could not change that entry", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  async function remove(entry: PickListEntry) {
    try {
      await customisationApi.deleteValue(entry.id);
      load();
      toast.success("Removed");
    } catch (error) {
      toast.error("Could not remove that entry", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  if (lists === null) return <CrmLoadingState label="Loading lists" />;

  return (
    <div className="grid gap-4 lg:grid-cols-[16rem_minmax(0,1fr)]">
      <div className="flex flex-col divide-y overflow-hidden rounded-lg border">
        {lists.map((entry) => (
          <button
            key={entry.key}
            type="button"
            onClick={() => setSelected(entry.key)}
            className={cn(
              "flex flex-col gap-0.5 px-3 py-2.5 text-left transition-colors",
              entry.key === selected ? "bg-accent" : "hover:bg-accent/50"
            )}
          >
            <span className="text-[13px] font-medium">{entry.label}</span>
            <span className="text-[11px] text-muted-foreground tabular-nums">
              {entry.values.filter((v) => v.isActive).length} in use
            </span>
          </button>
        ))}
      </div>

      {list ? (
        <div className="flex min-w-0 flex-col gap-3">
          <div>
            <h3 className="text-[14px] font-semibold">{list.label}</h3>
            <p className="max-w-prose text-[12.5px] text-muted-foreground">
              {list.description}
            </p>
          </div>

          <div className="flex gap-1.5">
            <Input
              value={adding}
              placeholder={`Add to ${list.label.toLowerCase()}…`}
              className="h-8 max-w-xs text-[13px]"
              onChange={(event) => setAdding(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter") void add();
              }}
            />
            <Button
              size="sm"
              className="h-8"
              disabled={busy || adding.trim().length === 0}
              onClick={() => void add()}
            >
              {busy ? <Loader2 className="animate-spin" /> : <Plus />}
              Add
            </Button>
          </div>

          <div className="overflow-hidden rounded-lg border">
            {list.values.map((entry) => (
              <div
                key={entry.id}
                className="flex flex-wrap items-center gap-2 border-b px-3 py-2 last:border-b-0"
              >
                <Input
                  defaultValue={entry.label}
                  className={cn(
                    "h-7 max-w-xs flex-1 text-[13px]",
                    !entry.isActive && "text-muted-foreground line-through"
                  )}
                  onBlur={(event) => void rename(entry, event.target.value)}
                />

                {entry.value !== entry.label ? (
                  <span
                    className="shrink-0 font-mono text-[11px] text-muted-foreground"
                    title="What records actually store. Renaming the label never moves this."
                  >
                    {entry.value}
                  </span>
                ) : null}

                {entry.isSystem ? (
                  <Badge
                    variant="outline"
                    className="h-4 shrink-0 px-1 text-[9px] font-normal text-muted-foreground"
                  >
                    Shipped
                  </Badge>
                ) : null}

                <div className="flex shrink-0 items-center gap-1.5">
                  <Switch
                    aria-label={`${entry.label} in use`}
                    checked={entry.isActive}
                    onCheckedChange={(value) => void toggle(entry, value)}
                  />
                  <Button
                    size="icon"
                    variant="ghost"
                    aria-label={`Remove ${entry.label}`}
                    className="size-7 text-muted-foreground"
                    disabled={entry.isSystem}
                    title={
                      entry.isSystem
                        ? "Ships with the product — switch it off instead"
                        : "Remove"
                    }
                    onClick={() => void remove(entry)}
                  >
                    <X className="size-3.5" />
                  </Button>
                </div>
              </div>
            ))}
          </div>

          <p className="text-[11px] text-muted-foreground">
            Switching an entry off hides it from new records without touching the
            ones that already use it — which is why retiring is safe and deleting
            is not.
          </p>
        </div>
      ) : null}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Branding
 * ------------------------------------------------------------------ */

function BrandingPanel() {
  const [branding, setBranding] = React.useState<Branding | null>(null);
  const [color, setColor] = React.useState("");
  const [logo, setLogo] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  React.useEffect(() => {
    customisationApi
      .branding()
      .then((value) => {
        setBranding(value);
        setColor(value.brandColor ?? "");
        setLogo(value.logoUrl ?? "");
      })
      .catch((error: unknown) => {
        toast.error("Could not load branding", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
      });
  }, []);

  async function save() {
    setSaving(true);
    try {
      const next = await customisationApi.saveBranding({
        brandColor: color.trim() || null,
        logoUrl: logo.trim() || null,
      });
      setBranding(next);
      toast.success("Branding saved");
    } catch (error) {
      toast.error("Could not save branding", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  if (branding === null) return <CrmLoadingState label="Loading branding" />;

  const valid = color === "" || /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(color.trim());

  return (
    <div className="flex max-w-2xl flex-col gap-5">
      <p className="text-[12.5px] text-muted-foreground">
        The quotation PDF and the public quote link are the only two places this
        CRM shows itself to somebody outside your company. This is what they see
        there.
      </p>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1">
          <Label htmlFor="brand-color">Brand colour</Label>
          <div className="flex gap-1.5">
            <Input
              id="brand-color"
              value={color}
              placeholder="#1F4FD8"
              className="font-mono"
              aria-invalid={!valid}
              onChange={(event) => setColor(event.target.value)}
            />
            <span
              className="size-9 shrink-0 rounded-md border"
              style={{ background: valid && color ? color : "transparent" }}
              aria-hidden
            />
          </div>
          {!valid ? (
            <p className="text-xs text-destructive">
              Use a hex value such as #1F4FD8.
            </p>
          ) : null}
        </div>

        <div className="space-y-1">
          <Label htmlFor="brand-logo">Logo URL</Label>
          <Input
            id="brand-logo"
            value={logo}
            placeholder="https://…/logo.png"
            onChange={(event) => setLogo(event.target.value)}
          />
          <p className="text-[11px] text-muted-foreground">
            A full https URL. Uploads come with the document library.
          </p>
        </div>
      </div>

      <div className="rounded-lg border p-4">
        <p className="text-[11px] tracking-widest text-muted-foreground uppercase">
          Preview
        </p>
        <div className="mt-3 flex items-center gap-3 border-b pb-3">
          {logo ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={logo}
              alt=""
              className="h-8 max-w-[140px] object-contain"
              onError={(event) => {
                event.currentTarget.style.display = "none";
              }}
            />
          ) : (
            <span
              className="flex size-8 items-center justify-center rounded text-[11px] font-semibold text-white"
              style={{ background: valid && color ? color : "#64748b" }}
            >
              <Blocks className="size-4" />
            </span>
          )}
          <div>
            <p className="text-[13px] font-semibold">{branding.companyName}</p>
            <p className="text-[11px] text-muted-foreground">
              Quotation BRG/2026/0148
            </p>
          </div>
        </div>
        <div
          className="mt-3 h-1.5 w-24 rounded-full"
          style={{ background: valid && color ? color : "#e2e8f0" }}
        />
      </div>

      <div>
        <Button disabled={saving || !valid} onClick={() => void save()}>
          {saving ? <Loader2 className="animate-spin" /> : <Check />}
          Save branding
        </Button>
      </div>
    </div>
  );
}
