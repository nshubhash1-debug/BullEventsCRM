"use client";

import * as React from "react";
import { Clock, Globe, Loader2, Lock, Timer } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Pill } from "@/components/crm/metrics";
import { Button } from "@/components/ui/button";
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
import { Switch } from "@/components/ui/switch";
import { accessApi, ApiError, type LoginPolicy } from "@/lib/api";
import { cn } from "@/lib/utils";

/** Sunday first, matching the bitmask the server stores. */
const DAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

const EVERY_DAY = 0b1111111;
const WEEKDAYS = 0b0111110;

/**
 * Login policies.
 *
 * Held per profile rather than per person, so "Tele Sales works ten to seven
 * from the office" is one rule instead of forty copies that drift apart the
 * first time somebody is hired.
 *
 * Three restrictions, and they are enforced in two different places on purpose:
 * the window and the address are checked at the door, because a policy governs
 * who may *start* a session and re-testing the clock on every request would
 * sign somebody out mid-sentence at seven o'clock. The idle limit is a property
 * of the session, so it lives with the session guard and ends a token that has
 * been sitting untouched.
 */
export default function LoginPoliciesPage() {
  const [rows, setRows] = React.useState<LoginPolicy[] | null>(null);
  const [editing, setEditing] = React.useState<LoginPolicy | null>(null);

  const load = React.useCallback(() => {
    accessApi
      .loginPolicies()
      .then(setRows)
      .catch((error: unknown) => {
        toast.error("Could not load login policies", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setRows([]);
      });
  }, []);

  React.useEffect(load, [load]);

  if (!rows) {
    return (
      <PagePanel icon={Lock} title="Login policies">
        <CrmLoadingState label="Loading login policies" />
      </PagePanel>
    );
  }

  const enforced = rows.filter((r) => r.isActive).length;

  return (
    <PagePanel
      icon={Lock}
      title="Login policies"
      hint="When, and from where, each profile may sign in. Platform operators are always exempt."
      subToolbar={
        <p className="text-[12px] text-muted-foreground">
          {enforced === 0
            ? "No profile is restricted — anyone may sign in at any hour from any network."
            : `${enforced} of ${rows.length} profiles are restricted.`}
        </p>
      }
    >
      <div className="overflow-hidden rounded-xl border bg-card shadow-xs">
        <table className="w-full min-w-[820px] text-[12.5px]">
          <thead>
            <tr className="border-b text-[10.5px] tracking-wide text-muted-foreground uppercase">
              <th className="px-4 py-2 text-left font-medium">Profile</th>
              <th className="px-4 py-2 text-left font-medium">Days</th>
              <th className="px-4 py-2 text-left font-medium">Hours</th>
              <th className="px-4 py-2 text-left font-medium">Networks</th>
              <th className="px-4 py-2 text-left font-medium">Idle limit</th>
              <th className="px-4 py-2" />
            </tr>
          </thead>

          <tbody>
            {rows.map((row) => (
              <tr key={row.profileId} className="border-b last:border-0 align-top hover:bg-muted/40">
                <td className="px-4 py-2.5">
                  <p className="font-medium">{row.profileName}</p>
                  <p className="text-[11px] text-muted-foreground">
                    {row.userCount} {row.userCount === 1 ? "person" : "people"}
                  </p>
                </td>

                {row.isActive ? (
                  <>
                    <td className="px-4 py-2.5">
                      <DayDots mask={row.allowedDays} />
                    </td>

                    <td className="px-4 py-2.5 whitespace-nowrap">
                      {row.loginFromMinute === null || row.loginToMinute === null ? (
                        <span className="text-muted-foreground">Any hour</span>
                      ) : (
                        <span className="tabular-nums">
                          {clock(row.loginFromMinute)} – {clock(row.loginToMinute)}
                        </span>
                      )}
                    </td>

                    <td className="max-w-[220px] px-4 py-2.5">
                      {row.allowedIpRanges ? (
                        <span className="font-mono text-[11.5px] break-all">
                          {row.allowedIpRanges}
                        </span>
                      ) : (
                        <span className="text-muted-foreground">Anywhere</span>
                      )}
                    </td>

                    <td className="px-4 py-2.5 whitespace-nowrap">
                      {row.idleTimeoutMinutes ? (
                        <span className="tabular-nums">{row.idleTimeoutMinutes} min</span>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </td>
                  </>
                ) : (
                  <td className="px-4 py-2.5 text-muted-foreground" colSpan={4}>
                    No restriction
                  </td>
                )}

                <td className="px-4 py-2.5 text-right whitespace-nowrap">
                  <Pill tone={row.isActive ? "warning" : "neutral"}>
                    {row.isActive ? "Enforced" : "Off"}
                  </Pill>
                  <Button
                    size="sm"
                    variant="ghost"
                    className="ml-1.5 h-7 px-2 text-[11px]"
                    onClick={() => setEditing(row)}
                  >
                    Edit
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <PolicyDialog
        policy={editing}
        onClose={() => setEditing(null)}
        onSaved={load}
      />
    </PagePanel>
  );
}

/** Seven dots — the week at a glance, which reads faster than "Mon, Tue, Wed…". */
function DayDots({ mask }: { mask: number }) {
  return (
    <span className="flex gap-0.5">
      {DAYS.map((day, index) => {
        const on = (mask & (1 << index)) !== 0;
        return (
          <span
            key={day}
            title={day}
            className={cn(
              "grid size-5 place-items-center rounded text-[9.5px] font-medium",
              on ? "bg-primary/15 text-primary" : "bg-muted text-muted-foreground/50"
            )}
          >
            {day[0]}
          </span>
        );
      })}
    </span>
  );
}

function clock(minutes: number) {
  return `${String(Math.floor(minutes / 60)).padStart(2, "0")}:${String(minutes % 60).padStart(2, "0")}`;
}

function toMinutes(value: string) {
  const [h, m] = value.split(":").map(Number);
  if (Number.isNaN(h) || Number.isNaN(m)) return null;
  return h * 60 + m;
}

function PolicyDialog({
  policy,
  onClose,
  onSaved,
}: {
  policy: LoginPolicy | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [active, setActive] = React.useState(false);
  const [days, setDays] = React.useState(EVERY_DAY);
  const [from, setFrom] = React.useState("");
  const [to, setTo] = React.useState("");
  const [ranges, setRanges] = React.useState("");
  const [idle, setIdle] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  const [seen, setSeen] = React.useState<LoginPolicy | null>(null);

  if (policy !== seen) {
    setSeen(policy);

    if (policy) {
      setActive(policy.isActive);
      setDays(policy.allowedDays);
      setFrom(policy.loginFromMinute === null ? "" : clock(policy.loginFromMinute));
      setTo(policy.loginToMinute === null ? "" : clock(policy.loginToMinute));
      setRanges(policy.allowedIpRanges ?? "");
      setIdle(policy.idleTimeoutMinutes?.toString() ?? "");
    }
  }

  async function save() {
    if (!policy) return;

    setSaving(true);
    try {
      await accessApi.saveLoginPolicy(policy.profileId, {
        allowedIpRanges: ranges.trim() || null,
        loginFromMinute: from ? toMinutes(from) : null,
        loginToMinute: to ? toMinutes(to) : null,
        allowedDays: days,
        idleTimeoutMinutes: Number(idle) || null,
        isActive: active,
      });

      toast.success(active ? "Policy enforced" : "Policy switched off", {
        description: policy.profileName,
      });

      onClose();
      onSaved();
    } catch (error) {
      toast.error("Could not save the policy", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={policy !== null} onOpenChange={(open) => (open ? null : onClose())}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{policy?.profileName}</DialogTitle>
          <DialogDescription>
            Applies to everyone on this profile
            {policy ? ` — ${policy.userCount} ${policy.userCount === 1 ? "person" : "people"}` : ""}.
            Platform operators are exempt, so a policy can always be lifted.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4">
          <label className="flex items-center gap-2.5 rounded-lg border p-3 text-[13px]">
            <Switch checked={active} onCheckedChange={setActive} />
            <span>
              <span className="font-medium">Enforce this policy</span>
              <span className="block text-[12px] text-muted-foreground">
                Off means no restriction at all — any hour, any network.
              </span>
            </span>
          </label>

          <fieldset
            className={cn("grid gap-3 transition-opacity", !active && "pointer-events-none opacity-50")}
          >
            <div className="grid gap-1.5">
              <Label className="flex items-center gap-1.5">
                <Clock className="size-3.5 text-muted-foreground" />
                Days allowed
              </Label>

              <div className="flex flex-wrap items-center gap-1">
                {DAYS.map((day, index) => {
                  const on = (days & (1 << index)) !== 0;
                  return (
                    <Button
                      key={day}
                      type="button"
                      size="sm"
                      variant={on ? "default" : "outline"}
                      className="h-7 w-11 text-[11.5px]"
                      onClick={() => setDays(days ^ (1 << index))}
                    >
                      {day}
                    </Button>
                  );
                })}

                <span className="mx-1 text-muted-foreground">·</span>
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  className="h-7 px-2 text-[11.5px]"
                  onClick={() => setDays(WEEKDAYS)}
                >
                  Weekdays
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  className="h-7 px-2 text-[11.5px]"
                  onClick={() => setDays(EVERY_DAY)}
                >
                  All
                </Button>
              </div>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <div className="grid gap-1.5">
                <Label htmlFor="from">From</Label>
                <Input id="from" type="time" value={from} onChange={(e) => setFrom(e.target.value)} />
              </div>
              <div className="grid gap-1.5">
                <Label htmlFor="to">To</Label>
                <Input id="to" type="time" value={to} onChange={(e) => setTo(e.target.value)} />
              </div>
            </div>

            <p className="-mt-1 text-[11.5px] text-muted-foreground">
              Leave both blank for any hour. A window that ends before it starts is read as an
              overnight shift, so 22:00 – 06:00 works.
            </p>

            <div className="grid gap-1.5">
              <Label htmlFor="ranges" className="flex items-center gap-1.5">
                <Globe className="size-3.5 text-muted-foreground" />
                Networks allowed
              </Label>
              <Input
                id="ranges"
                value={ranges}
                onChange={(e) => setRanges(e.target.value)}
                placeholder="203.0.113.0/24, 198.51.100.7"
                className="font-mono text-[12px]"
              />
              <p className="text-[11.5px] text-muted-foreground">
                Comma separated. CIDR ranges or single addresses. Blank means anywhere — the honest
                default for a field team on mobile data.
              </p>
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="idle" className="flex items-center gap-1.5">
                <Timer className="size-3.5 text-muted-foreground" />
                Sign out after idle (minutes)
              </Label>
              <Input
                id="idle"
                inputMode="numeric"
                value={idle}
                onChange={(e) => setIdle(e.target.value)}
                placeholder="Blank uses the token's own life"
                className="max-w-[220px] tabular-nums"
              />
            </div>
          </fieldset>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={save} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : <Lock className="size-4" />}
            Save policy
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
