"use client";

import * as React from "react";
import {
  AlertTriangle,
  ChevronDown,
  ChevronRight,
  Loader2,
  Search,
  UserMinus,
  UsersRound,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  accessApi,
  ApiError,
  type TeamMember,
  type TeamsOverview,
  type TeamSummary,
} from "@/lib/api";
import { cn } from "@/lib/utils";

const SCOPE_TONE: Record<string, string> = {
  Own: "text-slate-600 dark:text-slate-300",
  Team: "text-sky-600 dark:text-sky-400",
  Company: "text-amber-600 dark:text-amber-400",
  Platform: "text-rose-600 dark:text-rose-400",
};

/** The sentinel the destination picker uses for "off the tree". */
const NO_MANAGER = "none";

export default function TeamsPage() {
  const [overview, setOverview] = React.useState<TeamsOverview | null>(null);
  const [search, setSearch] = React.useState("");
  const [selected, setSelected] = React.useState<Set<number>>(new Set());
  const [collapsed, setCollapsed] = React.useState<Set<number>>(new Set());
  const [destination, setDestination] = React.useState<string>(NO_MANAGER);
  const [busy, setBusy] = React.useState(false);

  React.useEffect(() => {
    accessApi
      .teams()
      .then(setOverview)
      .catch((error: unknown) => {
        toast.error("Could not load teams", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setOverview({ teams: [], unassigned: [] });
      });
  }, []);

  const teams = overview?.teams ?? [];
  const unassigned = overview?.unassigned ?? [];

  /** Everyone a batch could be moved under — every manager plus every member. */
  const destinations = React.useMemo(() => {
    const seen = new Map<number, string>();

    for (const team of teams) {
      seen.set(team.managerId, `${team.managerName} · ${team.managerRoleName}`);
      for (const member of team.members) {
        seen.set(member.id, `${member.name} · ${member.roleName}`);
      }
    }
    for (const person of unassigned) {
      seen.set(person.id, `${person.name} · ${person.roleName}`);
    }

    return [...seen.entries()]
      .map(([id, label]) => ({ id, label }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }, [teams, unassigned]);

  const needle = search.trim().toLowerCase();

  function matches(person: { name: string; email: string; roleName: string }) {
    if (!needle) return true;
    return `${person.name} ${person.email} ${person.roleName}`
      .toLowerCase()
      .includes(needle);
  }

  function toggleMember(id: number, on: boolean) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (on) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  function toggleTeam(team: TeamSummary, on: boolean) {
    setSelected((prev) => {
      const next = new Set(prev);
      for (const member of team.members) {
        if (on) next.add(member.id);
        else next.delete(member.id);
      }
      return next;
    });
  }

  async function move(managerId: number | null) {
    setBusy(true);
    try {
      const next = await accessApi.reassign([...selected], managerId);
      setOverview(next);
      setSelected(new Set());
      toast.success(
        managerId === null
          ? "Moved off the reporting line"
          : "Reporting lines updated"
      );
    } catch (error) {
      toast.error("Could not move those people", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(false);
    }
  }

  const headcount =
    teams.reduce((total, team) => total + team.directReports, 0) +
    unassigned.length;

  return (
    <PagePanel
      icon={UsersRound}
      title="Teams"
      hint="A team is a manager and everyone beneath them on the reporting line — the same line that decides what a team-scoped role can see. Move people between managers here rather than editing one person at a time."
      actions={
        <Button
          size="sm"
          variant="outline"
          className="h-8"
          onClick={() => setCollapsed(new Set())}
        >
          Expand all
        </Button>
      }
      toolbar={
        <>
          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Name, email, role…"
              className="h-8 w-64 pl-8 text-[13px]"
            />
          </div>
          <span className="text-[12px] text-muted-foreground tabular-nums">
            {overview
              ? `${teams.length} ${teams.length === 1 ? "team" : "teams"} · ${headcount} people placed · ${unassigned.length} unassigned`
              : "Loading…"}
          </span>
        </>
      }
      subToolbar={
        selected.size > 0 ? (
          <>
            <span className="font-medium text-foreground tabular-nums">
              {selected.size} selected
            </span>
            <span>Move under</span>
            <Select value={destination} onValueChange={setDestination}>
              <SelectTrigger className="h-7 w-64 text-[12px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NO_MANAGER}>
                  <span className="text-muted-foreground">
                    Nobody — top of the line
                  </span>
                </SelectItem>
                {destinations
                  .filter((option) => !selected.has(option.id))
                  .map((option) => (
                    <SelectItem key={option.id} value={String(option.id)}>
                      {option.label}
                    </SelectItem>
                  ))}
              </SelectContent>
            </Select>
            <Button
              size="sm"
              className="h-7"
              disabled={busy}
              onClick={() =>
                void move(
                  destination === NO_MANAGER ? null : Number(destination)
                )
              }
            >
              {busy ? <Loader2 className="animate-spin" /> : null}
              Move
            </Button>
            <Button
              size="sm"
              variant="ghost"
              className="h-7"
              onClick={() => setSelected(new Set())}
            >
              Clear
            </Button>
          </>
        ) : undefined
      }
    >
      {overview === null ? (
        <CrmLoadingState label="Loading teams" />
      ) : teams.length === 0 && unassigned.length === 0 ? (
        <p className="text-[12.5px] text-muted-foreground">
          Nobody to show yet. Invite a teammate on the Users screen first.
        </p>
      ) : (
        <div className="flex flex-col gap-3">
          {teams.map((team) => (
            <TeamCard
              key={team.managerId}
              team={team}
              open={!collapsed.has(team.managerId)}
              onToggle={() =>
                setCollapsed((prev) => {
                  const next = new Set(prev);
                  if (!next.delete(team.managerId)) next.add(team.managerId);
                  return next;
                })
              }
              selected={selected}
              onToggleMember={toggleMember}
              onToggleTeam={toggleTeam}
              matches={matches}
            />
          ))}

          {unassigned.length > 0 ? (
            <UnassignedCard
              people={unassigned.filter(matches)}
              total={unassigned.length}
              selected={selected}
              onToggleMember={toggleMember}
            />
          ) : null}
        </div>
      )}
    </PagePanel>
  );
}

/* ------------------------------------------------------------------ *
 * One team
 * ------------------------------------------------------------------ */

function TeamCard({
  team,
  open,
  onToggle,
  selected,
  onToggleMember,
  onToggleTeam,
  matches,
}: {
  team: TeamSummary;
  open: boolean;
  onToggle: () => void;
  selected: Set<number>;
  onToggleMember: (id: number, on: boolean) => void;
  onToggleTeam: (team: TeamSummary, on: boolean) => void;
  matches: (person: {
    name: string;
    email: string;
    roleName: string;
  }) => boolean;
}) {
  const visible = team.members.filter(matches);
  const managerMatches = matches({
    name: team.managerName,
    email: team.managerEmail,
    roleName: team.managerRoleName,
  });

  if (visible.length === 0 && !managerMatches) return null;

  const chosen = team.members.filter((member) => selected.has(member.id)).length;

  return (
    <div className="overflow-hidden rounded-lg border">
      <div className="flex flex-wrap items-center gap-2 border-b bg-muted/40 px-3 py-2">
        <button
          type="button"
          aria-label={open ? `Collapse ${team.managerName}` : `Expand ${team.managerName}`}
          onClick={onToggle}
          className="flex size-5 shrink-0 items-center justify-center rounded text-muted-foreground hover:bg-accent"
        >
          {open ? (
            <ChevronDown className="size-3.5" />
          ) : (
            <ChevronRight className="size-3.5" />
          )}
        </button>

        <Checkbox
          aria-label={`Select everyone under ${team.managerName}`}
          checked={
            chosen === team.members.length && team.members.length > 0
              ? true
              : chosen > 0
                ? "indeterminate"
                : false
          }
          onCheckedChange={(value) => onToggleTeam(team, value === true)}
        />

        <Avatar className="size-7 shrink-0">
          <AvatarFallback className="bg-primary/10 text-[10px] text-primary">
            {initials(team.managerName)}
          </AvatarFallback>
        </Avatar>

        <div className="min-w-0 flex-1">
          <p
            className={cn(
              "truncate text-[13px] font-semibold",
              team.managerIsActive ? "" : "text-muted-foreground line-through"
            )}
          >
            {team.managerName}
          </p>
          <p className="truncate text-[11px] text-muted-foreground">
            {team.managerRoleName} · {team.managerEmail}
          </p>
        </div>

        {!team.scopeCoversTeam ? (
          <Tooltip>
            <TooltipTrigger asChild>
              <Badge
                variant="outline"
                className="h-5 shrink-0 cursor-help gap-1 px-1.5 text-[11px] font-normal text-amber-600 dark:text-amber-400"
              >
                <AlertTriangle className="size-3" />
                Scope too narrow
              </Badge>
            </TooltipTrigger>
            <TooltipContent className="max-w-72">
              {team.managerName} carries {team.totalMembers}{" "}
              {team.totalMembers === 1 ? "person" : "people"} but holds a{" "}
              {team.scopeLabel.toLowerCase()} seat, so the reporting line beneath
              them grants no access. Move them to a team-scoped role, or move the
              reports elsewhere.
            </TooltipContent>
          </Tooltip>
        ) : (
          <Badge
            variant="outline"
            className={cn(
              "h-5 shrink-0 px-1.5 text-[11px] font-normal",
              SCOPE_TONE[team.scope]
            )}
          >
            {team.scopeLabel}
          </Badge>
        )}

        <Stat label="Direct" value={team.directReports} />
        <Stat label="Total" value={team.totalMembers} />
        <Stat label="Levels" value={team.depth} />
        {team.inactiveMembers > 0 ? (
          <Stat label="Inactive" value={team.inactiveMembers} muted />
        ) : null}
      </div>

      {open ? (
        <div className="divide-y">
          {visible.map((member) => (
            <MemberRow
              key={member.id}
              member={member}
              checked={selected.has(member.id)}
              onToggle={onToggleMember}
            />
          ))}
        </div>
      ) : null}
    </div>
  );
}

function MemberRow({
  member,
  checked,
  onToggle,
}: {
  member: TeamMember;
  checked: boolean;
  onToggle: (id: number, on: boolean) => void;
}) {
  return (
    <div
      className="flex flex-wrap items-center gap-2 py-1.5 pr-3 hover:bg-accent/40"
      // Indented by depth so a sub-team inside the team stays readable.
      style={{ paddingLeft: `${1.75 + (member.depth - 1) * 1.25}rem` }}
    >
      <Checkbox
        aria-label={`Select ${member.name}`}
        checked={checked}
        onCheckedChange={(value) => onToggle(member.id, value === true)}
      />

      <div className="min-w-0 flex-1">
        <p
          className={cn(
            "truncate text-[13px]",
            member.isActive ? "" : "text-muted-foreground line-through"
          )}
        >
          {member.name}
        </p>
        <p className="truncate text-[11px] text-muted-foreground">
          {member.email}
        </p>
      </div>

      {member.branches.length > 0 ? (
        <span className="hidden shrink-0 text-[11px] text-muted-foreground sm:inline">
          {member.branches.join(", ")}
        </span>
      ) : null}

      {member.teamSize > 0 ? (
        <Badge
          variant="secondary"
          className="h-5 shrink-0 px-1.5 text-[10.5px] tabular-nums"
          title={`${member.teamSize} people beneath ${member.name}`}
        >
          +{member.teamSize}
        </Badge>
      ) : null}

      <Badge
        variant="outline"
        className="h-5 w-32 shrink-0 justify-center px-1.5 text-[11px] font-normal"
      >
        {member.roleName}
      </Badge>
    </div>
  );
}

function UnassignedCard({
  people,
  total,
  selected,
  onToggleMember,
}: {
  people: TeamMember[];
  total: number;
  selected: Set<number>;
  onToggleMember: (id: number, on: boolean) => void;
}) {
  return (
    <div className="overflow-hidden rounded-lg border border-dashed">
      <div className="flex items-center gap-2 border-b border-dashed bg-muted/20 px-3 py-2">
        <UserMinus className="size-4 text-muted-foreground" />
        <div className="min-w-0 flex-1">
          <p className="text-[13px] font-semibold">On no team</p>
          <p className="text-[11px] text-muted-foreground">
            {total} {total === 1 ? "person reports" : "people report"} to nobody
            and carry nobody. Nothing is broken — but nobody above them sees
            their records either.
          </p>
        </div>
      </div>

      <div className="divide-y">
        {people.map((person) => (
          <MemberRow
            key={person.id}
            member={{ ...person, depth: 1 }}
            checked={selected.has(person.id)}
            onToggle={onToggleMember}
          />
        ))}
      </div>
    </div>
  );
}

function Stat({
  label,
  value,
  muted = false,
}: {
  label: string;
  value: number;
  muted?: boolean;
}) {
  return (
    <span
      className={cn(
        "hidden shrink-0 flex-col items-end leading-tight sm:flex",
        muted ? "text-muted-foreground" : ""
      )}
    >
      <span className="text-[13px] font-semibold tabular-nums">{value}</span>
      <span className="text-[10px] tracking-wide text-muted-foreground uppercase">
        {label}
      </span>
    </span>
  );
}

function initials(name: string) {
  return name
    .split(" ")
    .map((part) => part[0])
    .slice(0, 2)
    .join("")
    .toUpperCase();
}
