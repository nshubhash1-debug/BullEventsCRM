"use client";

import * as React from "react";
import { Download, FileSpreadsheet, Loader2, ShieldCheck } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { RuleCard } from "@/components/admin/rule-list";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { getUsers, type UserListItem } from "@/lib/api";
import { downloadLeadExport } from "@/lib/data-admin-api";

const STAGES = [
  "New", "Contacted", "Qualified", "ObmVisit", "SiteVisit",
  "FollowUp", "Negotiation", "Booked", "Lost",
];

const COLUMNS = [
  "Name", "Phone", "Email", "City", "State", "Source", "Stage", "Priority",
  "Requirement", "Configuration", "Budget from", "Budget to", "Owner", "Branch", "Created",
];

/**
 * Export.
 *
 * The export is scoped to what the person running it can already see, which is
 * the only responsible way to build one: an export is the easiest way to walk
 * out of a business with its book, and one that ignored record visibility would
 * hand everything to anybody who could reach the screen.
 *
 * That is stated on the page rather than left implicit, because somebody who
 * exports 900 rows and expected 8,000 should understand why before they raise
 * it as a bug.
 */
export default function ExportPage() {
  const [stage, setStage] = React.useState("");
  const [ownerId, setOwnerId] = React.useState("");
  const [users, setUsers] = React.useState<UserListItem[]>([]);
  const [running, setRunning] = React.useState(false);

  React.useEffect(() => {
    getUsers()
      .then((rows) => setUsers(rows.filter((u) => u.isActive)))
      .catch(() => setUsers([]));
  }, []);

  async function run() {
    setRunning(true);
    try {
      await downloadLeadExport({
        stage: stage || undefined,
        ownerId: ownerId ? Number(ownerId) : undefined,
      });

      toast.success("Export downloaded");
    } catch (error) {
      toast.error("Could not export", {
        description: error instanceof Error ? error.message : "Network error.",
      });
    } finally {
      setRunning(false);
    }
  }

  return (
    <PagePanel
      icon={FileSpreadsheet}
      title="Export"
      hint="Leads as a CSV, scoped to what you can already see."
    >
      <div className="flex flex-col gap-4">
        <RuleCard icon={FileSpreadsheet} title="Leads">
          <div className="grid gap-3 px-4 py-4">
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="grid gap-1.5">
                <Label>Stage</Label>
                <Select value={stage} onValueChange={setStage}>
                  <SelectTrigger>
                    <SelectValue placeholder="Every stage" />
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
                <Label>Owner</Label>
                <Select value={ownerId} onValueChange={setOwnerId}>
                  <SelectTrigger>
                    <SelectValue placeholder="Anyone" />
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

            <div className="flex flex-wrap items-center gap-2">
              <Button className="gap-1.5" disabled={running} onClick={run}>
                {running ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <Download className="size-4" />
                )}
                Download CSV
              </Button>

              {stage || ownerId ? (
                <Button
                  variant="ghost"
                  size="sm"
                  className="text-[12px]"
                  onClick={() => {
                    setStage("");
                    setOwnerId("");
                  }}
                >
                  Clear filters
                </Button>
              ) : null}
            </div>

            <p className="text-[11.5px] text-muted-foreground">
              Up to 50,000 rows, newest first. Saved as UTF-8 with a byte-order mark so Excel
              opens names with accents correctly rather than mangling them.
            </p>
          </div>
        </RuleCard>

        <RuleCard icon={ShieldCheck} title="What comes out">
          <div className="px-4 py-3">
            <div className="flex flex-wrap gap-1.5">
              {COLUMNS.map((column) => (
                <span
                  key={column}
                  className="rounded border bg-muted/40 px-1.5 py-0.5 text-[11.5px]"
                >
                  {column}
                </span>
              ))}
            </div>

            <p className="mt-3 text-[12.5px] text-muted-foreground">
              The file contains only the leads your own access already reaches. A sales executive
              exporting from this screen gets their own pipeline; somebody who can see the whole
              company gets the whole company. Restricted fields are not included.
            </p>
          </div>
        </RuleCard>
      </div>
    </PagePanel>
  );
}
