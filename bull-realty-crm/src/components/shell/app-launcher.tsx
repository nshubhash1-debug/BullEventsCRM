"use client";

import * as React from "react";
import Link from "next/link";
import { Check, Search } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import { useActiveApp } from "@/lib/active-app";
import {
  APPS,
  MODULE_GROUPS,
  modulesForApp,
  type AppDef,
} from "@/lib/app-nav-config";
import { cn } from "@/lib/utils";

/** The 9-dot waffle that opens the launcher, drawn to SLDS proportions. */
function WaffleIcon({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 16 16"
      aria-hidden="true"
      className={cn("size-4", className)}
      fill="currentColor"
    >
      {[1, 6.5, 12].map((y) =>
        [1, 6.5, 12].map((x) => (
          <rect key={`${x}-${y}`} x={x} y={y} width="3" height="3" rx="0.6" />
        ))
      )}
    </svg>
  );
}

/**
 * The launcher picks the app first, items second.
 *
 * Choosing an app is what narrows the navigation bar: the items list below is
 * the selected app's own modules, so the launcher shows exactly what the bar
 * will show once you leave it. Searching looks across every app, because
 * someone hunting for "Payroll" should not have to know it lives in HR.
 */
export function AppLauncher() {
  const [open, setOpen] = React.useState(false);
  const [query, setQuery] = React.useState("");
  const activeApp = useActiveApp();

  // While searching, the item list spans every app; otherwise it is scoped to
  // the app currently loaded.
  const needle = query.trim().toLowerCase();

  const appMatches = APPS.filter(
    (app) =>
      !needle ||
      app.title.toLowerCase().includes(needle) ||
      app.description.toLowerCase().includes(needle)
  );

  const itemScope = needle
    ? APPS.flatMap((app) =>
        modulesForApp(app.id)
          .filter((entry) => entry.app === app.id)
          .map((entry) => ({ entry, app }))
      ).concat(
        // Shared administration modules, listed once rather than per app.
        modulesForApp(activeApp.id)
          .filter((entry) => entry.app === undefined)
          .map((entry) => ({ entry, app: activeApp }))
      )
    : modulesForApp(activeApp.id).map((entry) => ({ entry, app: activeApp }));

  const itemMatches = itemScope.filter(
    ({ entry }) => !needle || entry.title.toLowerCase().includes(needle)
  );

  function handleOpenChange(next: boolean) {
    setOpen(next);
    if (!next) setQuery("");
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger asChild>
        <button
          type="button"
          aria-label="App Launcher"
          title="App Launcher"
          className="flex size-8 shrink-0 items-center justify-center rounded text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
        >
          <WaffleIcon />
        </button>
      </DialogTrigger>

      <DialogContent
        showCloseButton
        className="max-h-[85vh] gap-0 overflow-hidden p-0 sm:max-w-2xl"
      >
        <DialogHeader className="space-y-3 border-b p-5 pb-4 text-left">
          <div>
            <DialogTitle className="text-base">App Launcher</DialogTitle>
            <DialogDescription>
              Pick an app — the navigation bar then carries only that app&rsquo;s
              modules.
            </DialogDescription>
          </div>
          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              autoFocus
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Search apps and items…"
              className="h-9 pl-9"
            />
          </div>
        </DialogHeader>

        <ScrollArea className="max-h-[60vh]">
          <div className="space-y-6 p-5">
            {appMatches.length > 0 ? (
              <section>
                <h3 className="mb-2.5 text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
                  Apps
                </h3>
                <div className="grid gap-2 sm:grid-cols-2">
                  {appMatches.map((app) => (
                    <AppTile
                      key={app.id}
                      app={app}
                      active={app.id === activeApp.id}
                      onSelect={() => handleOpenChange(false)}
                    />
                  ))}
                </div>
              </section>
            ) : null}

            {MODULE_GROUPS.map((group) => {
              const items = itemMatches.filter(
                ({ entry }) => entry.group === group
              );
              if (items.length === 0) return null;

              return (
                <section key={group}>
                  <h3 className="mb-2.5 text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
                    {group}
                  </h3>
                  <div className="grid gap-1.5 sm:grid-cols-2 lg:grid-cols-3">
                    {items.map(({ entry, app }) => {
                      const inner = (
                        <>
                          <span
                            className={cn(
                              "flex size-8 shrink-0 items-center justify-center rounded",
                              entry.comingSoon
                                ? "bg-muted text-muted-foreground"
                                : "bg-primary text-primary-foreground"
                            )}
                          >
                            <entry.icon className="size-4" />
                          </span>
                          <span className="min-w-0 flex-1">
                            <span className="block truncate text-[13px] font-medium">
                              {entry.title}
                            </span>
                            {needle && entry.app ? (
                              <span className="block truncate text-[11px] text-muted-foreground">
                                {app.title}
                              </span>
                            ) : null}
                          </span>
                          {entry.comingSoon ? (
                            <Badge
                              variant="outline"
                              className="h-4 shrink-0 px-1 text-[9px] font-normal"
                            >
                              Soon
                            </Badge>
                          ) : null}
                        </>
                      );

                      if (entry.comingSoon) {
                        return (
                          <div
                            key={entry.href}
                            className="flex cursor-not-allowed items-center gap-2.5 rounded border p-2.5 opacity-55"
                          >
                            {inner}
                          </div>
                        );
                      }

                      return (
                        <Link
                          key={entry.href}
                          href={entry.href}
                          onClick={() => handleOpenChange(false)}
                          className="flex items-center gap-2.5 rounded border p-2.5 transition-colors hover:border-primary/40 hover:bg-accent"
                        >
                          {inner}
                        </Link>
                      );
                    })}
                  </div>
                </section>
              );
            })}

            {appMatches.length === 0 && itemMatches.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                Nothing matches “{query}”.
              </p>
            ) : null}
          </div>
        </ScrollArea>
      </DialogContent>
    </Dialog>
  );
}

function AppTile({
  app,
  active,
  onSelect,
}: {
  app: AppDef;
  active: boolean;
  onSelect: () => void;
}) {
  return (
    <Link
      href={app.href}
      onClick={onSelect}
      aria-current={active ? "true" : undefined}
      className={cn(
        "flex items-start gap-3 rounded-md border p-3 transition-colors",
        active
          ? "border-primary/50 bg-accent"
          : "hover:border-primary/40 hover:bg-accent/60"
      )}
    >
      <span
        className={cn(
          "flex size-9 shrink-0 items-center justify-center rounded",
          app.accent
        )}
      >
        <app.icon className="size-[18px]" />
      </span>

      <span className="min-w-0 flex-1">
        <span className="flex items-center gap-1.5">
          <span className="truncate text-[13px] font-semibold">{app.title}</span>
          {active ? (
            <Check className="size-3.5 shrink-0 text-primary" />
          ) : null}
          {app.live ? null : (
            <Badge
              variant="outline"
              className="h-4 shrink-0 px-1 text-[9px] font-normal"
            >
              Scaffold
            </Badge>
          )}
        </span>
        <span className="mt-0.5 block text-[11px] leading-snug text-muted-foreground">
          {app.description}
        </span>
      </span>
    </Link>
  );
}
