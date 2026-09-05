"use client";

import * as React from "react";
import Link from "next/link";
import {
  AlertTriangle,
  Blocks,
  Building2,
  ClipboardCheck,
  HardHat,
  Handshake,
  Sparkles,
  Truck,
  Warehouse,
} from "lucide-react";

import { PagePanel } from "@/components/shell/page-panel";
import { formatMoney } from "@/lib/crm-api";
import { inventoryApi } from "@/lib/crm-api";
import { propsApi, type PropDashboard } from "@/lib/props-api";
import {
  crewApi,
  fleetApi,
  kitsApi,
  vendorsApi,
  type CrewRoleCoverage,
  type PropKit,
  type Vehicle,
} from "@/lib/resources-api";
import { cn } from "@/lib/utils";

function isoToday(offsetDays = 0) {
  const date = new Date();
  date.setDate(date.getDate() + offsetDays);
  return date.toISOString().slice(0, 10);
}

/**
 * One inventory's line on the overview.
 *
 * Every card carries the same three things — how much there is, what is
 * committed, and what needs attention — because that is the shape of the
 * question regardless of whether the stock is a hall, a vase or a bearer.
 */
interface Card {
  title: string;
  href: string;
  icon: React.ElementType;
  headline: string;
  headlineNote: string;
  facts: { label: string; value: string; tone?: "warn" | "bad" | "good" }[];
  warning?: string;
}

const TONE: Record<string, string> = {
  warn: "text-amber-600 dark:text-amber-400",
  bad: "text-rose-600 dark:text-rose-400",
  good: "text-emerald-600 dark:text-emerald-400",
};

export default function InventoryOverviewPage() {
  const [props, setProps] = React.useState<PropDashboard | null>(null);
  const [kits, setKits] = React.useState<PropKit[] | null>(null);
  const [venues, setVenues] = React.useState<{ projects: number; spaces: number } | null>(null);
  const [vendors, setVendors] = React.useState<{ total: number; expiring: number } | null>(null);
  const [crew, setCrew] = React.useState<{ total: number; coverage: CrewRoleCoverage[] } | null>(null);
  const [fleet, setFleet] = React.useState<{ vehicles: Vehicle[]; lapsed: number } | null>(null);
  const [loaded, setLoaded] = React.useState(false);

  const from = isoToday();
  const to = isoToday(6);

  React.useEffect(() => {
    let cancelled = false;

    // Composed on the client from the five modules' own endpoints rather than a
    // sixth roll-up on the server: each of these is already cached by the page
    // the reader is about to click through to, and a new endpoint would have
    // been a migration to say what these five already say.
    Promise.allSettled([
      propsApi.dashboard(),
      kitsApi.list({ from, to }),
      inventoryApi.projects(),
      inventoryApi.query({ page: 1, pageSize: 1 }),
      vendorsApi.query({ page: 1, pageSize: 300 }),
      crewApi.query({ page: 1, pageSize: 500 }),
      crewApi.coverage({ from, to }),
      fleetApi.availability({ from, to }),
    ]).then((results) => {
      if (cancelled) return;

      const value = <T,>(index: number): T | null =>
        results[index].status === "fulfilled"
          ? ((results[index] as PromiseFulfilledResult<T>).value ?? null)
          : null;

      setProps(value<PropDashboard>(0));
      setKits(value<PropKit[]>(1));

      const projects = value<unknown[]>(2);
      const spaces = value<{ total: number }>(3);
      setVenues(
        projects || spaces
          ? { projects: projects?.length ?? 0, spaces: spaces?.total ?? 0 }
          : null
      );

      const vendorPage = value<{ total: number; items: { expiringDocuments: number }[] }>(4);
      setVendors(
        vendorPage
          ? {
              total: vendorPage.total,
              expiring: vendorPage.items.filter((v) => v.expiringDocuments > 0).length,
            }
          : null
      );

      const crewPage = value<{ total: number }>(5);
      const coverage = value<CrewRoleCoverage[]>(6);
      setCrew(crewPage ? { total: crewPage.total, coverage: coverage ?? [] } : null);

      const vehicles = value<Vehicle[]>(7);
      setFleet(
        vehicles
          ? { vehicles, lapsed: vehicles.filter((v) => v.hasLapsedPapers).length }
          : null
      );

      setLoaded(true);
    });

    return () => {
      cancelled = true;
    };
  }, [from, to]);

  const cards: Card[] = [];

  if (venues) {
    cards.push({
      title: "Venues",
      href: "/dashboard/inventory/venues/diary",
      icon: Building2,
      headline: String(venues.spaces),
      headlineNote: "bookable spaces",
      facts: [{ label: "Venues on the books", value: String(venues.projects) }],
    });
  }

  if (props) {
    const fieldableKits = kits?.filter((k) => k.canFulfil).length ?? 0;

    cards.push({
      title: "Props & Décor",
      href: "/dashboard/inventory/props",
      icon: Sparkles,
      headline: props.goodPieces.toLocaleString(),
      headlineNote: `usable pieces across ${props.totalItems} items`,
      facts: [
        { label: "Catalogue value", value: formatMoney(props.catalogueValue) },
        {
          label: "Out on events",
          value: props.piecesOutOnEvents.toLocaleString(),
          tone: props.piecesOutOnEvents > 0 ? "warn" : undefined,
        },
        {
          label: "Kits ready to field",
          value: kits ? `${fieldableKits} of ${kits.length}` : "—",
        },
        {
          label: "Needs repair",
          value: String(props.repairablePieces + props.damagedPieces),
          tone: props.damagedPieces > 0 ? "warn" : undefined,
        },
      ],
      warning:
        props.overdueIssues > 0
          ? `${props.overdueIssues} gate pass${props.overdueIssues === 1 ? "" : "es"} past the return date`
          : undefined,
    });
  }

  if (vendors) {
    cards.push({
      title: "Suppliers",
      href: "/dashboard/inventory/vendors",
      icon: Handshake,
      headline: String(vendors.total),
      headlineNote: "on the directory",
      facts: [
        {
          label: "Papers expiring soon",
          value: String(vendors.expiring),
          tone: vendors.expiring > 0 ? "warn" : "good",
        },
      ],
      warning:
        vendors.expiring > 0
          ? `${vendors.expiring} supplier${vendors.expiring === 1 ? "" : "s"} with a licence or insurance lapsing`
          : undefined,
    });
  }

  if (crew) {
    const free = crew.coverage.reduce((sum, c) => sum + c.available, 0);
    const trades = crew.coverage.filter((c) => c.available === 0).length;

    cards.push({
      title: "Crew",
      href: "/dashboard/inventory/crew",
      icon: HardHat,
      headline: String(crew.total),
      headlineNote: "on the roster",
      facts: [
        { label: "Free this week", value: String(free), tone: free > 0 ? "good" : "bad" },
        { label: "Trades covered", value: String(crew.coverage.length) },
      ],
      warning:
        trades > 0
          ? `${trades} trade${trades === 1 ? " has" : "s have"} nobody free this week`
          : undefined,
    });
  }

  if (fleet) {
    const free = fleet.vehicles.filter((v) => v.isAvailable).length;
    const payload = fleet.vehicles
      .filter((v) => v.isAvailable)
      .reduce((sum, v) => sum + (v.payloadKg ?? 0), 0);

    cards.push({
      title: "Fleet",
      href: "/dashboard/inventory/fleet",
      icon: Truck,
      headline: `${free} of ${fleet.vehicles.length}`,
      headlineNote: "free this week",
      facts: [
        { label: "Payload free", value: `${payload.toLocaleString()} kg` },
        {
          label: "Papers lapsed",
          value: String(fleet.lapsed),
          tone: fleet.lapsed > 0 ? "bad" : "good",
        },
      ],
      warning:
        fleet.lapsed > 0
          ? `${fleet.lapsed} vehicle${fleet.lapsed === 1 ? "" : "s"} cannot be dispatched`
          : undefined,
    });
  }

  const warnings = cards.filter((c) => c.warning);

  return (
    <PagePanel
      icon={Warehouse}
      title="Inventory"
      hint="Everything an event is assembled from — what there is, what is committed, and what needs attention this week."
    >
      <div className="min-h-0 flex-1 space-y-5 overflow-y-auto px-5 pb-5">
        {!loaded ? (
          <p className="py-16 text-center text-[13px] text-muted-foreground">Loading…</p>
        ) : (
          <>
            {warnings.length > 0 ? (
              <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 dark:border-amber-900 dark:bg-amber-950/40">
                <div className="mb-1 flex items-center gap-1.5 text-[13px] font-semibold text-amber-900 dark:text-amber-300">
                  <AlertTriangle className="size-3.5" />
                  Wants looking at
                </div>
                <ul className="space-y-0.5 text-[12.5px] text-amber-900 dark:text-amber-300">
                  {warnings.map((c) => (
                    <li key={c.title}>
                      <Link href={c.href} className="underline-offset-2 hover:underline">
                        {c.title}
                      </Link>
                      {" — "}
                      {c.warning}
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {cards.map((card) => (
                <Link
                  key={card.title}
                  href={card.href}
                  className="group rounded-lg border bg-card p-4 transition hover:border-primary/50 hover:shadow-sm"
                >
                  <div className="flex items-center gap-1.5 text-[11px] uppercase tracking-wide text-muted-foreground">
                    <card.icon className="size-3.5" />
                    {card.title}
                  </div>

                  <div className="mt-1.5 text-3xl font-semibold tabular-nums">
                    {card.headline}
                  </div>
                  <div className="text-[11.5px] text-muted-foreground">
                    {card.headlineNote}
                  </div>

                  {card.facts.length > 0 ? (
                    <dl className="mt-3 space-y-1 border-t pt-2.5">
                      {card.facts.map((fact) => (
                        <div
                          key={fact.label}
                          className="flex items-baseline justify-between gap-2 text-[12.5px]"
                        >
                          <dt className="text-muted-foreground">{fact.label}</dt>
                          <dd
                            className={cn(
                              "font-medium tabular-nums",
                              fact.tone ? TONE[fact.tone] : ""
                            )}
                          >
                            {fact.value}
                          </dd>
                        </div>
                      ))}
                    </dl>
                  ) : null}
                </Link>
              ))}
            </div>

            <div className="flex flex-wrap gap-2 border-t pt-4 text-[13px]">
              <Link
                href="/dashboard/inventory/day-sheet"
                className="rounded-md border px-3 py-1.5 hover:bg-muted"
              >
                <ClipboardCheck className="mr-1 inline size-3.5" />
                Day sheet
              </Link>
              <Link
                href="/dashboard/inventory/props/kits"
                className="rounded-md border px-3 py-1.5 hover:bg-muted"
              >
                <Blocks className="mr-1 inline size-3.5" />
                Kits
              </Link>
              <Link
                href="/dashboard/inventory/props/gate-passes"
                className="rounded-md border px-3 py-1.5 hover:bg-muted"
              >
                <Warehouse className="mr-1 inline size-3.5" />
                Gate passes
              </Link>
              <Link
                href="/dashboard/inventory/vendors/orders"
                className="rounded-md border px-3 py-1.5 hover:bg-muted"
              >
                <Handshake className="mr-1 inline size-3.5" />
                Purchase orders
              </Link>
            </div>
          </>
        )}
      </div>
    </PagePanel>
  );
}
