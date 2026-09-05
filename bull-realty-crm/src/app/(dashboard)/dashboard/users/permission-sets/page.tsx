"use client";

import * as React from "react";
import { CalendarClock, KeyRound, Loader2, Plus, Trash2, UserPlus } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Pill } from "@/components/crm/metrics";
import { RuleCard, RuleEmpty } from "@/components/admin/rule-list";
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
import {
  accessApi,
  ApiError,
  getUsers,
  type ObjectPermissionRow,
  type PermissionSetSummary,
  type UserListItem,
} from "@/lib/api";

const OBJECTS = [
  "Lead", "Contact", "Opportunity", "Quotation", "Unit",
  "Project", "SiteVisit", "FollowUp", "Call", "Booking", "Report",
];

const ACTIONS = ["View", "Create", "Edit", "Delete", "ViewAll", "ModifyAll"];

/**
 * Permission sets.
 *
 * A profile is the one baseline a seat carries; a permission set is an extra
 * grant laid on top of it. The distinction is what lets somebody cover a
 * colleague's leave for a fortnight without their job title changing — and,
 * more importantly, what lets that access lapse on its own.
 *
 * Every grant can carry an expiry, because a temporary permission nobody
 * remembers to remove is how access quietly accumulates until an audit finds
 * five people who can see everything and nobody who knows why.
 */
export default function PermissionSetsPage() {
  const [sets, setSets] = React.useState<PermissionSetSummary[] | null>(null);
  const [users, setUsers] = React.useState<UserListItem[]>([]);
  const [editing, setEditing] = React.useState<PermissionSetSummary | null>(null);
  const [creating, setCreating] = React.useState(false);
  const [granting, setGranting] = React.useState<PermissionSetSummary | null>(null);
  const [busy, setBusy] = React.useState<number | null>(null);

  const load = React.useCallback(() => {
    accessApi
      .permissionSets()
      .then(setSets)
      .catch((error: unknown) => {
        toast.error("Could not load permission sets", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setSets([]);
      });
  }, []);

  React.useEffect(() => {
    load();
    getUsers().then((rows) => setUsers(rows.filter((u) => u.isActive))).catch(() => setUsers([]));
  }, [load]);

  async function act(id: number, work: () => Promise<unknown>, done: string) {
    setBusy(id);
    try {
      await work();
      toast.success(done);
      load();
    } catch (error) {
      toast.error("Could not change the set", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  if (!sets) {
    return (
      <PagePanel icon={KeyRound} title="Permission sets">
        <CrmLoadingState label="Loading permission sets" />
      </PagePanel>
    );
  }

  return (
    <PagePanel
      icon={KeyRound}
      title="Permission sets"
      hint="Extra access laid on top of somebody's profile, optionally until a date."
      actions={
        <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
          <Plus className="size-3.5" />
          New set
        </Button>
      }
    >
      <div className="flex flex-col gap-4">
        {sets.length === 0 ? (
          <RuleCard icon={KeyRound} title="Sets" count={0}>
            <RuleEmpty
              title="No permission set yet."
              meaning="Everyone has exactly what their profile allows. A set grants somebody more without changing their role — and can be made to expire on its own."
              action={
                <Button size="sm" variant="outline" onClick={() => setCreating(true)}>
                  Create the first set
                </Button>
              }
            />
          </RuleCard>
        ) : (
          sets.map((set) => (
            <RuleCard
              key={set.id}
              icon={KeyRound}
              title={set.name}
              count={set.grants.length}
              hint={set.grants.length === 1 ? "person holds it" : "people hold it"}
              actions={
                <>
                  {set.isSystem ? <Pill tone="neutral">Built in</Pill> : null}
                  <Button
                    size="sm"
                    variant="outline"
                    className="h-7 gap-1 px-2 text-[11px]"
                    onClick={() => setGranting(set)}
                  >
                    <UserPlus className="size-3" />
                    Grant
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    className="h-7 px-2 text-[11px]"
                    onClick={() => setEditing(set)}
                  >
                    Edit
                  </Button>
                  {!set.isSystem ? (
                    <Button
                      size="sm"
                      variant="ghost"
                      className="h-7 px-1.5 text-muted-foreground"
                      disabled={busy === set.id}
                      onClick={() => {
                        if (window.confirm(`Delete "${set.name}"? Everyone holding it loses that access.`)) {
                          act(set.id, () => accessApi.deletePermissionSet(set.id), "Set deleted");
                        }
                      }}
                    >
                      <Trash2 className="size-3.5" />
                    </Button>
                  ) : null}
                </>
              }
            >
              <div className="px-4 py-3">
                {set.description ? (
                  <p className="mb-2 text-[12.5px] text-muted-foreground">{set.description}</p>
                ) : null}

                <div className="flex flex-wrap gap-1.5">
                  {set.objects.length === 0 ? (
                    <span className="text-[12px] text-muted-foreground">
                      Grants nothing yet — edit it to add object permissions.
                    </span>
                  ) : (
                    set.objects.map((grant) => (
                      <span
                        key={grant.object}
                        className="inline-flex items-center gap-1 rounded border bg-muted/40 px-1.5 py-0.5 text-[11.5px]"
                      >
                        <span className="font-medium">{grant.object}</span>
                        <span className="text-muted-foreground">
                          {grant.actions.join(", ")}
                        </span>
                      </span>
                    ))
                  )}
                </div>
              </div>

              {set.grants.length > 0 ? (
                <div className="border-t">
                  <table className="w-full text-[12.5px]">
                    <tbody>
                      {set.grants.map((grant) => (
                        <tr key={grant.id} className="border-b last:border-0">
                          <td className="px-4 py-2">
                            <span className={grant.hasLapsed ? "text-muted-foreground line-through" : ""}>
                              {grant.userName}
                            </span>
                            {grant.reason ? (
                              <p className="text-[11px] text-muted-foreground">{grant.reason}</p>
                            ) : null}
                          </td>

                          <td className="px-4 py-2 text-right whitespace-nowrap">
                            {grant.expiresAt ? (
                              <span className="inline-flex items-center gap-1.5">
                                <CalendarClock className="size-3.5 text-muted-foreground" />
                                <Pill tone={grant.hasLapsed ? "neutral" : "warning"}>
                                  {grant.hasLapsed ? "lapsed" : "until"}{" "}
                                  {new Date(grant.expiresAt).toLocaleDateString("en-IN", {
                                    day: "numeric",
                                    month: "short",
                                  })}
                                </Pill>
                              </span>
                            ) : (
                              <Pill tone="info">permanent</Pill>
                            )}
                          </td>

                          <td className="w-10 px-4 py-2 text-right">
                            <Button
                              size="sm"
                              variant="ghost"
                              className="h-7 px-1.5 text-muted-foreground"
                              onClick={() =>
                                act(
                                  set.id,
                                  () => accessApi.revokePermissionSet(grant.id),
                                  `${grant.userName} no longer holds it`
                                )
                              }
                            >
                              <Trash2 className="size-3.5" />
                            </Button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : null}
            </RuleCard>
          ))
        )}
      </div>

      <p className="mt-2 text-[11.5px] text-muted-foreground">
        Granting or revoking a set ends that person&apos;s live sessions, so the new access takes
        effect on their next sign-in rather than whenever their token happens to expire.
      </p>

      <Dialog
        open={creating || editing !== null}
        onOpenChange={(open) => {
          if (!open) {
            setCreating(false);
            setEditing(null);
          }
        }}
      >
        <SetForm
          set={editing}
          onDone={() => {
            setCreating(false);
            setEditing(null);
            load();
          }}
        />
      </Dialog>

      <Dialog open={granting !== null} onOpenChange={(open) => (open ? null : setGranting(null))}>
        <GrantForm
          set={granting}
          users={users}
          onDone={() => {
            setGranting(null);
            load();
          }}
        />
      </Dialog>
    </PagePanel>
  );
}

function SetForm({ set, onDone }: { set: PermissionSetSummary | null; onDone: () => void }) {
  const [name, setName] = React.useState(set?.name ?? "");
  const [description, setDescription] = React.useState(set?.description ?? "");
  const [grants, setGrants] = React.useState<Record<string, Set<string>>>(() => {
    const map: Record<string, Set<string>> = {};
    for (const row of set?.objects ?? []) map[row.object] = new Set(row.actions);
    return map;
  });
  const [saving, setSaving] = React.useState(false);

  function toggle(object: string, action: string) {
    setGrants((current) => {
      const next = { ...current };
      const actions = new Set(next[object] ?? []);

      if (actions.has(action)) actions.delete(action);
      else actions.add(action);

      if (actions.size === 0) delete next[object];
      else next[object] = actions;

      return next;
    });
  }

  async function save() {
    const objects: ObjectPermissionRow[] = Object.entries(grants).map(([object, actions]) => ({
      object,
      actions: [...actions],
    }));

    setSaving(true);
    try {
      const body = { name, description: description || null, objects };

      if (set) await accessApi.updatePermissionSet(set.id, body);
      else await accessApi.createPermissionSet(body);

      toast.success(set ? "Set updated" : "Set created");
      onDone();
    } catch (error) {
      toast.error("Could not save the set", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <DialogContent className="sm:max-w-2xl">
      <DialogHeader>
        <DialogTitle>{set ? "Edit permission set" : "New permission set"}</DialogTitle>
        <DialogDescription>
          These grants are added to whatever the holder&apos;s profile already allows. Nothing here
          takes access away.
        </DialogDescription>
      </DialogHeader>

      <div className="grid max-h-[60vh] gap-3 overflow-y-auto pr-1">
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="grid gap-1.5">
            <Label htmlFor="ps-name">Name</Label>
            <Input
              id="ps-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Holiday cover — collections"
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="ps-desc">What it is for</Label>
            <Input
              id="ps-desc"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Read the booking ledger while Priya is away"
            />
          </div>
        </div>

        <div className="overflow-x-auto rounded-lg border">
          <table className="w-full text-[12.5px]">
            <thead>
              <tr className="border-b bg-muted/50 text-[10.5px] tracking-wide text-muted-foreground uppercase">
                <th className="px-3 py-2 text-left font-medium">Object</th>
                {ACTIONS.map((action) => (
                  <th key={action} className="px-2 py-2 text-center font-medium">
                    {action}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {OBJECTS.map((object) => (
                <tr key={object} className="border-b last:border-0">
                  <td className="px-3 py-1.5 font-medium">{object}</td>
                  {ACTIONS.map((action) => (
                    <td key={action} className="px-2 py-1.5 text-center">
                      <Checkbox
                        checked={grants[object]?.has(action) ?? false}
                        onCheckedChange={() => toggle(object, action)}
                        aria-label={`${action} on ${object}`}
                      />
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <DialogFooter>
        <Button onClick={save} disabled={saving} className="gap-1.5">
          {saving ? <Loader2 className="size-4 animate-spin" /> : <KeyRound className="size-4" />}
          {set ? "Save set" : "Create set"}
        </Button>
      </DialogFooter>
    </DialogContent>
  );
}

function GrantForm({
  set,
  users,
  onDone,
}: {
  set: PermissionSetSummary | null;
  users: UserListItem[];
  onDone: () => void;
}) {
  const [userId, setUserId] = React.useState("");
  const [expires, setExpires] = React.useState("");
  const [reason, setReason] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  const [seen, setSeen] = React.useState<PermissionSetSummary | null>(null);

  if (set !== seen) {
    setSeen(set);
    if (set) {
      setUserId("");
      setExpires("");
      setReason("");
    }
  }

  async function save() {
    if (!set || !userId) {
      toast.error("Choose who this is for.");
      return;
    }

    setSaving(true);
    try {
      await accessApi.grantPermissionSet(set.id, {
        userId: Number(userId),
        expiresAt: expires || null,
        reason: reason.trim() || null,
      });

      toast.success("Granted", {
        description: expires
          ? "It will lapse on its own — nobody has to remember to remove it."
          : "This grant is permanent until somebody removes it.",
      });

      onDone();
    } catch (error) {
      toast.error("Could not grant the set", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <DialogContent className="sm:max-w-md">
      <DialogHeader>
        <DialogTitle>Grant {set?.name}</DialogTitle>
        <DialogDescription>
          Their live sessions end, so the new access applies from their next sign-in.
        </DialogDescription>
      </DialogHeader>

      <div className="grid gap-3">
        <div className="grid gap-1.5">
          <Label>Who</Label>
          <Select value={userId} onValueChange={setUserId}>
            <SelectTrigger>
              <SelectValue placeholder="Choose a person" />
            </SelectTrigger>
            <SelectContent>
              {users.map((user) => (
                <SelectItem key={user.id} value={user.id.toString()}>
                  {user.name} · {user.role}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="grid gap-1.5">
          <Label htmlFor="ps-expires">Until</Label>
          <Input
            id="ps-expires"
            type="date"
            value={expires}
            onChange={(e) => setExpires(e.target.value)}
          />
          <p className="text-[11.5px] text-muted-foreground">
            Leave blank for permanent. An expiry is the difference between cover for a fortnight
            and access nobody remembers granting.
          </p>
        </div>

        <div className="grid gap-1.5">
          <Label htmlFor="ps-reason">Why</Label>
          <Input
            id="ps-reason"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Covering Priya's leave"
          />
        </div>
      </div>

      <DialogFooter>
        <Button onClick={save} disabled={saving} className="gap-1.5">
          {saving ? <Loader2 className="size-4 animate-spin" /> : <UserPlus className="size-4" />}
          Grant it
        </Button>
      </DialogFooter>
    </DialogContent>
  );
}
