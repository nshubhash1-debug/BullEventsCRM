"use client";

import * as React from "react";
import Link from "next/link";
import {
  CalendarClock,
  Goal,
  GraduationCap,
  Inbox,
  Landmark,
  LayoutDashboard,
  LifeBuoy,
  MapPinned,
  Megaphone,
  Network,
  Receipt,
  TicketCheck,
  UserRound,
  UsersRound,
  Wallet,
} from "lucide-react";

import { PagePanel } from "@/components/shell/page-panel";
import { hrApi, hrx, type HrDashboard } from "@/lib/hrms-api";

const MODULES = [
  { href: "/dashboard/hr/employees", label: "Employees", icon: UsersRound, blurb: "Master + org + statutory" },
  { href: "/dashboard/hr/org", label: "Org chart", icon: Network, blurb: "Reporting tree" },
  { href: "/dashboard/hr/attendance", label: "Attendance", icon: CalendarClock, blurb: "Geo punch + muster" },
  { href: "/dashboard/hr/leave", label: "Leave", icon: CalendarClock, blurb: "Balances & approvals" },
  { href: "/dashboard/hr/payroll", label: "Payroll", icon: Receipt, blurb: "Run, PF/ESIC, slips" },
  { href: "/dashboard/hr/recruitment", label: "Recruitment", icon: Inbox, blurb: "Kanban pipeline" },
  { href: "/dashboard/hr/interviews", label: "Interviews", icon: TicketCheck, blurb: "Scorecards" },
  { href: "/dashboard/hr/performance", label: "Performance", icon: Goal, blurb: "KRAs, goals, reviews" },
  { href: "/dashboard/hr/training", label: "Training", icon: GraduationCap, blurb: "Calendar & enrolment" },
  { href: "/dashboard/hr/helpdesk", label: "HR helpdesk", icon: LifeBuoy, blurb: "Employee tickets" },
  { href: "/dashboard/hr/expenses", label: "Expenses", icon: Wallet, blurb: "Claims → Accounts" },
  { href: "/dashboard/hr/policies", label: "Policies", icon: Megaphone, blurb: "Handbook & news" },
  { href: "/dashboard/hr/holidays", label: "Holidays", icon: Landmark, blurb: "Company calendar" },
  { href: "/dashboard/hr/lifecycle", label: "Lifecycle", icon: MapPinned, blurb: "Confirm / promote / transfer" },
  { href: "/dashboard/hr/assets", label: "Assets", icon: Landmark, blurb: "Issue & recover" },
  { href: "/dashboard/hr/me", label: "My HR", icon: UserRound, blurb: "ESS self-service" },
];

export default function HrOverviewPage() {
  const [data, setData] = React.useState<HrDashboard | null>(null);
  const [news, setNews] = React.useState<{ title: string; body: string }[]>([]);

  React.useEffect(() => {
    hrApi.dashboard().then(setData).catch(() => setData(null));
    hrx.announcements().then((rows) => setNews(rows.slice(0, 3))).catch(() => setNews([]));
  }, []);

  const cards = [
    { label: "On rolls", value: data?.headcount, href: "/dashboard/hr/employees" },
    { label: "Present today", value: data?.presentToday, href: "/dashboard/hr/attendance" },
    { label: "Absent", value: data?.absentToday, href: "/dashboard/hr/attendance" },
    { label: "On leave", value: data?.onLeaveToday, href: "/dashboard/hr/leave" },
    { label: "Pending people", value: data?.pendingApprovals, href: "/dashboard/hr/leave" },
    { label: "Expense queue", value: data?.pendingExpenses, href: "/dashboard/hr/expenses" },
    { label: "Open tickets", value: data?.openTickets, href: "/dashboard/hr/helpdesk" },
    { label: "Live goals", value: data?.activeGoals, href: "/dashboard/hr/performance" },
    { label: "Interviews (7d)", value: data?.interviewsThisWeek, href: "/dashboard/hr/interviews" },
    { label: "Holidays soon", value: data?.holidaysUpcoming, href: "/dashboard/hr/holidays" },
    { label: "Joiners", value: data?.joinersThisMonth, href: "/dashboard/hr/employees" },
    { label: "Exits", value: data?.exitsThisMonth, href: "/dashboard/hr/exit" },
  ];

  return (
    <PagePanel
      icon={LayoutDashboard}
      title="HR command centre"
      hint="Workforce suite mapped to Horilla and Frappe HR: people, time, talent, workplace and pay — running inside Bull Events, not a bolted-on Python app."
    >
      <div className="grid gap-3 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-4">
        {cards.map((card) => (
          <Link
            key={card.label}
            href={card.href}
            className="rounded-xl border bg-gradient-to-br from-card to-muted/30 p-4 shadow-xs hover:border-primary/40"
          >
            <p className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
              {card.label}
            </p>
            <p className="mt-2 text-2xl font-semibold tabular-nums">{card.value ?? "—"}</p>
          </Link>
        ))}
      </div>

      {news.length ? (
        <div className="mt-6 rounded-xl border bg-violet-500/5 p-4">
          <p className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-violet-700 dark:text-violet-300">
            Announcements
          </p>
          <ul className="space-y-2 text-[13px]">
            {news.map((n) => (
              <li key={n.title}>
                <span className="font-medium">{n.title}</span>
                <span className="text-muted-foreground"> — {n.body}</span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      <h2 className="mt-8 mb-3 text-[12px] font-semibold uppercase tracking-wide text-muted-foreground">
        Modules
      </h2>
      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
        {MODULES.map((m) => (
          <Link
            key={m.href}
            href={m.href}
            className="flex items-start gap-3 rounded-lg border px-3 py-2.5 hover:bg-muted/50"
          >
            <m.icon className="mt-0.5 size-4 text-primary" />
            <span>
              <span className="block text-[13px] font-medium">{m.label}</span>
              <span className="text-[11px] text-muted-foreground">{m.blurb}</span>
            </span>
          </Link>
        ))}
      </div>
    </PagePanel>
  );
}
