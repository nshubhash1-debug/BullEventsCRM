"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ChevronDown, Pencil, Plus } from "lucide-react";

import { AppLauncher } from "@/components/shell/app-launcher";
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
import { useActiveApp } from "@/lib/active-app";
import {
  moduleForPathname,
  overflowModulesForApp,
  primaryModulesForApp,
  type ModuleDef,
  type SubNavItem,
} from "@/lib/app-nav-config";
import { cn } from "@/lib/utils";

/**
 * The SLDS "context bar": App Launcher and app name in the primary region, the
 * object tabs in the secondary region, and the 3px brand band along the bottom.
 * The active tab carries its own heavier underline above that band.
 *
 * The tabs are the selected app's modules, not every module in the product —
 * picking Lead Management in the launcher is what makes the bar show leads
 * rather than payroll.
 */
export function AppNavBar() {
  const pathname = usePathname();
  const app = useActiveApp();

  const primaryModules = primaryModulesForApp(app.id);
  const overflowModules = overflowModulesForApp(app.id);

  const active = moduleForPathname(pathname, app.id);
  const overflowActive = overflowModules.some(
    (entry) => entry.href === active.href
  );

  return (
    <div className="flex h-10 shrink-0 items-stretch border-b-[3px] border-b-brand-band bg-card pr-1 pl-1.5">
      {/* Primary region */}
      <div className="flex shrink-0 items-center gap-2 pr-3">
        <AppLauncher />
        <span className="text-[16px] font-bold tracking-tight whitespace-nowrap">
          {app.title}
        </span>
      </div>

      <div className="my-2 w-px shrink-0 bg-border" />

      {/* Secondary region */}
      <nav className="flex flex-1 items-stretch overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {primaryModules.map((entry) => (
          <NavTab
            key={entry.href}
            module={entry}
            active={entry.href === active.href}
          />
        ))}

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button
              type="button"
              className={cn(
                "flex shrink-0 items-center gap-1 px-3 text-[13px] whitespace-nowrap transition-colors",
                "border-b-[3px] -mb-[3px]",
                overflowActive
                  ? "border-primary font-semibold text-foreground"
                  : "border-transparent text-foreground/75 hover:bg-accent/60 hover:text-foreground"
              )}
            >
              More <ChevronDown className="size-3.5 opacity-70" />
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start" className="w-56">
            <DropdownMenuLabel className="text-[11px] tracking-widest text-muted-foreground uppercase">
              Administration
            </DropdownMenuLabel>
            {overflowModules.map((entry) =>
              entry.comingSoon ? (
                <DropdownMenuItem key={entry.href} disabled>
                  <entry.icon className="size-4 text-muted-foreground" />
                  <span className="flex-1">{entry.title}</span>
                  <Badge
                    variant="outline"
                    className="h-4 px-1 text-[9px] font-normal"
                  >
                    Soon
                  </Badge>
                </DropdownMenuItem>
              ) : (
                <DropdownMenuItem key={entry.href} asChild>
                  <Link href={entry.href}>
                    <entry.icon className="size-4 text-muted-foreground" />
                    {entry.title}
                  </Link>
                </DropdownMenuItem>
              )
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      </nav>

      {/* Personalize */}
      <div className="flex shrink-0 items-center">
        <Button
          variant="ghost"
          size="icon"
          aria-label="Edit navigation items"
          title="Edit navigation items"
          className="size-8 text-muted-foreground"
        >
          <Pencil className="size-3.5" />
        </Button>
      </div>
    </div>
  );
}

/** Groups a module's destinations by section, preserving declaration order. */
function sectioned(items: SubNavItem[]) {
  const sections: { name: string | null; items: SubNavItem[] }[] = [];

  for (const item of items) {
    const name = item.section ?? null;
    const last = sections.at(-1);
    if (last && last.name === name) last.items.push(item);
    else sections.push({ name, items: [item] });
  }

  return sections;
}

function NavTab({ module, active }: { module: ModuleDef; active: boolean }) {
  // -mb-[3px] pulls the tab's underline over the bar's brand band, the way
  // Lightning stacks the active indicator on top of the band.
  const base =
    "flex shrink-0 items-center gap-1 px-3 text-[13px] whitespace-nowrap transition-colors border-b-[3px] -mb-[3px]";

  if (module.comingSoon) {
    return (
      <span
        aria-disabled
        title={`${module.title} — coming soon`}
        className={cn(
          base,
          "cursor-not-allowed border-transparent text-muted-foreground/45"
        )}
      >
        {module.title}
      </span>
    );
  }

  // A single-destination module (Reports) has nothing to put in a menu, so it
  // stays a plain link rather than growing a chevron that opens one item.
  const hasMenu =
    (module.subNav?.length ?? 0) > 1 ||
    Boolean(module.recent?.length) ||
    Boolean(module.actions?.length);

  return (
    <div className="flex shrink-0 items-stretch">
      <Link
        href={module.href}
        className={cn(
          base,
          hasMenu && "pr-1",
          active
            ? "border-primary font-semibold text-foreground"
            : "border-transparent text-foreground/75 hover:bg-accent/60 hover:text-foreground"
        )}
      >
        {module.title}
      </Link>

      {hasMenu ? (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button
              type="button"
              aria-label={`${module.title} menu`}
              className={cn(
                "flex items-center border-b-[3px] -mb-[3px] pr-2 pl-0.5 transition-colors",
                active
                  ? "border-primary text-foreground"
                  : "border-transparent text-muted-foreground hover:bg-accent/60 hover:text-foreground"
              )}
            >
              <ChevronDown className="size-3.5 opacity-70" />
            </button>
          </DropdownMenuTrigger>

          <DropdownMenuContent align="start" className="w-72">
            {sectioned(module.subNav ?? []).map((section, index) => (
              <div key={section.name ?? `section-${index}`}>
                {section.name ? (
                  <DropdownMenuLabel className="text-[11px] tracking-widest text-muted-foreground uppercase">
                    {section.name}
                  </DropdownMenuLabel>
                ) : null}

                {section.items.map((item) =>
                  item.comingSoon ? (
                    <DropdownMenuItem key={item.title} disabled>
                      <item.icon className="size-4 text-muted-foreground" />
                      <span className="flex-1">{item.title}</span>
                      <Badge
                        variant="outline"
                        className="h-4 px-1 text-[9px] font-normal"
                      >
                        Soon
                      </Badge>
                    </DropdownMenuItem>
                  ) : (
                    <DropdownMenuItem key={item.title} asChild>
                      <Link href={item.href}>
                        <item.icon className="size-4 shrink-0 text-muted-foreground" />
                        <span className="min-w-0 flex-1">
                          <span className="block truncate text-[13px]">
                            {item.title}
                          </span>
                          {item.description ? (
                            <span className="block truncate text-[11px] text-muted-foreground">
                              {item.description}
                            </span>
                          ) : null}
                        </span>
                      </Link>
                    </DropdownMenuItem>
                  )
                )}
              </div>
            ))}

            {module.recent?.length ? (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuLabel className="text-[11px] tracking-widest text-muted-foreground uppercase">
                  {module.recentLabel ?? "Recent records"}
                </DropdownMenuLabel>
                {module.recent.map((record) => (
                  <DropdownMenuItem key={record.name} asChild>
                    <Link href={module.href}>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-[13px]">
                          {record.name}
                        </span>
                        <span className="block truncate text-[11px] text-muted-foreground">
                          {record.meta}
                        </span>
                      </span>
                    </Link>
                  </DropdownMenuItem>
                ))}
              </>
            ) : null}

            {module.actions?.length ? (
              <>
                <DropdownMenuSeparator />
                {module.actions.map((action) => (
                  <DropdownMenuItem key={action.label} asChild>
                    <Link href={action.href ?? module.href}>
                      <Plus className="size-4 text-muted-foreground" />
                      {action.label}
                    </Link>
                  </DropdownMenuItem>
                ))}
              </>
            ) : null}
          </DropdownMenuContent>
        </DropdownMenu>
      ) : null}
    </div>
  );
}
