"use client";

import * as React from "react";
import { Copy, FilePlus2, Plus, Search, Wand2 } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Textarea } from "@/components/ui/textarea";
import {
  BLUEPRINT_CATEGORIES,
  WIDGET_BLUEPRINTS,
  type WidgetBlueprint,
} from "@/lib/dashboard/presets";
import {
  DEFAULT_OPTIONS,
  DEFAULT_QUERY,
  newWidgetId,
  styleLabel,
  type WidgetDef,
} from "@/lib/dashboard/types";
import { cn } from "@/lib/utils";

/* ------------------------------------------------------------------ *
 * Add widget
 * ------------------------------------------------------------------ */

export function AddWidgetDialog({
  onAdd,
  onBuildFromScratch,
}: {
  onAdd: (widget: WidgetDef) => void;
  /** Opens the editor on a blank widget instead of a preset. */
  onBuildFromScratch: (widget: WidgetDef) => void;
}) {
  const [open, setOpen] = React.useState(false);
  const [query, setQuery] = React.useState("");
  const [category, setCategory] = React.useState<string>("All");

  const needle = query.trim().toLowerCase();
  const results = WIDGET_BLUEPRINTS.filter((blueprint) => {
    if (category !== "All" && blueprint.category !== category) return false;
    if (!needle) return true;
    return (
      blueprint.title.toLowerCase().includes(needle) ||
      blueprint.description.toLowerCase().includes(needle) ||
      blueprint.category.toLowerCase().includes(needle)
    );
  });

  function add(blueprint: WidgetBlueprint) {
    onAdd(blueprint.build());
    close();
  }

  function blank() {
    onBuildFromScratch({
      id: newWidgetId(),
      style: "bar",
      span: 2,
      height: "medium",
      query: { ...DEFAULT_QUERY },
      options: { ...DEFAULT_OPTIONS },
    });
    close();
  }

  function close() {
    setOpen(false);
    setQuery("");
    setCategory("All");
  }

  return (
    <Dialog open={open} onOpenChange={(next) => (next ? setOpen(true) : close())}>
      <DialogTrigger asChild>
        <Button size="sm" className="h-8">
          <Plus /> Add widget
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[88vh] gap-0 overflow-hidden p-0 sm:max-w-3xl">
        <DialogHeader className="space-y-3 border-b p-5 pb-4 text-left">
          <div className="flex items-start justify-between gap-3">
            <div>
              <DialogTitle className="text-base">Widget gallery</DialogTitle>
              <DialogDescription>
                Drop in a ready-made chart, then reshape it however you like.
              </DialogDescription>
            </div>
            <Button variant="outline" size="sm" className="h-8" onClick={blank}>
              <Wand2 /> Build from scratch
            </Button>
          </div>

          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              autoFocus
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Search widgets…"
              className="h-9 pl-9"
            />
          </div>

          <div className="flex flex-wrap gap-1">
            {["All", ...BLUEPRINT_CATEGORIES].map((name) => (
              <button
                key={name}
                type="button"
                onClick={() => setCategory(name)}
                className={cn(
                  "rounded-full border px-2.5 py-1 text-[12px] transition-colors",
                  category === name
                    ? "border-primary bg-primary font-medium text-primary-foreground"
                    : "text-muted-foreground hover:bg-accent"
                )}
              >
                {name}
              </button>
            ))}
          </div>
        </DialogHeader>

        <ScrollArea className="max-h-[52vh]">
          <div className="grid gap-2 p-5 sm:grid-cols-2">
            {results.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                No widgets match “{query}”.
              </p>
            ) : (
              results.map((blueprint) => {
                const preview = blueprint.build();
                return (
                  <button
                    key={blueprint.id}
                    type="button"
                    onClick={() => add(blueprint)}
                    className="flex flex-col gap-1 rounded-lg border p-3 text-left transition-colors hover:border-primary/40 hover:bg-accent"
                  >
                    <span className="flex items-center gap-2">
                      <span className="flex-1 truncate text-[13px] font-semibold">
                        {blueprint.title}
                      </span>
                      <Badge
                        variant="outline"
                        className="h-4 shrink-0 px-1 text-[9px] font-normal"
                      >
                        {styleLabel(preview.style)}
                      </Badge>
                    </span>
                    <span className="text-xs leading-snug text-muted-foreground">
                      {blueprint.description}
                    </span>
                    <span className="mt-0.5 text-[10.5px] tracking-wide text-muted-foreground/70 uppercase">
                      {blueprint.category}
                    </span>
                  </button>
                );
              })
            )}
          </div>
        </ScrollArea>
      </DialogContent>
    </Dialog>
  );
}

/* ------------------------------------------------------------------ *
 * New dashboard
 * ------------------------------------------------------------------ */

export function NewDashboardDialog({
  open,
  onOpenChange,
  onCreate,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreate: (input: {
    name: string;
    description: string;
    duplicate: boolean;
  }) => void;
}) {
  const [name, setName] = React.useState("");
  const [description, setDescription] = React.useState("");
  const [duplicate, setDuplicate] = React.useState(false);

  function handleOpenChange(next: boolean) {
    onOpenChange(next);
    if (!next) {
      setName("");
      setDescription("");
      setDuplicate(false);
    }
  }

  function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!name.trim()) return;
    onCreate({
      name: name.trim(),
      description: description.trim() || "Custom dashboard",
      duplicate,
    });
    handleOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={submit}>
          <DialogHeader>
            <DialogTitle>New dashboard</DialogTitle>
            <DialogDescription>
              Start from an empty canvas or copy the dashboard you&apos;re
              viewing.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-4">
            <div className="grid gap-2">
              <Label htmlFor="dashboard-name">Name</Label>
              <Input
                id="dashboard-name"
                autoFocus
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder="e.g. Weekly Sales Review"
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="dashboard-description">Description</Label>
              <Textarea
                id="dashboard-description"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
                placeholder="What does this dashboard answer?"
                className="min-h-16 resize-none"
              />
            </div>

            <div className="grid gap-2">
              <Label>Starting point</Label>
              <div className="grid grid-cols-2 gap-2">
                <button
                  type="button"
                  onClick={() => setDuplicate(false)}
                  className={cn(
                    "flex flex-col items-start gap-1 rounded-lg border p-3 text-left transition-colors hover:bg-accent",
                    !duplicate && "border-primary/50 bg-accent"
                  )}
                >
                  <FilePlus2 className="size-4 text-muted-foreground" />
                  <span className="text-[13px] font-medium">Blank</span>
                  <span className="text-[11px] text-muted-foreground">
                    Empty canvas
                  </span>
                </button>
                <button
                  type="button"
                  onClick={() => setDuplicate(true)}
                  className={cn(
                    "flex flex-col items-start gap-1 rounded-lg border p-3 text-left transition-colors hover:bg-accent",
                    duplicate && "border-primary/50 bg-accent"
                  )}
                >
                  <Copy className="size-4 text-muted-foreground" />
                  <span className="text-[13px] font-medium">Duplicate</span>
                  <span className="text-[11px] text-muted-foreground">
                    Copy current widgets
                  </span>
                </button>
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => handleOpenChange(false)}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={!name.trim()}>
              Create dashboard
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
