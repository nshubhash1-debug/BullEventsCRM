"use client";

import * as React from "react";
import Link from "next/link";
import {
  CalendarDays,
  Check,
  LayoutDashboard,
  MoreHorizontal,
  Pencil,
  Plus,
  RefreshCw,
  RotateCcw,
  Share2,
  Target,
  Trash2,
  X,
} from "lucide-react";
import { toast } from "sonner";

import { DashboardGrid } from "@/components/dashboard/dashboard-grid";
import {
  AddWidgetDialog,
  NewDashboardDialog,
} from "@/components/dashboard/dashboard-dialogs";
import { WidgetEditor } from "@/components/dashboard/widget-editor";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { PERIODS, type PeriodKey } from "@/lib/dashboard/analytics";
import { DEFAULT_DASHBOARDS } from "@/lib/dashboard/presets";
import {
  normaliseWidget,
  type DashboardVersion,
  type WidgetDef,
} from "@/lib/dashboard/types";
import { usePersistedState } from "@/lib/persisted-store";
import { cn } from "@/lib/utils";

const DASHBOARDS_KEY = "brg.dashboards.v2";
const ACTIVE_KEY = "brg.activeDashboard.v2";
const PERIOD_KEY = "brg.dashboardPeriod.v2";

export default function DashboardPage() {
  const [stored, setDashboards] = usePersistedState<DashboardVersion[]>(
    DASHBOARDS_KEY,
    DEFAULT_DASHBOARDS
  );
  const [activeId, setActiveId] = usePersistedState<string>(
    ACTIVE_KEY,
    DEFAULT_DASHBOARDS[0].id
  );
  const [period, setPeriod] = usePersistedState<PeriodKey>(
    PERIOD_KEY,
    "last12Months"
  );

  const [editing, setEditing] = React.useState(false);
  const [draft, setDraft] = React.useState<WidgetDef[]>([]);
  const [newDashboardOpen, setNewDashboardOpen] = React.useState(false);
  const [editingWidget, setEditingWidget] = React.useState<WidgetDef | null>(null);
  const [refreshToken, setRefreshToken] = React.useState(0);
  const [refreshedAt, setRefreshedAt] = React.useState<string | null>(null);

  // Layouts live in the browser, so anything read back may predate a field the
  // widget model has since grown. Normalising on read is what keeps a saved
  // dashboard from breaking after an update.
  const dashboards = React.useMemo(
    () =>
      stored.map((dashboard) => ({
        ...dashboard,
        widgets: (dashboard.widgets ?? []).map(normaliseWidget),
      })),
    [stored]
  );

  const active =
    dashboards.find((dashboard) => dashboard.id === activeId) ?? dashboards[0];
  const widgets = editing ? draft : active.widgets;

  const periodLabel =
    PERIODS.find((entry) => entry.value === period)?.label ?? "All time";
  const scopeLabel = `${active.name} · ${periodLabel}`;

  /* ---------------- dashboard actions ---------------- */

  function startEditing() {
    setDraft(active.widgets);
    setEditing(true);
  }

  function saveEditing() {
    setDashboards(
      dashboards.map((dashboard) =>
        dashboard.id === active.id ? { ...dashboard, widgets: draft } : dashboard
      )
    );
    setEditing(false);
    toast.success("Dashboard saved", {
      description: `${draft.length} widget${draft.length === 1 ? "" : "s"} on ${active.name}.`,
    });
  }

  function cancelEditing() {
    setEditing(false);
    setDraft([]);
  }

  function switchDashboard(id: string) {
    setEditing(false);
    setDraft([]);
    setActiveId(id);
  }

  /**
   * Writes a widget back into whichever list is live.
   *
   * In builder mode that is the unsaved draft; outside it, the saved dashboard.
   * Editing a widget from the card menu in view mode should stick without
   * making someone enter builder mode first, so both paths are supported.
   */
  function commitWidget(next: WidgetDef) {
    if (editing) {
      setDraft((current) => {
        const exists = current.some((widget) => widget.id === next.id);
        return exists
          ? current.map((widget) => (widget.id === next.id ? next : widget))
          : [...current, next];
      });
      return;
    }

    setDashboards(
      dashboards.map((dashboard) =>
        dashboard.id !== active.id
          ? dashboard
          : {
              ...dashboard,
              widgets: dashboard.widgets.some((widget) => widget.id === next.id)
                ? dashboard.widgets.map((widget) =>
                    widget.id === next.id ? next : widget
                  )
                : [...dashboard.widgets, next],
            }
      )
    );
    toast.success("Widget updated");
  }

  function replaceWidgets(next: WidgetDef[]) {
    if (editing) {
      setDraft(next);
      return;
    }

    setDashboards(
      dashboards.map((dashboard) =>
        dashboard.id === active.id ? { ...dashboard, widgets: next } : dashboard
      )
    );
  }

  function createDashboard({
    name,
    description,
    duplicate,
  }: {
    name: string;
    description: string;
    duplicate: boolean;
  }) {
    const id = `custom-${Math.random().toString(36).slice(2, 9)}`;
    const created: DashboardVersion = {
      id,
      name,
      description,
      widgets: duplicate
        ? active.widgets.map((widget) => ({
            ...widget,
            id: `${widget.id}-${Math.random().toString(36).slice(2, 7)}`,
          }))
        : [],
    };

    setDashboards([...dashboards, created]);
    setActiveId(id);
    setDraft(created.widgets);
    setEditing(true);
    toast.success(`“${name}” created`, {
      description: duplicate
        ? "Copied the current layout — now in builder mode."
        : "Blank canvas — add your first widget.",
    });
  }

  function deleteDashboard() {
    if (dashboards.length <= 1) return;
    const remaining = dashboards.filter((dashboard) => dashboard.id !== active.id);
    setDashboards(remaining);
    setActiveId(remaining[0].id);
    setEditing(false);
    toast.success(`“${active.name}” deleted`);
  }

  function resetDashboards() {
    setDashboards(DEFAULT_DASHBOARDS);
    setActiveId(DEFAULT_DASHBOARDS[0].id);
    setEditing(false);
    toast.success("Dashboards reset to defaults");
  }

  function refresh() {
    setRefreshToken((token) => token + 1);
    setRefreshedAt(
      new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
    );
  }

  /**
   * Clicking a mark narrows that widget to the value clicked.
   *
   * Scoped to the one widget rather than the whole dashboard: a click that
   * silently refiltered every other chart is the kind of thing people undo by
   * reloading the page.
   */
  function drill(widget: WidgetDef, key: string, label: string) {
    if (key === "__other__") return;

    const field = widget.query.dimension;
    const already = widget.query.filters.some(
      (condition) => condition.field === field && condition.value === key
    );
    if (already) return;

    commitWidget({
      ...widget,
      query: {
        ...widget.query,
        filters: [
          ...widget.query.filters,
          {
            id: `drill-${Math.random().toString(36).slice(2, 8)}`,
            field,
            operator: "equals",
            value: key,
          },
        ],
      },
    });

    toast.success(`Filtered to ${label}`, {
      description: "Open the widget's Filters tab to remove it.",
    });
  }

  return (
    <div className="flex flex-col gap-2.5">
      {/* Page header — dashboard tabs */}
      <div className="overflow-hidden rounded-md border bg-card shadow-xs">
        <div className="flex flex-wrap items-center justify-between gap-2 px-3.5 py-2">
          <div className="flex items-center gap-5">
            <span className="flex items-center gap-1.5 text-[17px] font-semibold tracking-tight text-primary">
              <LayoutDashboard className="size-[18px]" />
              Dashboard
            </span>
            <Link
              href="/dashboard/calendar"
              className="flex items-center gap-1.5 text-[17px] font-semibold tracking-tight text-muted-foreground transition-colors hover:text-foreground"
            >
              <CalendarDays className="size-[18px]" />
              Calendar
            </Link>
            <Link
              href="/dashboard/goals"
              className="flex items-center gap-1.5 text-[17px] font-semibold tracking-tight text-muted-foreground transition-colors hover:text-foreground"
            >
              <Target className="size-[18px]" />
              Goals
            </Link>
          </div>

          <Button size="sm" className="h-8" onClick={() => setNewDashboardOpen(true)}>
            <Plus /> Create new dashboard
          </Button>
        </div>

        <div className="flex items-stretch gap-0 overflow-x-auto border-t px-2 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
          {dashboards.map((dashboard) => (
            <button
              key={dashboard.id}
              type="button"
              onClick={() => switchDashboard(dashboard.id)}
              title={dashboard.description}
              className={cn(
                "border-b-2 px-3 py-2 text-[13px] whitespace-nowrap transition-colors",
                dashboard.id === active.id
                  ? "border-primary font-medium text-primary"
                  : "border-transparent text-muted-foreground hover:text-foreground"
              )}
            >
              {dashboard.name}
            </button>
          ))}
        </div>
      </div>

      {/* Scope strip */}
      <div className="flex flex-wrap items-center gap-2 rounded-md border bg-card px-3 py-2 shadow-xs">
        <Select
          value={period}
          onValueChange={(value) => setPeriod(value as PeriodKey)}
        >
          <SelectTrigger className="h-8 w-44 text-[13px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {PERIODS.map((entry) => (
              <SelectItem key={entry.value} value={entry.value}>
                {entry.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Badge variant="outline" className="h-7 font-normal">
          {widgets.length} widget{widgets.length === 1 ? "" : "s"}
        </Badge>

        <span className="ml-1 hidden text-[12px] text-muted-foreground lg:block">
          {refreshedAt ? `Refreshed at ${refreshedAt}` : "Live from the CRM"}
        </span>

        <div className="ml-auto flex items-center gap-1.5">
          {editing ? (
            <>
              <AddWidgetDialog
                onAdd={(widget) => setDraft([...draft, widget])}
                onBuildFromScratch={setEditingWidget}
              />
              <Button variant="outline" size="sm" className="h-8" onClick={cancelEditing}>
                <X /> Cancel
              </Button>
              <Button size="sm" className="h-8" onClick={saveEditing}>
                <Check /> Save
              </Button>
            </>
          ) : (
            <>
              <Button variant="outline" size="sm" className="h-8" onClick={refresh}>
                <RefreshCw /> Refresh
              </Button>
              <Button variant="outline" size="sm" className="h-8" onClick={startEditing}>
                <Pencil /> Edit
              </Button>
            </>
          )}

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                variant="ghost"
                size="icon"
                aria-label="Dashboard options"
                className="size-8"
              >
                <MoreHorizontal className="size-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-52">
              <DropdownMenuLabel className="text-[11px] font-normal text-muted-foreground">
                {active.description}
              </DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuItem>
                <Share2 /> Subscribe
              </DropdownMenuItem>
              <DropdownMenuItem onClick={() => setNewDashboardOpen(true)}>
                <Plus /> New dashboard
              </DropdownMenuItem>
              <DropdownMenuItem onClick={resetDashboards}>
                <RotateCcw /> Reset to defaults
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                disabled={dashboards.length <= 1}
                variant="destructive"
                onClick={deleteDashboard}
              >
                <Trash2 /> Delete dashboard
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>

      {editing ? (
        <div className="flex items-center gap-2 rounded-md border border-dashed bg-card px-3 py-2 text-[12.5px] text-muted-foreground">
          <Pencil className="size-3.5 shrink-0" />
          Builder mode — drag a widget by its handle to reorder, set its width
          from the card menu, or open Configure to change what it measures.
        </div>
      ) : null}

      {widgets.length === 0 ? (
        <div className="flex flex-col items-center justify-center gap-3 rounded-md border border-dashed bg-card py-16 text-center">
          <span className="flex size-11 items-center justify-center rounded-full bg-muted text-muted-foreground">
            <LayoutDashboard className="size-5" />
          </span>
          <div>
            <p className="text-sm font-medium">This dashboard is empty</p>
            <p className="text-[13px] text-muted-foreground">
              {editing
                ? "Open the widget gallery to place your first chart."
                : "Switch to edit mode to start building."}
            </p>
          </div>
          {editing ? (
            <AddWidgetDialog
              onAdd={(widget) => setDraft([...draft, widget])}
              onBuildFromScratch={setEditingWidget}
            />
          ) : (
            <Button size="sm" onClick={startEditing}>
              <Pencil /> Edit dashboard
            </Button>
          )}
        </div>
      ) : (
        <DashboardGrid
          widgets={widgets}
          editing={editing}
          period={period}
          scopeLabel={scopeLabel}
          refreshToken={refreshToken}
          onChange={replaceWidgets}
          onEdit={setEditingWidget}
          onDrill={drill}
        />
      )}

      <WidgetEditor
        key={editingWidget?.id ?? "none"}
        widget={editingWidget}
        open={editingWidget !== null}
        onOpenChange={(open) => {
          if (!open) setEditingWidget(null);
        }}
        onSave={commitWidget}
        inheritedPeriod={period}
      />

      <NewDashboardDialog
        open={newDashboardOpen}
        onOpenChange={setNewDashboardOpen}
        onCreate={createDashboard}
      />
    </div>
  );
}
