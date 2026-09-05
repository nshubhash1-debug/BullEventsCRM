"use client";

import * as React from "react";
import Link from "next/link";
import { AlertTriangle, ArrowRight, Clock, Settings } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { useSession } from "@/components/dashboard/session-provider";
import { Badge } from "@/components/ui/badge";
import {
  ApiError,
  customisationApi,
  getSubscription,
  getUsers,
  type SetupStep,
  type Subscription,
  type UserListItem,
} from "@/lib/api";
import { SETUP_GROUPS, SETUP_TREE } from "@/lib/setup-nav";
import { useSetupRecents } from "@/lib/setup-recents";
import { cn } from "@/lib/utils";

/**
 * Setup Home.
 *
 * Not a dashboard — an administrator arriving here has a job in mind, and the
 * page's only real duty is to get them to it. So it leads with what needs
 * attention, then what they touched last, then the whole tree laid flat. The
 * numbers on it are the ones that change what somebody does next: seats close
 * to the cap, a trial running out, invites nobody accepted.
 */
export default function SetupHome() {
  const { user } = useSession();
  const [subscription, setSubscription] = React.useState<Subscription | null>(null);
  const [steps, setSteps] = React.useState<SetupStep[]>([]);
  const [users, setUsers] = React.useState<UserListItem[]>([]);

  React.useEffect(() => {
    getSubscription()
      .then(setSubscription)
      .catch((error: unknown) => {
        // A company admin without the subscription permission still gets a
        // usable Setup Home; only the plan card goes missing.
        if (!(error instanceof ApiError) || error.status !== 403) {
          toast.error("Could not load the plan", {
            description:
              error instanceof ApiError ? error.message : "Network error.",
          });
        }
      });

    customisationApi.setup().then(setSteps).catch(() => setSteps([]));
    getUsers().then(setUsers).catch(() => setUsers([]));
  }, []);

  const attention = React.useMemo(
    () => buildAttention(subscription, steps, users),
    [subscription, steps, users]
  );

  return (
    <PagePanel
      icon={Settings}
      title="Setup"
      hint="Everything that configures this workspace rather than works in it. Use Quick Find on the left when you know the name of what you want — it is faster than the tree."
      actions={
        <span className="text-[12px] text-muted-foreground">
          {user.companyName}
        </span>
      }
    >
      <div className="flex flex-col gap-7">
        {attention.length > 0 ? (
          <section className="flex flex-col gap-2">
            <h2 className="text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
              Needs attention
            </h2>

            <div className="flex flex-col gap-1.5">
              {attention.map((item) => (
                <Link
                  key={item.title}
                  href={item.href}
                  className={cn(
                    "flex items-start gap-2.5 rounded-lg border border-l-[3px] px-3.5 py-2.5 transition-colors hover:bg-accent/40",
                    item.tone === "warn"
                      ? "border-l-amber-500"
                      : "border-l-sky-500"
                  )}
                >
                  <AlertTriangle
                    className={cn(
                      "mt-0.5 size-3.5 shrink-0",
                      item.tone === "warn"
                        ? "text-amber-600 dark:text-amber-400"
                        : "text-sky-600 dark:text-sky-400"
                    )}
                  />
                  <span className="min-w-0 flex-1">
                    <span className="block text-[13px] font-medium">
                      {item.title}
                    </span>
                    <span className="block text-[12px] text-muted-foreground">
                      {item.detail}
                    </span>
                  </span>
                  <ArrowRight className="mt-0.5 size-3.5 shrink-0 text-muted-foreground" />
                </Link>
              ))}
            </div>
          </section>
        ) : null}

        <Recents />

        {SETUP_GROUPS.map((group) => {
          const sections = SETUP_TREE.filter((s) => s.group === group);
          if (sections.length === 0) return null;

          return (
            <section key={group} className="flex flex-col gap-3">
              <h2 className="text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
                {group}
              </h2>

              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                {sections.map((section) => (
                  <div key={section.title} className="rounded-lg border p-3.5">
                    <p className="flex items-center gap-2 text-[13px] font-semibold">
                      <section.icon className="size-4 text-primary" />
                      {section.title}
                    </p>

                    <ul className="mt-2 flex flex-col gap-0.5">
                      {section.items.map((item) =>
                        item.soon ? (
                          <li
                            key={item.title}
                            className="flex items-center gap-1.5 py-0.5 text-[12.5px] text-muted-foreground/55"
                          >
                            {item.title}
                            <Badge
                              variant="outline"
                              className="h-3.5 px-1 text-[8.5px] font-normal"
                            >
                              Soon
                            </Badge>
                          </li>
                        ) : (
                          <li key={item.title}>
                            <Link
                              href={item.href}
                              className="block truncate py-0.5 text-[12.5px] text-foreground/80 hover:text-primary hover:underline"
                            >
                              {item.title}
                            </Link>
                          </li>
                        )
                      )}
                    </ul>
                  </div>
                ))}
              </div>
            </section>
          );
        })}
      </div>
    </PagePanel>
  );
}

/* ------------------------------------------------------------------ *
 * Needs attention
 * ------------------------------------------------------------------ */

interface Attention {
  title: string;
  detail: string;
  href: string;
  tone: "warn" | "info";
}

/**
 * The handful of things worth interrupting somebody about.
 *
 * Derived from live data rather than stored as alerts, so the list is honest
 * the moment the underlying thing is fixed — and deliberately short. A page
 * that always has six warnings on it trains people to scroll past all six.
 */
function buildAttention(
  subscription: Subscription | null,
  steps: SetupStep[],
  users: UserListItem[]
): Attention[] {
  const items: Attention[] = [];

  if (subscription) {
    const trial = subscription.trialDaysLeft;

    if (trial !== null && trial <= 14) {
      items.push({
        title:
          trial === 0
            ? "This trial has run out"
            : `${trial} ${trial === 1 ? "day" : "days"} left on the trial`,
        detail:
          "The workspace is suspended automatically when it ends — not deleted.",
        href: "/dashboard/companies/subscription",
        tone: "warn",
      });
    }

    for (const limit of subscription.limits) {
      if (limit.allowed <= 0) continue;

      const used = limit.used / limit.allowed;
      if (used < 0.85) continue;

      items.push({
        title:
          limit.used > limit.allowed
            ? `Over the ${limit.label} limit`
            : `${limit.label} nearly full`,
        detail: `${limit.used.toLocaleString("en-IN")} of ${limit.allowed.toLocaleString("en-IN")} on the ${subscription.planName} plan.`,
        href: "/dashboard/companies/subscription",
        tone: limit.used > limit.allowed ? "warn" : "info",
      });
    }
  }

  const neverSignedIn = users.filter(
    (u) => u.isActive && u.lastLoginAt === null
  ).length;

  if (neverSignedIn > 0) {
    items.push({
      title: `${neverSignedIn} ${neverSignedIn === 1 ? "invite has" : "invites have"} never been used`,
      detail:
        "Active seats nobody has signed into. They count against the plan either way.",
      href: "/dashboard/users",
      tone: "info",
    });
  }

  const unmanaged = users.filter((u) => u.isActive && u.managerId === null).length;

  if (unmanaged > 1) {
    items.push({
      title: `${unmanaged} people report to nobody`,
      detail:
        "A team-scoped role only reaches the people beneath it, so anyone off the tree is invisible to their manager.",
      href: "/dashboard/users/teams",
      tone: "info",
    });
  }

  const undone = steps.filter((step) => !step.done);

  if (undone.length > 0) {
    items.push({
      title: `${undone.length} setup ${undone.length === 1 ? "step is" : "steps are"} outstanding`,
      detail: undone.map((step) => step.title).join(" · "),
      href: "/dashboard/companies/setup",
      tone: "info",
    });
  }

  return items.slice(0, 4);
}

/* ------------------------------------------------------------------ *
 * Recently visited
 * ------------------------------------------------------------------ */

/**
 * The setup pages this browser opened last, as chips.
 *
 * Read in an effect rather than during render because local storage does not
 * exist on the server, and reading it while rendering would make the markup the
 * server produced disagree with the first client render.
 */
function Recents() {
  const recents = useSetupRecents();

  const items = recents
    .map((href) =>
      SETUP_TREE.flatMap((s) => s.items.map((i) => ({ ...i, section: s.title })))
        .find((i) => i.href === href)
    )
    .filter((item): item is NonNullable<typeof item> => Boolean(item))
    .slice(0, 6);

  if (items.length === 0) return null;

  return (
    <section className="flex flex-col gap-2">
      <h2 className="flex items-center gap-1.5 text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
        <Clock className="size-3" />
        Recently visited
      </h2>

      <div className="flex flex-wrap gap-1.5">
        {items.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className="flex items-center gap-1.5 rounded-md border px-2.5 py-1.5 text-[12.5px] transition-colors hover:bg-accent/50"
          >
            <item.icon className="size-3.5 text-muted-foreground" />
            {item.title}
          </Link>
        ))}
      </div>
    </section>
  );
}
