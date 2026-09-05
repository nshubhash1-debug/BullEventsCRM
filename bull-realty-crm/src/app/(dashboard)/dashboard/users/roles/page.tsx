"use client";

import * as React from "react";
import {
  Building2,
  Check,
  Eye,
  EyeOff,
  Loader2,
  Lock,
  Pencil,
  ShieldCheck,
  Users,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { ScrollArea } from "@/components/ui/scroll-area";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  accessApi,
  ApiError,
  OBJECT_ACTIONS,
  VISIBILITY_OPTIONS,
  type FieldPermissionRow,
  type ObjectPermissionRow,
  type ObjectVisibilityRow,
  type ProfileDetail,
  type RoleSummary,
} from "@/lib/api";
import { cn } from "@/lib/utils";

/* ------------------------------------------------------------------ *
 * Labels
 *
 * The API speaks in keys — "post-sales", "SiteVisit" — because those are what
 * is stored. Turning them into English here rather than server-side keeps the
 * stored value stable when the wording changes.
 * ------------------------------------------------------------------ */

const MODULE_LABELS: Record<string, string> = {
  leads: "Leads",
  engagement: "Engagement",
  automation: "Lead Automation",
  calls: "Calls",
  sales: "Sales",
  inventory: "Inventory",
  reports: "Reports",
  calendar: "Calendar",
  goals: "Goals",
  "post-sales": "Post Sales",
  "customer-care": "Customer Care",
  constructions: "Constructions",
  hr: "HR",
  administration: "Administration",
  "system-console": "System Console",
};

const OBJECT_LABELS: Record<string, string> = {
  Lead: "Leads",
  Contact: "Contacts",
  Opportunity: "Opportunities",
  Quotation: "Quotations",
  Unit: "Units",
  Project: "Projects",
  SiteVisit: "Site visits",
  ObmVisit: "OBM visits",
  FollowUp: "Follow-ups",
  Call: "Calls",
  Booking: "Bookings",
  Employee: "Employees",
  Goal: "Goals",
  Report: "Reports",
  User: "Users",
  Company: "Companies",
  Branch: "Branches",
};

function label(map: Record<string, string>, key: string) {
  return map[key] ?? key;
}

const SCOPE_TONE: Record<string, string> = {
  Own: "text-slate-600 dark:text-slate-300",
  Team: "text-sky-600 dark:text-sky-400",
  Company: "text-amber-600 dark:text-amber-400",
  Platform: "text-rose-600 dark:text-rose-400",
};

/* ------------------------------------------------------------------ *
 * Page
 * ------------------------------------------------------------------ */

export default function RolesPage() {
  const [roles, setRoles] = React.useState<RoleSummary[] | null>(null);
  const [selectedKey, setSelectedKey] = React.useState<string | null>(null);

  React.useEffect(() => {
    accessApi
      .roles()
      .then((list) => {
        setRoles(list);
        setSelectedKey((current) => current ?? list[0]?.key ?? null);
      })
      .catch((error: unknown) => {
        toast.error("Could not load the role catalogue", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setRoles([]);
      });
  }, []);

  const selected = roles?.find((role) => role.key === selectedKey) ?? null;

  return (
    <PagePanel
      icon={ShieldCheck}
      title="Roles & access"
      hint="A role fixes how much of the company somebody sees. The permission matrix under it fixes what they may do with what they see, and record visibility sets the floor everything else opens up from."
      className="min-h-0"
    >
      {roles === null ? (
        <CrmLoadingState label="Loading roles" />
      ) : (
        <Tabs defaultValue="roles" className="min-h-0 flex-1">
          <TabsList>
            <TabsTrigger value="roles">Roles</TabsTrigger>
            <TabsTrigger value="visibility">Record visibility</TabsTrigger>
          </TabsList>

          <TabsContent value="roles" className="min-h-0">
            <div className="grid min-h-0 gap-4 lg:grid-cols-[19rem_minmax(0,1fr)]">
              <RoleList
                roles={roles}
                selectedKey={selectedKey}
                onSelect={setSelectedKey}
              />
              {selected ? <RoleDetail role={selected} /> : null}
            </div>
          </TabsContent>

          <TabsContent value="visibility" className="min-h-0">
            <VisibilityPanel />
          </TabsContent>
        </Tabs>
      )}
    </PagePanel>
  );
}

/* ------------------------------------------------------------------ *
 * The catalogue
 * ------------------------------------------------------------------ */

function RoleList({
  roles,
  selectedKey,
  onSelect,
}: {
  roles: RoleSummary[];
  selectedKey: string | null;
  onSelect: (key: string) => void;
}) {
  return (
    <ScrollArea className="max-h-[calc(100vh-19rem)] rounded-lg border">
      <div className="divide-y">
        {roles.map((role) => {
          const active = role.key === selectedKey;

          return (
            <button
              key={role.key}
              type="button"
              onClick={() => onSelect(role.key)}
              className={cn(
                "flex w-full flex-col gap-1 px-3 py-2.5 text-left transition-colors",
                active ? "bg-accent" : "hover:bg-accent/50"
              )}
            >
              <span className="flex items-center gap-2">
                <span className="flex-1 truncate text-[13px] font-medium">
                  {role.name}
                </span>
                <Badge
                  variant="secondary"
                  className="h-5 shrink-0 gap-1 px-1.5 text-[11px] tabular-nums"
                >
                  <Users className="size-3" />
                  {role.userCount}
                </Badge>
              </span>
              <span
                className={cn(
                  "text-[11px]",
                  SCOPE_TONE[role.scope] ?? "text-muted-foreground"
                )}
              >
                {role.scopeLabel}
              </span>
            </button>
          );
        })}
      </div>
    </ScrollArea>
  );
}

/* ------------------------------------------------------------------ *
 * One role: what it means, and the matrix behind it
 * ------------------------------------------------------------------ */

function RoleDetail({ role }: { role: RoleSummary }) {
  return (
    <div className="flex min-w-0 flex-col gap-4">
      <div className="rounded-lg border p-4">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div className="min-w-0">
            <h2 className="text-[15px] font-semibold">{role.name}</h2>
            <p className="mt-0.5 max-w-prose text-[12.5px] text-muted-foreground">
              {role.description}
            </p>
          </div>
          <Badge
            variant="outline"
            className={cn(
              "h-5 shrink-0 px-1.5 text-[11px] font-normal",
              SCOPE_TONE[role.scope]
            )}
          >
            {role.scopeLabel}
          </Badge>
        </div>

        {(role.readOnly || role.wonBusinessOnly || role.peopleData) && (
          <div className="mt-3 flex flex-wrap gap-1.5">
            {role.readOnly ? (
              <Trait icon={Eye} text="Reads widely, writes nothing" />
            ) : null}
            {role.wonBusinessOnly ? (
              <Trait icon={Lock} text="Closed-won business only" />
            ) : null}
            {role.peopleData ? (
              <Trait icon={Users} text="People records, not the pipeline" />
            ) : null}
          </div>
        )}

        <div className="mt-4">
          <p className="text-[11px] tracking-widest text-muted-foreground uppercase">
            Modules this role opens
          </p>
          <div className="mt-1.5 flex flex-wrap gap-1">
            {role.modules.map((module) => (
              <Badge
                key={module}
                variant="secondary"
                className="h-5 px-1.5 text-[11px] font-normal"
              >
                {label(MODULE_LABELS, module)}
              </Badge>
            ))}
          </div>
          <p className="mt-2 text-[11px] text-muted-foreground">
            Scope and modules come from the product&apos;s role catalogue and are
            the same in every company. What a company tunes is the matrix below,
            and the per-person module exceptions on the Users screen.
          </p>
        </div>
      </div>

      {role.profileId === null ? (
        <div className="rounded-lg border border-dashed p-4 text-[12.5px] text-muted-foreground">
          No permission profile is attached to this role yet, so it falls back to
          the defaults the role catalogue defines.
        </div>
      ) : (
        <>
          <PermissionMatrix key={role.profileId} profileId={role.profileId} />
          <FieldPanel key={`fields-${role.profileId}`} profileId={role.profileId} />
        </>
      )}
    </div>
  );
}

function Trait({
  icon: Icon,
  text,
}: {
  icon: typeof Eye;
  text: string;
}) {
  return (
    <span className="flex items-center gap-1.5 rounded-md border px-2 py-1 text-[11px] text-muted-foreground">
      <Icon className="size-3" />
      {text}
    </span>
  );
}

/* ------------------------------------------------------------------ *
 * Object × action matrix
 * ------------------------------------------------------------------ */

/**
 * Edited locally and saved in one go.
 *
 * A per-tick save would be twelve requests to grant one object and would leave
 * a half-applied matrix behind whenever one of them failed. The API replaces
 * the matrix wholesale for the same reason.
 */
function PermissionMatrix({ profileId }: { profileId: number }) {
  const [profile, setProfile] = React.useState<ProfileDetail | null>(null);
  const [draft, setDraft] = React.useState<Map<string, Set<string>>>(new Map());
  const [saving, setSaving] = React.useState(false);

  // No reset to null on the way in: the caller keys this component by profile
  // id, so switching roles remounts it and the initial state is already empty.
  const load = React.useCallback(() => {
    accessApi
      .profile(profileId)
      .then((detail) => {
        setProfile(detail);
        setDraft(toDraft(detail.objects));
      })
      .catch((error: unknown) => {
        toast.error("Could not load this profile", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
      });
  }, [profileId]);

  React.useEffect(load, [load]);

  const dirty = React.useMemo(() => {
    if (!profile) return false;
    return serialise(draft) !== serialise(toDraft(profile.objects));
  }, [draft, profile]);

  function toggle(object: string, action: string, on: boolean) {
    setDraft((prev) => {
      const next = new Map(prev);
      const actions = new Set(next.get(object) ?? []);

      if (on) {
        actions.add(action);
        // The wide grants are meaningless without the narrow one they widen:
        // "modify all" on an object nobody may view reads as access but grants
        // nothing the UI can show.
        if (action === "ViewAll" || action === "ModifyAll") actions.add("View");
        if (action === "ModifyAll") actions.add("ViewAll");
      } else {
        actions.delete(action);
        if (action === "View") {
          actions.delete("ViewAll");
          actions.delete("ModifyAll");
        }
        if (action === "ViewAll") actions.delete("ModifyAll");
      }

      next.set(object, actions);
      return next;
    });
  }

  async function save() {
    setSaving(true);
    try {
      const objects: ObjectPermissionRow[] = [...draft.entries()].map(
        ([object, actions]) => ({ object, actions: [...actions] })
      );

      const saved = await accessApi.saveProfile(profileId, { objects });
      setProfile(saved);
      setDraft(toDraft(saved.objects));
      toast.success("Permissions saved");
    } catch (error) {
      toast.error("Could not save permissions", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  if (!profile) return <CrmLoadingState label="Loading permissions" />;

  const objects = [
    ...new Set([...OBJECT_ORDER, ...draft.keys()]),
  ];

  return (
    <div className="flex min-w-0 flex-col rounded-lg border">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b px-4 py-2.5">
        <div className="min-w-0">
          <p className="text-[13px] font-medium">{profile.name}</p>
          <p className="text-[11px] text-muted-foreground">
            What this role may do with the records its scope already reaches.
          </p>
        </div>
        <div className="flex items-center gap-1.5">
          {dirty ? (
            <Button
              size="sm"
              variant="ghost"
              className="h-7"
              onClick={() => setDraft(toDraft(profile.objects))}
            >
              Discard
            </Button>
          ) : null}
          <Button size="sm" className="h-7" disabled={!dirty || saving} onClick={save}>
            {saving ? <Loader2 className="animate-spin" /> : <Check />}
            Save
          </Button>
        </div>
      </div>

      <ScrollArea className="max-h-[26rem]">
        <table className="w-full border-collapse text-[12.5px]">
          <thead className="sticky top-0 z-10 bg-muted/80 backdrop-blur">
            <tr>
              <th className="border-b px-3 py-2 text-left font-medium">Object</th>
              {OBJECT_ACTIONS.map((action) => (
                <th
                  key={action.value}
                  className="border-b px-2 py-2 text-center font-medium"
                >
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <span className="cursor-help whitespace-nowrap">
                        {action.label}
                      </span>
                    </TooltipTrigger>
                    <TooltipContent className="max-w-56">
                      {action.hint}
                    </TooltipContent>
                  </Tooltip>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {objects.map((object) => {
              const actions = draft.get(object) ?? new Set<string>();

              return (
                <tr key={object} className="border-b last:border-b-0">
                  <td className="px-3 py-1.5 font-medium">
                    {label(OBJECT_LABELS, object)}
                  </td>
                  {OBJECT_ACTIONS.map((action) => (
                    <td key={action.value} className="px-2 py-1.5 text-center">
                      <Checkbox
                        aria-label={`${action.label} ${label(OBJECT_LABELS, object)}`}
                        checked={actions.has(action.value)}
                        onCheckedChange={(value) =>
                          toggle(object, action.value, value === true)
                        }
                      />
                    </td>
                  ))}
                </tr>
              );
            })}
          </tbody>
        </table>
      </ScrollArea>
    </div>
  );
}

/** The order the objects read in — day-to-day sales first, admin last. */
const OBJECT_ORDER = [
  "Lead",
  "Contact",
  "Opportunity",
  "Quotation",
  "SiteVisit",
  "ObmVisit",
  "FollowUp",
  "Call",
  "Unit",
  "Project",
  "Booking",
  "Employee",
  "Goal",
  "Report",
  "User",
  "Branch",
  "Company",
];

function toDraft(rows: ObjectPermissionRow[]) {
  const map = new Map<string, Set<string>>();
  for (const object of OBJECT_ORDER) map.set(object, new Set());
  for (const row of rows) map.set(row.object, new Set(row.actions));
  return map;
}

/** A stable string for the whole matrix, so "has anything changed" is one compare. */
function serialise(draft: Map<string, Set<string>>) {
  return [...draft.entries()]
    .map(([object, actions]) => `${object}:${[...actions].sort().join("+")}`)
    .sort()
    .join("|");
}

/* ------------------------------------------------------------------ *
 * Field-level restrictions
 * ------------------------------------------------------------------ */

/**
 * The fields this profile may not read or may not change.
 *
 * Only the exceptions are stored, so the two ticks start on for everything and
 * the screen reads as "what have we taken away" rather than "what have we
 * granted". Unticking read unticks write with it: a seat that can overwrite a
 * number it cannot see is a worse outcome than either restriction alone.
 */
function FieldPanel({ profileId }: { profileId: number }) {
  const [rows, setRows] = React.useState<FieldPermissionRow[] | null>(null);
  const [draft, setDraft] = React.useState<FieldPermissionRow[]>([]);
  const [saving, setSaving] = React.useState(false);

  React.useEffect(() => {
    accessApi
      .fields(profileId)
      .then((list) => {
        setRows(list);
        setDraft(list);
      })
      .catch((error: unknown) => {
        toast.error("Could not load field restrictions", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setRows([]);
      });
  }, [profileId]);

  const dirty = React.useMemo(
    () => rows !== null && key(draft) !== key(rows),
    [draft, rows]
  );

  function set(row: FieldPermissionRow, patch: Partial<FieldPermissionRow>) {
    setDraft((current) =>
      current.map((entry) => {
        if (entry.object !== row.object || entry.field !== row.field) return entry;

        const next = { ...entry, ...patch };
        // Read is the floor. Taking it away takes write with it, and giving
        // write back gives read back, so the pair can never disagree.
        if (!next.canRead) next.canEdit = false;
        if (next.canEdit) next.canRead = true;
        return next;
      })
    );
  }

  async function save() {
    setSaving(true);
    try {
      const saved = await accessApi.saveFields(profileId, draft);
      setRows(saved);
      setDraft(saved);
      toast.success("Field restrictions saved");
    } catch (error) {
      toast.error("Could not save field restrictions", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  if (rows === null) return <CrmLoadingState label="Loading fields" />;

  const restricted = draft.filter((row) => !row.canRead || !row.canEdit).length;

  const grouped = [...new Set(draft.map((row) => row.object))].map((object) => ({
    object,
    fields: draft.filter((row) => row.object === object),
  }));

  return (
    <div className="flex min-w-0 flex-col rounded-lg border">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b px-4 py-2.5">
        <div className="min-w-0">
          <p className="text-[13px] font-medium">Field restrictions</p>
          <p className="text-[11px] text-muted-foreground">
            {restricted === 0
              ? "Nothing restricted — every field follows its object."
              : `${restricted} ${restricted === 1 ? "field is" : "fields are"} restricted for this role.`}
          </p>
        </div>
        <div className="flex items-center gap-1.5">
          {dirty ? (
            <Button
              size="sm"
              variant="ghost"
              className="h-7"
              onClick={() => setDraft(rows)}
            >
              Discard
            </Button>
          ) : null}
          <Button size="sm" className="h-7" disabled={!dirty || saving} onClick={save}>
            {saving ? <Loader2 className="animate-spin" /> : <Check />}
            Save
          </Button>
        </div>
      </div>

      <ScrollArea className="max-h-[24rem]">
        <div className="flex flex-col">
          {grouped.map((group) => (
            <div key={group.object}>
              <p className="sticky top-0 z-10 bg-muted/80 px-4 py-1.5 text-[11px] tracking-widest text-muted-foreground uppercase backdrop-blur">
                {label(OBJECT_LABELS, group.object)}
              </p>

              {group.fields.map((row) => (
                <div
                  key={`${row.object}.${row.field}`}
                  className="flex items-center gap-3 border-b px-4 py-2 last:border-b-0"
                >
                  <div className="min-w-0 flex-1">
                    <p className="flex items-center gap-1.5 truncate text-[13px] font-medium">
                      {row.label}
                      {row.sensitive ? (
                        <Badge
                          variant="outline"
                          className="h-4 px-1 text-[9px] font-normal text-amber-600 dark:text-amber-400"
                        >
                          Personal
                        </Badge>
                      ) : null}
                    </p>
                    <p className="truncate text-[11px] text-muted-foreground">
                      {row.why}
                    </p>
                  </div>

                  <label className="flex shrink-0 items-center gap-1.5 text-[11.5px] text-muted-foreground">
                    {row.canRead ? (
                      <Eye className="size-3.5" />
                    ) : (
                      <EyeOff className="size-3.5 text-amber-600 dark:text-amber-400" />
                    )}
                    <Checkbox
                      aria-label={`Read ${row.label}`}
                      checked={row.canRead}
                      onCheckedChange={(value) =>
                        set(row, { canRead: value === true })
                      }
                    />
                  </label>

                  <label className="flex shrink-0 items-center gap-1.5 text-[11.5px] text-muted-foreground">
                    <Pencil className="size-3.5" />
                    <Checkbox
                      aria-label={`Edit ${row.label}`}
                      checked={row.canEdit}
                      disabled={!row.canRead}
                      onCheckedChange={(value) =>
                        set(row, { canEdit: value === true })
                      }
                    />
                  </label>
                </div>
              ))}
            </div>
          ))}
        </div>
      </ScrollArea>
    </div>
  );
}

/** A stable string for the whole set, so "has anything changed" is one compare. */
function key(rows: FieldPermissionRow[]) {
  return rows
    .map((row) => `${row.object}.${row.field}:${row.canRead ? 1 : 0}${row.canEdit ? 1 : 0}`)
    .sort()
    .join("|");
}

/* ------------------------------------------------------------------ *
 * Company-wide record visibility
 * ------------------------------------------------------------------ */

function VisibilityPanel() {
  const [rows, setRows] = React.useState<ObjectVisibilityRow[] | null>(null);
  const [pending, setPending] = React.useState<string | null>(null);

  React.useEffect(() => {
    accessApi
      .visibility()
      .then(setRows)
      .catch((error: unknown) => {
        toast.error("Could not load record visibility", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setRows([]);
      });
  }, []);

  async function apply(next: ObjectVisibilityRow) {
    const previous = rows;
    setPending(next.object);
    setRows((current) =>
      (current ?? []).map((row) => (row.object === next.object ? next : row))
    );

    try {
      await accessApi.setVisibility(next);
    } catch (error) {
      setRows(previous);
      toast.error("Could not save that change", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setPending(null);
    }
  }

  if (rows === null) return <CrmLoadingState label="Loading record visibility" />;

  if (rows.length === 0) {
    return (
      <div className="rounded-lg border border-dashed p-4 text-[12.5px] text-muted-foreground">
        No visibility rules are stored for this company. Every object falls back
        to Private, which is the safe end — the owner and their reporting line,
        and nobody else.
      </div>
    );
  }

  return (
    <div className="flex min-w-0 flex-col gap-3">
      <p className="max-w-prose text-[12.5px] text-muted-foreground">
        This is the floor, not the ceiling. A sharing rule, a wider role scope or
        an explicit &ldquo;view all&rdquo; can each open a record up from here;
        nothing narrows past it.
      </p>

      <div className="overflow-hidden rounded-lg border">
        <table className="w-full border-collapse text-[12.5px]">
          <thead className="bg-muted/60">
            <tr>
              <th className="border-b px-3 py-2 text-left font-medium">Object</th>
              <th className="border-b px-3 py-2 text-left font-medium">
                Default visibility
              </th>
              <th className="border-b px-3 py-2 text-left font-medium">
                <Tooltip>
                  <TooltipTrigger asChild>
                    <span className="cursor-help">Managers inherit</span>
                  </TooltipTrigger>
                  <TooltipContent className="max-w-64">
                    Whether somebody higher up the reporting line automatically
                    reaches their team&apos;s records of this type.
                  </TooltipContent>
                </Tooltip>
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.object} className="border-b last:border-b-0">
                <td className="px-3 py-1.5 font-medium">
                  {label(OBJECT_LABELS, row.object)}
                </td>
                <td className="px-3 py-1.5">
                  <Select
                    value={row.visibility}
                    disabled={pending === row.object}
                    onValueChange={(value) =>
                      void apply({
                        ...row,
                        visibility: value as ObjectVisibilityRow["visibility"],
                      })
                    }
                  >
                    <SelectTrigger className="h-7 w-48 text-[12.5px]">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {VISIBILITY_OPTIONS.map((option) => (
                        <SelectItem key={option.value} value={option.value}>
                          <span className="flex flex-col">
                            <span>{option.label}</span>
                            <span className="text-[11px] text-muted-foreground">
                              {option.hint}
                            </span>
                          </span>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </td>
                <td className="px-3 py-1.5">
                  <Switch
                    aria-label={`Managers inherit ${label(OBJECT_LABELS, row.object)}`}
                    checked={row.grantAccessUsingHierarchy}
                    disabled={pending === row.object}
                    onCheckedChange={(value) =>
                      void apply({ ...row, grantAccessUsingHierarchy: value })
                    }
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <p className="flex items-center gap-1.5 text-[11px] text-muted-foreground">
        <Building2 className="size-3" />
        These settings belong to this company alone. Another tenant on the same
        install keeps its own.
      </p>
    </div>
  );
}
