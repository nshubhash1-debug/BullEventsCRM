"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  Bell,
  Building2,
  CircleHelp,
  Laptop,
  LogOut,
  Repeat2,
  Search,
  Settings,
  Star,
  User,
} from "lucide-react";

import { SessionsDialog } from "@/components/dashboard/sessions-dialog";
import { useSession } from "@/components/dashboard/session-provider";
import { GlobalSearch } from "@/components/shell/global-search";
import { ThemeToggle } from "@/components/theme/theme-toggle";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
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

function initials(name: string) {
  return name
    .split(" ")
    .map((part) => part[0])
    .slice(0, 2)
    .join("")
    .toUpperCase();
}

const notifications = [
  {
    title: "Kabir Sethi moved to Negotiation",
    detail: "Lead reassigned by Priya Sharma",
    time: "12m ago",
  },
  {
    title: "New lead from Channel Partner",
    detail: "Kunal Realty registered a lead",
    time: "1h ago",
  },
  {
    title: "Branch created",
    detail: "Pune — Baner is now active",
    time: "Yesterday",
  },
];

const favorites = [
  { label: "Venue diary", href: "/dashboard/inventory/venues/diary" },
  { label: "Hot leads this week", href: "/dashboard/leads" },
  { label: "Sales Overview dashboard", href: "/dashboard" },
];

const utilityButton =
  "size-8 shrink-0 rounded text-muted-foreground hover:bg-accent hover:text-foreground";

/**
 * The Lightning global header: brand at the left, a wide pill search in the
 * middle, and the favorites / setup / help / notifications / avatar cluster at
 * the right. The App Launcher and app name live one row below, on the
 * navigation bar, exactly as SLDS lays out its "context bar".
 */
export function GlobalHeader() {
  const router = useRouter();
  const { user, companies, canSwitchCompany, signOut } = useSession();
  const [sessionsOpen, setSessionsOpen] = React.useState(false);

  return (
    <header className="flex h-[50px] shrink-0 items-center gap-3 border-b bg-card px-3">
      {/* The tenant, not the product, names the header: on a platform admin
          account the only reliable way to tell which company you are working in
          is to have it in front of you at all times. */}
      <Link
        href="/dashboard"
        title={`Working in ${user.companyName}`}
        className="flex shrink-0 items-center gap-2"
      >
        <span className="flex size-7 items-center justify-center rounded bg-primary text-primary-foreground">
          <Building2 className="size-4" strokeWidth={2.5} />
        </span>
        <span className="hidden max-w-52 truncate text-[13px] font-semibold tracking-tight lg:block">
          {user.companyName}
        </span>
      </Link>

      {/* flex-1 with min-w-0, not w-full: the siblings either side are
          shrink-0, so a child that resolves to 100% can never give width back
          and the whole header scrolls sideways on a phone. */}
      <div className="mx-auto min-w-0 flex-1 px-2 sm:max-w-2xl">
        <GlobalSearch
          trigger={
            <button
              type="button"
              className="flex h-8 w-full items-center gap-2.5 rounded-full border bg-muted/60 px-3.5 text-left text-muted-foreground transition-colors hover:border-input hover:bg-card"
            >
              <Search className="size-4 shrink-0" />
              <span className="flex-1 truncate text-[13px]">
                Search leads, branches, users and more…
              </span>
              <kbd className="hidden shrink-0 items-center rounded border bg-card px-1.5 py-0.5 font-mono text-[10px] sm:flex">
                Ctrl K
              </kbd>
            </button>
          }
        />
      </div>

      <div className="flex shrink-0 items-center gap-0.5">
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              aria-label="Favorites"
              title="Favorites"
              className={`hidden sm:inline-flex ${utilityButton}`}
            >
              <Star className="size-[18px]" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-64">
            <DropdownMenuLabel className="text-[11px] tracking-wide text-muted-foreground uppercase">
              Favorites
            </DropdownMenuLabel>
            {favorites.map((favorite) => (
              <DropdownMenuItem key={favorite.label} asChild>
                <Link href={favorite.href}>
                  <Star className="size-4 text-amber-500" />
                  {favorite.label}
                </Link>
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>

        <Button
          variant="ghost"
          size="icon"
          aria-label="Setup"
          title="Setup"
          className={`hidden sm:inline-flex ${utilityButton}`}
          asChild
        >
          <Link href="/dashboard/setup">
            <Settings className="size-[18px]" />
          </Link>
        </Button>

        <Button
          variant="ghost"
          size="icon"
          aria-label="Help"
          title="Help"
          className={`hidden md:inline-flex ${utilityButton}`}
        >
          <CircleHelp className="size-[18px]" />
        </Button>

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              aria-label="Notifications"
              className={`relative ${utilityButton}`}
            >
              <Bell className="size-[18px]" />
              <span className="absolute top-0.5 right-0.5 flex size-4 items-center justify-center rounded-full bg-destructive text-[9px] font-semibold text-white ring-2 ring-card">
                {notifications.length}
              </span>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-80">
            <DropdownMenuLabel className="flex items-center justify-between text-[11px] font-semibold tracking-wide text-muted-foreground uppercase">
              Notifications
              <Badge variant="secondary" className="h-4 px-1.5 text-[10px]">
                {notifications.length} new
              </Badge>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            {notifications.map((n) => (
              <DropdownMenuItem
                key={n.title}
                className="flex-col items-start gap-0.5 py-2"
              >
                <span className="text-[13px] font-medium">{n.title}</span>
                <span className="text-xs text-muted-foreground">{n.detail}</span>
                <span className="mt-0.5 text-[11px] text-muted-foreground/70">
                  {n.time}
                </span>
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>

        <ThemeToggle variant="ghost" className={utilityButton} />

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="ghost"
              className="ml-0.5 size-8 rounded-full p-0 hover:bg-transparent"
              aria-label="Account"
            >
              <Avatar className="size-7 ring-1 ring-border">
                <AvatarFallback className="bg-primary/10 text-[10px] font-semibold text-primary">
                  {initials(user.name)}
                </AvatarFallback>
              </Avatar>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-64">
            <DropdownMenuLabel className="flex flex-col">
              <span className="text-sm font-medium">{user.name}</span>
              <span className="text-xs font-normal text-muted-foreground">
                {user.email}
              </span>
              <Badge
                variant="outline"
                className="mt-2 w-fit gap-1.5 font-normal text-emerald-700 dark:text-emerald-400"
              >
                <span className="size-1.5 rounded-full bg-emerald-500" />
                {user.companyName}
              </Badge>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            {canSwitchCompany ? (
              <DropdownMenuItem onClick={() => router.push("/select-company")}>
                <Repeat2 />
                <span className="flex-1">Switch company</span>
                {companies.length > 1 ? (
                  <Badge
                    variant="secondary"
                    className="h-4 px-1.5 text-[10px] tabular-nums"
                  >
                    {companies.length}
                  </Badge>
                ) : null}
              </DropdownMenuItem>
            ) : null}
            <DropdownMenuItem onSelect={() => setSessionsOpen(true)}>
              <Laptop /> Signed-in devices
            </DropdownMenuItem>
            <DropdownMenuItem>
              <User /> Profile
            </DropdownMenuItem>
            <DropdownMenuItem>
              <Settings /> Settings
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem variant="destructive" onClick={signOut}>
              <LogOut /> Sign out
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>

        <SessionsDialog open={sessionsOpen} onOpenChange={setSessionsOpen} />
      </div>
    </header>
  );
}
