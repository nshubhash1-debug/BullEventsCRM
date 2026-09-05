"use client";

import * as React from "react";
import {
  ChevronDown,
  ChevronRight,
  CornerDownRight,
  Loader2,
  Search,
  Users,
  Workflow,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { accessApi, ApiError, type HierarchyNode } from "@/lib/api";
import { cn } from "@/lib/utils";

const SCOPE_TONE: Record<string, string> = {
  Own: "text-slate-600 dark:text-slate-300",
  Team: "text-sky-600 dark:text-sky-400",
  Company: "text-amber-600 dark:text-amber-400",
  Platform: "text-rose-600 dark:text-rose-400",
};

/** The sentinel the manager picker uses for "reports to nobody". */
const NO_MANAGER = "none";

export default function HierarchyPage() {
  const [tree, setTree] = React.useState<HierarchyNode[] | null>(null);
  const [search, setSearch] = React.useState("");
  const [collapsed, setCollapsed] = React.useState<Set<number>>(new Set());
  const [saving, setSaving] = React.useState<number | null>(null);

  const load = React.useCallback(() => {
    accessApi
      .hierarchy()
      .then(setTree)
      .catch((error: unknown) => {
        toast.error("Could not load the reporting lines", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setTree([]);
      });
  }, []);

  React.useEffect(load, [load]);

  /** Everyone, flattened — the manager picker and the counters both want a list. */
  const everyone = React.useMemo(() => flatten(tree ?? []), [tree]);

  /**
   * Ids to keep while searching: the matches themselves plus every ancestor,
   * because a match five levels down is unreachable if the branches above it
   * are filtered away.
   */
  const visible = React.useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle || !tree) return null;

    const keep = new Set<number>();

    function walk(node: HierarchyNode, ancestors: number[]) {
      const hit = `${node.name} ${node.email} ${node.roleName}`
        .toLowerCase()
        .includes(needle);

      if (hit) {
        keep.add(node.id);
        for (const id of ancestors) keep.add(id);
      }

      for (const report of node.reports) walk(report, [...ancestors, node.id]);
    }

    for (const root of tree) walk(root, []);
    return keep;
  }, [search, tree]);

  async function setManager(userId: number, managerId: number | null) {
    setSaving(userId);
    try {
      await accessApi.setManager(userId, managerId);
      // Reloaded rather than patched in place: moving one person re-parents
      // their whole branch and changes every team size above both the old and
      // the new manager. Recomputing that client-side is the server's walk,
      // written a second time.
      const next = await accessApi.hierarchy();
      setTree(next);
      toast.success("Reporting line updated");
    } catch (error) {
      toast.error("Could not change the reporting line", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(null);
    }
  }

  function toggle(id: number) {
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  }

  const unmanaged = everyone.filter((node) => node.managerId === null).length;

  return (
    <PagePanel
      icon={Workflow}
      title="Reporting lines"
      hint="Who reports to whom. A team-scoped role — an AGM — sees their own records plus everyone beneath them here, to any depth, so this tree is what decides their reach."
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
              className="h-8 w-56 pl-8 text-[13px]"
            />
          </div>
          <span className="text-[12px] text-muted-foreground tabular-nums">
            {tree
              ? `${everyone.length} people · ${unmanaged} at the top of a line`
              : "Loading…"}
          </span>
        </>
      }
    >
      {tree === null ? (
        <CrmLoadingState label="Loading reporting lines" />
      ) : tree.length === 0 ? (
        <p className="text-[12.5px] text-muted-foreground">
          Nobody to show yet. Invite a teammate on the Users screen and they will
          appear here.
        </p>
      ) : (
        <div className="flex flex-col">
          {tree.map((root) => (
            <NodeRow
              key={root.id}
              node={root}
              depth={0}
              everyone={everyone}
              collapsed={collapsed}
              onToggle={toggle}
              visible={visible}
              saving={saving}
              onSetManager={setManager}
            />
          ))}
        </div>
      )}
    </PagePanel>
  );
}

function NodeRow({
  node,
  depth,
  everyone,
  collapsed,
  onToggle,
  visible,
  saving,
  onSetManager,
}: {
  node: HierarchyNode;
  depth: number;
  everyone: HierarchyNode[];
  collapsed: Set<number>;
  onToggle: (id: number) => void;
  visible: Set<number> | null;
  saving: number | null;
  onSetManager: (userId: number, managerId: number | null) => void;
}) {
  if (visible && !visible.has(node.id)) return null;

  const hasReports = node.reports.length > 0;
  const isOpen = !collapsed.has(node.id);

  return (
    <>
      <div
        className="flex flex-wrap items-center gap-2 border-b py-1.5 pr-2 last:border-b-0 hover:bg-accent/40"
        style={{ paddingLeft: `${depth * 1.25}rem` }}
      >
        {hasReports ? (
          <button
            type="button"
            aria-label={isOpen ? `Collapse ${node.name}` : `Expand ${node.name}`}
            onClick={() => onToggle(node.id)}
            className="flex size-5 shrink-0 items-center justify-center rounded text-muted-foreground hover:bg-accent"
          >
            {isOpen ? (
              <ChevronDown className="size-3.5" />
            ) : (
              <ChevronRight className="size-3.5" />
            )}
          </button>
        ) : (
          <span className="flex size-5 shrink-0 items-center justify-center text-muted-foreground/40">
            {depth > 0 ? <CornerDownRight className="size-3" /> : null}
          </span>
        )}

        <Avatar className="size-7 shrink-0">
          <AvatarFallback className="bg-primary/10 text-[10px] text-primary">
            {initials(node.name)}
          </AvatarFallback>
        </Avatar>

        <div className="min-w-0 flex-1">
          <p
            className={cn(
              "truncate text-[13px] font-medium",
              node.isActive ? "text-primary" : "text-muted-foreground line-through"
            )}
          >
            {node.name}
          </p>
          <p className="truncate text-[11px] text-muted-foreground">
            {node.email}
          </p>
        </div>

        <Badge
          variant="outline"
          className={cn(
            "h-5 shrink-0 px-1.5 text-[11px] font-normal",
            SCOPE_TONE[node.scope]
          )}
        >
          {node.roleName}
        </Badge>

        {node.teamSize > 0 ? (
          <Badge
            variant="secondary"
            className="h-5 shrink-0 gap-1 px-1.5 text-[11px] tabular-nums"
            title={`${node.teamSize} people beneath ${node.name}`}
          >
            <Users className="size-3" />
            {node.teamSize}
          </Badge>
        ) : null}

        <ManagerPicker
          node={node}
          everyone={everyone}
          saving={saving === node.id}
          onChange={onSetManager}
        />
      </div>

      {isOpen
        ? node.reports.map((report) => (
            <NodeRow
              key={report.id}
              node={report}
              depth={depth + 1}
              everyone={everyone}
              collapsed={collapsed}
              onToggle={onToggle}
              visible={visible}
              saving={saving}
              onSetManager={onSetManager}
            />
          ))
        : null}
    </>
  );
}

/**
 * Who this person reports to.
 *
 * Their own subtree is filtered out of the options: pointing somebody at one of
 * their own reports closes the line into a ring, and the server rejects it. It
 * is a better experience to never offer the choice than to explain the refusal
 * afterwards.
 */
function ManagerPicker({
  node,
  everyone,
  saving,
  onChange,
}: {
  node: HierarchyNode;
  everyone: HierarchyNode[];
  saving: boolean;
  onChange: (userId: number, managerId: number | null) => void;
}) {
  const forbidden = React.useMemo(() => {
    const ids = new Set<number>([node.id]);
    const walk = (current: HierarchyNode) => {
      for (const report of current.reports) {
        ids.add(report.id);
        walk(report);
      }
    };
    walk(node);
    return ids;
  }, [node]);

  const options = everyone
    .filter((person) => !forbidden.has(person.id))
    .sort((a, b) => a.name.localeCompare(b.name));

  return (
    <div className="flex shrink-0 items-center gap-1.5">
      {saving ? (
        <Loader2 className="size-3.5 animate-spin text-muted-foreground" />
      ) : null}
      <Select
        value={node.managerId === null ? NO_MANAGER : String(node.managerId)}
        disabled={saving}
        onValueChange={(value) =>
          onChange(node.id, value === NO_MANAGER ? null : Number(value))
        }
      >
        <SelectTrigger
          className="h-7 w-48 text-[12px]"
          aria-label={`Manager for ${node.name}`}
        >
          <SelectValue placeholder="Reports to…" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={NO_MANAGER}>
            <span className="text-muted-foreground">Top of the line</span>
          </SelectItem>
          {options.map((person) => (
            <SelectItem key={person.id} value={String(person.id)}>
              <span className="flex flex-col">
                <span>{person.name}</span>
                <span className="text-[11px] text-muted-foreground">
                  {person.roleName}
                </span>
              </span>
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}

function flatten(nodes: HierarchyNode[]): HierarchyNode[] {
  return nodes.flatMap((node) => [node, ...flatten(node.reports)]);
}

function initials(name: string) {
  return name
    .split(" ")
    .map((part) => part[0])
    .slice(0, 2)
    .join("")
    .toUpperCase();
}
