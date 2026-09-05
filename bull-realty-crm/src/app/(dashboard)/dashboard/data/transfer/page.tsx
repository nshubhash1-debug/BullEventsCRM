"use client";

import * as React from "react";
import { ArrowRight, Loader2, Search, TriangleAlert, Users } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { TONE_TEXT } from "@/components/crm/metrics";
import { RuleCard } from "@/components/admin/rule-list";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ApiError, getUsers, type UserListItem } from "@/lib/api";
import { dataAdminApi, type MassTransferResult } from "@/lib/data-admin-api";
import { cn } from "@/lib/utils";

const UNOWNED = "__unowned";

const STAGES = [
  "New", "Contacted", "Qualified", "ObmVisit", "SiteVisit",
  "FollowUp", "Negotiation", "Booked", "Lost",
];

/**
 * Mass transfer.
 *
 * Preview first, always. A transfer is not reversible in any useful sense —
 * once four hundred leads have changed hands the previous owner is not recorded
 * on them — so the screen shows what would move, and how many, before the
 * button that moves them is enabled.
 *
 * The commonest real use is somebody leaving: their open pipeline has to go
 * somewhere the same day, and "everything owned by X" is exactly the filter
 * that needs to be one click.
 */
export default function MassTransferPage() {
  const [users, setUsers] = React.useState<UserListItem[]>([]);
  const [from, setFrom] = React.useState("");
  const [to, setTo] = React.useState("");
  const [stage, setStage] = React.useState("");
  const [source, setSource] = React.useState("");
  const [city, setCity] = React.useState("");

  const [preview, setPreview] = React.useState<MassTransferResult | null>(null);
  const [busy, setBusy] = React.useState(false);

  React.useEffect(() => {
    getUsers()
      .then((rows) => setUsers(rows.filter((u) => u.isActive)))
      .catch(() => setUsers([]));
  }, []);

  const body = React.useCallback(
    (isPreview: boolean) => ({
      object: "Lead",
      fromUserId: from === UNOWNED ? null : Number(from) || null,
      toUserId: Number(to),
      stage: stage || null,
      source: source.trim() || null,
      city: city.trim() || null,
      preview: isPreview,
    }),
    [from, to, stage, source, city]
  );

  // Any change to the filter invalidates the preview: moving a set that is no
  // longer the set somebody looked at is exactly the mistake this screen exists
  // to prevent.
  //
  // Compared during render rather than cleared from an effect, so the stale
  // preview never paints for a frame after the filter has already changed.
  const signature = [from, to, stage, source, city].join("|");
  const [previewedFor, setPreviewedFor] = React.useState(signature);

  if (signature !== previewedFor) {
    setPreviewedFor(signature);
    setPreview(null);
  }

  async function check() {
    if (!from || !to) {
      toast.error("Choose who the records are moving from and to.");
      return;
    }

    setBusy(true);
    try {
      setPreview(await dataAdminApi.massTransfer(body(true)));
      setPreviewedFor(signature);
    } catch (error) {
      toast.error("Could not check", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(false);
    }
  }

  async function move() {
    if (!preview) return;

    const toName = users.find((u) => String(u.id) === to)?.name ?? "them";

    if (!window.confirm(`Move ${preview.matched} leads to ${toName}? This cannot be undone.`)) {
      return;
    }

    setBusy(true);
    try {
      const result = await dataAdminApi.massTransfer(body(false));

      toast.success(`${result.moved} leads moved`, { description: `Now owned by ${toName}.` });
      setPreview(null);
    } catch (error) {
      toast.error("The transfer failed", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(false);
    }
  }

  return (
    <PagePanel
      icon={Users}
      title="Mass transfer"
      hint="Move ownership of many leads at once. Always previewed before anything changes."
    >
      <div className="flex flex-col gap-4">
        <RuleCard icon={Users} title="Which records">
          <div className="grid gap-3 px-4 py-4">
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="grid gap-1.5">
                <Label>Currently owned by</Label>
                <Select value={from} onValueChange={setFrom}>
                  <SelectTrigger>
                    <SelectValue placeholder="Choose a person" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={UNOWNED}>Nobody — the unassigned queue</SelectItem>
                    {users.map((user) => (
                      <SelectItem key={user.id} value={String(user.id)}>
                        {user.name} · {user.role}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="grid gap-1.5">
                <Label>Move to</Label>
                <Select value={to} onValueChange={setTo}>
                  <SelectTrigger>
                    <SelectValue placeholder="Choose a person" />
                  </SelectTrigger>
                  <SelectContent>
                    {users.map((user) => (
                      <SelectItem key={user.id} value={String(user.id)}>
                        {user.name} · {user.role}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              <div className="grid gap-1.5">
                <Label>Stage</Label>
                <Select value={stage} onValueChange={setStage}>
                  <SelectTrigger>
                    <SelectValue placeholder="Any stage" />
                  </SelectTrigger>
                  <SelectContent>
                    {STAGES.map((s) => (
                      <SelectItem key={s} value={s}>
                        {s}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="mt-source">Source</Label>
                <Input
                  id="mt-source"
                  value={source}
                  onChange={(e) => setSource(e.target.value)}
                  placeholder="Any"
                />
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="mt-city">City</Label>
                <Input
                  id="mt-city"
                  value={city}
                  onChange={(e) => setCity(e.target.value)}
                  placeholder="Any"
                />
              </div>
            </div>

            <div>
              <Button variant="outline" className="gap-1.5" disabled={busy} onClick={check}>
                {busy && !preview ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <Search className="size-4" />
                )}
                Check what would move
              </Button>
            </div>
          </div>
        </RuleCard>

        {preview ? (
          <RuleCard
            icon={TriangleAlert}
            title="What would move"
            count={preview.matched}
            hint={preview.matched === 1 ? "lead" : "leads"}
          >
            <div className="grid gap-3 px-4 py-4">
              {preview.matched === 0 ? (
                <p className="text-[13px] text-muted-foreground">
                  Nothing matches that filter. Nothing would move.
                </p>
              ) : (
                <>
                  <p className="flex flex-wrap items-center gap-2 text-[13px]">
                    <span className="font-semibold tabular-nums">{preview.matched}</span>
                    leads move from
                    <span className="font-medium">
                      {from === UNOWNED
                        ? "the unassigned queue"
                        : (users.find((u) => String(u.id) === from)?.name ?? "—")}
                    </span>
                    <ArrowRight className="size-3.5 text-muted-foreground" />
                    <span className="font-medium">
                      {users.find((u) => String(u.id) === to)?.name ?? "—"}
                    </span>
                  </p>

                  {preview.sample.length > 0 ? (
                    <div>
                      <p className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
                        For example
                      </p>
                      <ul className="mt-1 space-y-0.5">
                        {preview.sample.map((name) => (
                          <li key={name} className="text-[12.5px] text-muted-foreground">
                            {name}
                          </li>
                        ))}
                      </ul>
                    </div>
                  ) : null}

                  <p
                    className={cn(
                      "flex items-start gap-2 rounded-md border border-amber-500/25 bg-amber-500/10 px-3 py-2 text-[12.5px]",
                      TONE_TEXT.warning
                    )}
                  >
                    <TriangleAlert className="mt-0.5 size-3.5 shrink-0" />
                    The previous owner is not recorded on the moved records, so this cannot be
                    reversed by running it backwards.
                  </p>

                  <div>
                    <Button className="gap-1.5" disabled={busy} onClick={move}>
                      {busy ? (
                        <Loader2 className="size-4 animate-spin" />
                      ) : (
                        <Users className="size-4" />
                      )}
                      Move {preview.matched} leads
                    </Button>
                  </div>
                </>
              )}
            </div>
          </RuleCard>
        ) : null}
      </div>
    </PagePanel>
  );
}
