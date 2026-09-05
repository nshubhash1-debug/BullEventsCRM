"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ArrowRight, Compass, Hammer } from "lucide-react";

import { PagePanel } from "@/components/shell/page-panel";
import { Badge } from "@/components/ui/badge";
import {
  appById,
  moduleForPathname,
  modulesForApp,
  type AppId,
} from "@/lib/app-nav-config";

/**
 * The page every not-yet-built module renders.
 *
 * An app that ships with empty routes teaches people it is broken, so each
 * module states what it will hold — the `blueprint` on its nav entry — and the
 * app root doubles as a directory of its own modules. When a module gets a real
 * screen, its route file replaces this and nothing else has to change.
 */
export function AppScaffold({ appId }: { appId: AppId }) {
  const pathname = usePathname();
  const app = appById(appId);
  const active = moduleForPathname(pathname, appId);

  // Only this app's own modules — the shared administration tabs are reachable
  // from the nav bar and would just be noise in an app directory.
  const siblings = modulesForApp(appId).filter(
    (entry) => entry.app === appId && entry.href !== active.href
  );

  const isAppRoot = pathname === app.href;
  const Icon = active.icon;

  return (
    <PagePanel
      icon={isAppRoot ? app.icon : Icon}
      title={isAppRoot ? app.title : `${app.title} · ${active.title}`}
      hint={app.description}
      actions={
        <Badge
          variant="outline"
          className="h-5 gap-1.5 px-2 text-[11px] font-normal text-amber-700 dark:text-amber-400"
        >
          <Hammer className="size-3" />
          Scaffold
        </Badge>
      }
    >
      <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 py-2">
        <section className="rounded-md border bg-muted/30 p-4">
          <h2 className="flex items-center gap-2 text-[13px] font-semibold">
            <Compass className="size-4 text-primary" />
            {isAppRoot
              ? `What the ${app.title} app covers`
              : `What ${active.title} will hold`}
          </h2>
          <p className="mt-1 text-[12px] text-muted-foreground">
            {isAppRoot
              ? app.description
              : "This module is wired into navigation and tenant scoping; the screens below are what it is being built to do."}
          </p>

          {active.blueprint?.length ? (
            <ul className="mt-3 space-y-1.5">
              {active.blueprint.map((line) => (
                <li
                  key={line}
                  className="flex gap-2.5 text-[13px] text-foreground/85"
                >
                  <span className="mt-[7px] size-1.5 shrink-0 rounded-full bg-primary/60" />
                  {line}
                </li>
              ))}
            </ul>
          ) : null}
        </section>

        {siblings.length > 0 ? (
          <section>
            <h3 className="mb-2.5 text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
              {isAppRoot ? "Modules" : `More in ${app.title}`}
            </h3>
            <div className="grid gap-2 sm:grid-cols-2">
              {siblings.map((entry) => (
                <Link
                  key={entry.href}
                  href={entry.href}
                  className="group flex items-start gap-3 rounded-md border p-3 transition-colors hover:border-primary/40 hover:bg-accent"
                >
                  <span className="mt-0.5 flex size-8 shrink-0 items-center justify-center rounded bg-primary/10 text-primary">
                    <entry.icon className="size-4" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="flex items-center gap-1.5 text-[13px] font-medium">
                      {entry.title}
                      <ArrowRight className="size-3 opacity-0 transition-opacity group-hover:opacity-60" />
                    </span>
                    <span className="mt-0.5 block truncate text-[12px] text-muted-foreground">
                      {entry.blueprint?.[0] ?? "Not built yet"}
                    </span>
                  </span>
                </Link>
              ))}
            </div>
          </section>
        ) : null}
      </div>
    </PagePanel>
  );
}
