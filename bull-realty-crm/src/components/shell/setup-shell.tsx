"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { ChevronDown, ChevronRight, Search, Settings, X } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import {
  SETUP_GROUPS,
  SETUP_HOME,
  SETUP_TREE,
  searchSetup,
  setupItemFor,
  type SetupSection,
} from "@/lib/setup-nav";
import { rememberSetupVisit } from "@/lib/setup-recents";
import { cn } from "@/lib/utils";

/**
 * The Setup experience: a left-hand tree, Quick Find above it, and a breadcrumb
 * over whatever page is open.
 *
 * Wrapped only around the admin console. The working apps deliberately have no
 * left rail — their destinations live in the tab dropdowns, and a rail
 * repeating them would cost horizontal space the dense list views need. Setup
 * is the opposite shape: a deep tree somebody visits a few times a month
 * knowing what they want and not where it lives. That is the case a rail and a
 * search box are for, and it is why Salesforce splits the two shells too.
 */
export function SetupShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const active = setupItemFor(pathname);
  const [railOpen, setRailOpen] = React.useState(false);

  // The rail is a drawer on narrow screens, and it has to close when somebody
  // navigates or it covers the page it just opened. Adjusted during render
  // rather than in an effect: an effect would paint the drawer over the new
  // page for a frame before removing it.
  const [lastPath, setLastPath] = React.useState(pathname);
  if (pathname !== lastPath) {
    setLastPath(pathname);
    setRailOpen(false);
  }

  const activeHref = active?.href ?? null;

  React.useEffect(() => {
    if (activeHref) rememberSetupVisit(activeHref);
  }, [activeHref]);

  return (
    <div className="flex min-h-0 min-w-0 flex-1 gap-4">
      <aside
        className={cn(
          "z-20 w-64 shrink-0 flex-col rounded-xl border bg-card shadow-xs",
          "lg:flex lg:sticky lg:top-[7.25rem] lg:max-h-[calc(100svh-8.5rem)]",
          railOpen
            ? "fixed inset-y-16 left-3 flex max-h-[calc(100svh-5rem)]"
            : "hidden"
        )}
      >
        <QuickFind onNavigate={() => setRailOpen(false)} />
        <SetupTree activeHref={active?.href ?? null} pathname={pathname} />
      </aside>

      {railOpen ? (
        <button
          type="button"
          aria-label="Close the setup menu"
          className="fixed inset-0 z-10 bg-foreground/20 lg:hidden"
          onClick={() => setRailOpen(false)}
        />
      ) : null}

      <div className="flex min-w-0 flex-1 flex-col gap-3">
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            className="h-7 lg:hidden"
            onClick={() => setRailOpen(true)}
          >
            <Settings /> Setup menu
          </Button>

          <Breadcrumb
            section={active?.section ?? null}
            title={active?.title ?? null}
          />
        </div>

        {children}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Quick Find
 * ------------------------------------------------------------------ */

/**
 * The search box that is the actual navigation.
 *
 * An administrator lands in setup two or three times a month knowing the name
 * of the thing they want and not the branch it sits under. Browsing the tree
 * for it is the slow path; this is the fast one, and it is the single most
 * copied idea in Salesforce's admin UI for exactly that reason.
 */
function QuickFind({ onNavigate }: { onNavigate: () => void }) {
  const router = useRouter();
  const [query, setQuery] = React.useState("");
  const [highlighted, setHighlighted] = React.useState(0);

  const matches = React.useMemo(() => searchSetup(query), [query]);

  // Reset during render rather than in an effect: an effect would leave the
  // highlight on a row that no longer exists for one frame, and Enter during
  // that frame opens the wrong page.
  const [lastQuery, setLastQuery] = React.useState(query);
  if (query !== lastQuery) {
    setLastQuery(query);
    setHighlighted(0);
  }

  function go(index: number) {
    const match = matches[index];
    if (!match || match.soon) return;

    setQuery("");
    onNavigate();
    router.push(match.href);
  }

  return (
    <div className="relative shrink-0 border-b p-2">
      <div className="relative">
        <Search className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
        <Input
          value={query}
          placeholder="Quick Find"
          aria-label="Search setup"
          className="h-8 pr-7 pl-8 text-[13px]"
          onChange={(event) => setQuery(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "ArrowDown") {
              event.preventDefault();
              setHighlighted((index) => Math.min(index + 1, matches.length - 1));
            } else if (event.key === "ArrowUp") {
              event.preventDefault();
              setHighlighted((index) => Math.max(index - 1, 0));
            } else if (event.key === "Enter") {
              event.preventDefault();
              go(highlighted);
            } else if (event.key === "Escape") {
              setQuery("");
            }
          }}
        />
        {query ? (
          <button
            type="button"
            aria-label="Clear"
            className="absolute top-1/2 right-2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
            onClick={() => setQuery("")}
          >
            <X className="size-3.5" />
          </button>
        ) : null}
      </div>

      {query ? (
        <div className="absolute inset-x-2 top-[calc(100%-0.25rem)] z-30 overflow-hidden rounded-lg border bg-popover shadow-lg">
          {matches.length === 0 ? (
            <p className="px-3 py-2.5 text-[12.5px] text-muted-foreground">
              Nothing in setup matches “{query}”.
            </p>
          ) : (
            <ul className="max-h-80 overflow-y-auto py-1">
              {matches.map((match, index) => (
                <li key={`${match.href}-${match.title}`}>
                  <button
                    type="button"
                    disabled={match.soon}
                    onMouseEnter={() => setHighlighted(index)}
                    onClick={() => go(index)}
                    className={cn(
                      "flex w-full items-start gap-2.5 px-3 py-2 text-left",
                      index === highlighted && !match.soon && "bg-accent",
                      match.soon && "cursor-not-allowed opacity-55"
                    )}
                  >
                    <match.icon className="mt-0.5 size-3.5 shrink-0 text-muted-foreground" />
                    <span className="min-w-0 flex-1">
                      <span className="flex items-center gap-1.5">
                        <span className="truncate text-[13px] font-medium">
                          {match.title}
                        </span>
                        {match.soon ? (
                          <Badge
                            variant="outline"
                            className="h-4 px-1 text-[9px] font-normal"
                          >
                            Soon
                          </Badge>
                        ) : null}
                      </span>
                      <span className="block truncate text-[11px] text-muted-foreground">
                        {match.section} · {match.description}
                      </span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      ) : null}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * The tree
 * ------------------------------------------------------------------ */

function SetupTree({
  activeHref,
  pathname,
}: {
  activeHref: string | null;
  pathname: string;
}) {
  return (
    <ScrollArea className="min-h-0 flex-1">
      <nav className="flex flex-col gap-3 p-2 pb-4">
        <Link
          href={SETUP_HOME}
          className={cn(
            "flex items-center gap-2 rounded-md px-2 py-1.5 text-[13px] font-medium transition-colors",
            pathname === SETUP_HOME
              ? "bg-accent text-foreground"
              : "text-foreground/80 hover:bg-accent/60"
          )}
        >
          <Settings className="size-3.5 text-muted-foreground" />
          Setup Home
        </Link>

        {SETUP_GROUPS.map((group) => {
          const sections = SETUP_TREE.filter((s) => s.group === group);
          if (sections.length === 0) return null;

          return (
            <div key={group} className="flex flex-col gap-0.5">
              <p className="px-2 pt-1 text-[10px] font-semibold tracking-widest text-muted-foreground uppercase">
                {group}
              </p>

              {sections.map((section) => (
                <TreeSection
                  key={section.title}
                  section={section}
                  activeHref={activeHref}
                />
              ))}
            </div>
          );
        })}
      </nav>
    </ScrollArea>
  );
}

function TreeSection({
  section,
  activeHref,
}: {
  section: SetupSection;
  activeHref: string | null;
}) {
  const owns = section.items.some((item) => item.href === activeHref);

  // Open when it holds the current page, and stays wherever the administrator
  // puts it after that — collapsing the branch somebody is standing in is the
  // classic tree-nav annoyance.
  const [open, setOpen] = React.useState(owns);
  const [wasOwning, setWasOwning] = React.useState(owns);

  if (owns !== wasOwning) {
    setWasOwning(owns);
    if (owns) setOpen(true);
  }

  return (
    <div>
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        className="flex w-full items-center gap-1.5 rounded-md px-2 py-1.5 text-left text-[13px] text-foreground/85 transition-colors hover:bg-accent/60"
      >
        {open ? (
          <ChevronDown className="size-3 shrink-0 text-muted-foreground" />
        ) : (
          <ChevronRight className="size-3 shrink-0 text-muted-foreground" />
        )}
        <section.icon className="size-3.5 shrink-0 text-muted-foreground" />
        <span className="truncate">{section.title}</span>
      </button>

      {open ? (
        <ul className="mt-0.5 ml-[1.05rem] flex flex-col gap-0.5 border-l pl-2">
          {section.items.map((item) => {
            const isActive = item.href === activeHref;

            if (item.soon) {
              return (
                <li key={item.title}>
                  <span
                    aria-disabled
                    title={`${item.title} — not built yet`}
                    className="flex cursor-not-allowed items-center gap-1.5 rounded-md px-2 py-1 text-[12.5px] text-muted-foreground/50"
                  >
                    <span className="truncate">{item.title}</span>
                    <Badge
                      variant="outline"
                      className="h-3.5 shrink-0 px-1 text-[8.5px] font-normal"
                    >
                      Soon
                    </Badge>
                  </span>
                </li>
              );
            }

            return (
              <li key={item.title}>
                <Link
                  href={item.href}
                  className={cn(
                    "block truncate rounded-md px-2 py-1 text-[12.5px] transition-colors",
                    isActive
                      ? "bg-primary/10 font-medium text-primary"
                      : "text-foreground/75 hover:bg-accent/60 hover:text-foreground"
                  )}
                >
                  {item.title}
                </Link>
              </li>
            );
          })}
        </ul>
      ) : null}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Breadcrumb
 * ------------------------------------------------------------------ */

/**
 * Says where in setup this page sits.
 *
 * A deep tree without one leaves every screen looking like it could be
 * anywhere — the reason Salesforce puts "Setup › Users › Permission Sets" over
 * every page rather than trusting the rail to carry it alone.
 */
function Breadcrumb({
  section,
  title,
}: {
  section: string | null;
  title: string | null;
}) {
  return (
    <nav
      aria-label="Breadcrumb"
      className="flex min-w-0 items-center gap-1.5 text-[11.5px] text-muted-foreground"
    >
      <Link href={SETUP_HOME} className="shrink-0 hover:text-foreground">
        Setup
      </Link>

      {section ? (
        <>
          <ChevronRight className="size-3 shrink-0" />
          <span className="shrink-0">{section}</span>
        </>
      ) : null}

      {title ? (
        <>
          <ChevronRight className="size-3 shrink-0" />
          <span className="truncate font-medium text-foreground">{title}</span>
        </>
      ) : null}
    </nav>
  );
}
