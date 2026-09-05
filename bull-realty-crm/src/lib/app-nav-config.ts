import {
  BadgeIndianRupee,
  BarChart3,
  BellRing,
  Blocks,
  Boxes,
  Briefcase,
  Building2,
  CalendarCheck2,
  CalendarClock,
  CalendarDays,
  CalendarRange,
  ClipboardCheck,
  Contact,
  Cpu,
  Database,
  GraduationCap,
  Goal,
  FileSignature,
  FileText,
  HardHat,
  Handshake,
  History,
  Headphones,
  HeartHandshake,
  Inbox,
  KanbanSquare,
  KeyRound,
  Calculator,
  HandCoins,
  Landmark,
  LayoutDashboard,
  LayoutGrid,
  LifeBuoy,
  ListChecks,
  Mail,
  MapPinned,
  Megaphone,
  MessageCircle,
  Network,
  Package,
  Palette,
  PhoneCall,
  Plug,
  Radio,
  Receipt,
  Wallet,
  Route,
  Settings,
  ShieldAlert,
  ShieldCheck,
  Signal,
  Sparkles,
  Target,
  Truck,
  TicketCheck,
  Timer,
  TrendingUp,
  UploadCloud,
  UserRound,
  UsersRound,
  Warehouse,
  Waypoints,
  Workflow,
  type LucideIcon,
} from "lucide-react";

/* ------------------------------------------------------------------ *
 * Apps
 *
 * The launcher picks an app; the navigation bar then shows only that app's
 * modules. Without this every tab from every business line lived on one bar,
 * and a leasing rep saw payroll sitting next to their pipeline.
 * ------------------------------------------------------------------ */

export type AppId =
  | "lead-management"
  | "inventory"
  | "post-sales"
  | "hr"
  | "customer-care"
  | "system-firmware"
  | "constructions"
  | "admin-console";

export interface AppDef {
  id: AppId;
  title: string;
  /** One line under the title in the launcher tile. */
  description: string;
  icon: LucideIcon;
  /** Where selecting the app lands. */
  href: string;
  /**
   * URL prefixes this app owns. The active app is derived from the pathname
   * rather than from stored state, so a deep link opens the right app even when
   * nothing was ever chosen in the launcher.
   */
  prefixes: string[];
  /** Tile treatment in the launcher — a tint per business line. */
  accent: string;
  /** False while the app is still a scaffold rather than a built module set. */
  live: boolean;
}

export const APPS: AppDef[] = [
  {
    id: "lead-management",
    title: "Lead Management",
    description: "Pipeline, follow-ups, venue visits and the enquiry desk.",
    icon: Waypoints,
    href: "/dashboard",
    prefixes: [
      "/dashboard/leads",
      "/dashboard/engagement",
      "/dashboard/automation",
      "/dashboard/calls",
      "/dashboard/sales",
      "/dashboard/reports",
      "/dashboard/calendar",
      "/dashboard/goals",
    ],
    accent: "bg-sky-600 text-white",
    live: true,
  },
  {
    // Everything an event is assembled from, on its own bar.
    //
    // It sat under Lead Management while props were the only thing in it, and
    // that stopped being defensible the moment the venues, the suppliers, the
    // crew and the trucks joined them: a godown keeper counting crates back in
    // has no business looking at somebody's pipeline, and the person who does
    // own the pipeline should not have to walk past four warehouse tabs to
    // reach a quotation.
    id: "inventory",
    title: "Inventory",
    description: "Venues, décor stock, suppliers, crew and the fleet.",
    icon: Warehouse,
    href: "/dashboard/inventory",
    prefixes: ["/dashboard/inventory"],
    accent: "bg-teal-600 text-white",
    live: true,
  },
  {
    id: "post-sales",
    title: "Post Sales",
    description: "Bookings, collections, paperwork and handover.",
    icon: BadgeIndianRupee,
    href: "/dashboard/post-sales",
    prefixes: ["/dashboard/post-sales"],
    accent: "bg-emerald-600 text-white",
    live: true,
  },
  {
    id: "hr",
    title: "HR",
    description: "Full HRMS — people, time, talent, workplace and pay.",
    icon: UsersRound,
    href: "/dashboard/hr",
    prefixes: ["/dashboard/hr"],
    accent: "bg-violet-600 text-white",
    live: true,
  },
  {
    id: "customer-care",
    title: "Customer Care",
    description: "Tickets, conversations, SLAs and knowledge.",
    icon: LifeBuoy,
    href: "/dashboard/care",
    prefixes: ["/dashboard/care"],
    accent: "bg-orange-600 text-white",
    live: false,
  },
  {
    id: "admin-console",
    title: "Setup",
    description: "Users, security, customisation and integrations.",
    icon: ShieldCheck,
    // Setup Home rather than the user list: an administrator arriving from the
    // launcher has a job in mind, and the home page plus Quick Find gets them
    // there faster than the first screen in the tree does.
    href: "/dashboard/setup",
    prefixes: [
      "/dashboard/setup",
      "/dashboard/users",
      "/dashboard/companies",
      "/dashboard/branches",
      "/dashboard/admin",
    ],
    accent: "bg-indigo-600 text-white",
    live: true,
  },
  {
    id: "system-firmware",
    title: "System Firmware",
    description: "Devices, builds, staged rollouts and telemetry.",
    icon: Cpu,
    href: "/dashboard/firmware",
    prefixes: ["/dashboard/firmware"],
    accent: "bg-slate-700 text-white",
    live: false,
  },
  {
    id: "constructions",
    title: "Constructions",
    description: "Projects, site progress, contractors and materials.",
    icon: HardHat,
    href: "/dashboard/construction",
    prefixes: ["/dashboard/construction"],
    accent: "bg-amber-600 text-white",
    live: false,
  },
];

export const DEFAULT_APP_ID: AppId = "lead-management";

export const APP_STORAGE_KEY = "bull_events_active_app";

export function appById(id: string | null | undefined): AppDef {
  return APPS.find((app) => app.id === id) ?? APPS[0];
}

/**
 * The app that owns this URL, or null when none does.
 *
 * Longest-prefix match, so `/dashboard/post-sales/collections` resolves to Post
 * Sales. Null is still a real answer rather than a failure — a route no app has
 * claimed yet resolves to nothing, and callers hold whichever app the user was
 * already in rather than throwing them back to the default one.
 */
export function appOwningPathname(pathname: string): AppDef | null {
  // The bare dashboard is Lead Management's home. It is matched exactly rather
  // than as a prefix, which would otherwise swallow every route beneath it.
  if (pathname === "/dashboard") return appById("lead-management");

  let best: AppDef | null = null;
  let bestLength = -1;

  for (const app of APPS) {
    for (const prefix of app.prefixes) {
      if (pathname !== prefix && !pathname.startsWith(`${prefix}/`)) continue;
      if (prefix.length <= bestLength) continue;
      best = app;
      bestLength = prefix.length;
    }
  }

  return best;
}

/** The same match, resolved to Lead Management when the URL belongs to no app. */
export function appForPathname(pathname: string): AppDef {
  return appOwningPathname(pathname) ?? appById(DEFAULT_APP_ID);
}

/* ------------------------------------------------------------------ *
 * Modules
 * ------------------------------------------------------------------ */

/** One destination — used in both the tab dropdown and the left rail. */
export interface SubNavItem {
  title: string;
  href: string;
  icon: LucideIcon;
  comingSoon?: boolean;
  /** Grouping heading inside the rail and the dropdown. */
  section?: string;
  /** One line under the title in the dropdown. */
  description?: string;
}

/** A recently-viewed record shown in a nav tab's dropdown. */
export interface RecentRecord {
  name: string;
  meta: string;
}

/** A quick action at the bottom of a nav tab's dropdown. */
export interface TabAction {
  label: string;
  href?: string;
}

/** App Launcher grouping — the "Apps" section of the launcher. */
export type ModuleGroup =
  | "Sales"
  | "Engagement"
  | "Administration"
  | "Analytics"
  | "Operations"
  /**
   * What the company owns and holds — venues on the books, décor on the shelf.
   * Split from Resources because the questions differ: stock is counted, and a
   * resource is engaged.
   */
  | "Stock"
  /** What the company engages for a job — suppliers, crew, vehicles. */
  | "Resources"
  | "People"
  | "Talent"
  | "Workplace"
  | "Service"
  | "Platform";

/** A top-level module — one tab on the navigation bar. */
export interface ModuleDef {
  title: string;
  href: string;
  icon: LucideIcon;
  group: ModuleGroup;
  /**
   * Which app owns this module. Left undefined for the administration modules,
   * which every app shares — a company, branch or user list is the same list
   * whichever business line you came in through.
   */
  app?: AppId;
  comingSoon?: boolean;
  /** Shown in the overflow "More" menu rather than inline. */
  overflow?: boolean;
  subNav?: SubNavItem[];
  recentLabel?: string;
  recent?: RecentRecord[];
  actions?: TabAction[];
  /**
   * What this module will hold once built. Rendered by the scaffold page so a
   * module that has no screens yet still states what belongs there instead of
   * showing an empty box.
   */
  blueprint?: string[];
}

/**
 * The navigation model.
 *
 * Tabs are grouped by what a rep is doing, not by which table the data lives in:
 * Leads is the sales record set, Engagement is outbound activity, Lead
 * Automation is the messaging channels, Calls is telephony. Everything
 * administrative sits in the overflow so the working tabs stay short.
 *
 * A module's `subNav` doubles as its tab dropdown, so there is exactly one list
 * of destinations per module and the two can never disagree.
 */
export const MODULES: ModuleDef[] = [
  /* ---------------- Lead Management ---------------- */
  {
    title: "Home",
    href: "/dashboard",
    icon: LayoutDashboard,
    group: "Analytics",
    app: "lead-management",
    recentLabel: "Recent dashboards",
    recent: [
      { name: "Sales Overview", meta: "Updated 5 min ago" },
      { name: "Lead Performance", meta: "Updated 12 min ago" },
      { name: "Branch Scorecard", meta: "Updated 1 hr ago" },
    ],
    subNav: [
      { title: "Dashboard", href: "/dashboard", icon: LayoutDashboard },
      {
        title: "Calendar",
        href: "/dashboard/calendar",
        icon: CalendarDays,
        description: "Venue visits, client meetings and tasks in one grid",
      },
      {
        title: "Goals",
        href: "/dashboard/goals",
        icon: Target,
        description: "Targets and how far along they are",
      },
    ],
  },

  {
    title: "Leads",
    href: "/dashboard/leads",
    icon: Waypoints,
    group: "Sales",
    app: "lead-management",
    recentLabel: "Recent leads",
    recent: [
      { name: "Kabir Sethi", meta: "Booked · Hot" },
      { name: "Divya Rao", meta: "Site Visit · Hot" },
      { name: "Vikram Malhotra", meta: "New · High" },
    ],
    actions: [{ label: "New Lead", href: "/dashboard/leads" }],
    subNav: [
      {
        title: "Leads",
        href: "/dashboard/leads",
        icon: Waypoints,
        description: "Every captured enquiry",
      },
      {
        title: "Enquiry board",
        href: "/dashboard/leads/board",
        icon: KanbanSquare,
        description: "Inquiry through to contract, by stage",
      },
      {
        title: "Follow-ups",
        href: "/dashboard/leads/follow-ups",
        icon: BellRing,
        description: "The dated commitment queue",
      },
      {
        title: "Pipeline",
        href: "/dashboard/leads/pipeline",
        icon: KanbanSquare,
        description: "Revenue deals — drag between stages",
      },
      {
        title: "Contacts",
        href: "/dashboard/leads/contacts",
        icon: Contact,
        description: "People, not enquiries",
      },
      {
        title: "Opportunities",
        href: "/dashboard/leads/opportunities",
        icon: Handshake,
        description: "Revenue-bearing deals",
      },
    ],
  },

  {
    title: "Engagement",
    href: "/dashboard/engagement/site-visits",
    icon: CalendarCheck2,
    group: "Engagement",
    app: "lead-management",
    subNav: [
      {
        title: "Venue Visits",
        href: "/dashboard/engagement/site-visits",
        icon: CalendarCheck2,
        description: "Clients touring a venue",
      },
      {
        title: "Client Meetings",
        href: "/dashboard/engagement/obm-visits",
        icon: Route,
        description: "Off-site meetings with clients and vendors",
      },
      {
        title: "Tasks",
        href: "/dashboard/engagement/tasks",
        icon: ListChecks,
        description: "Work items that are not a touchpoint",
      },
    ],
  },

  {
    title: "Lead Automation",
    href: "/dashboard/automation/whatsapp",
    icon: Workflow,
    group: "Engagement",
    app: "lead-management",
    subNav: [
      {
        title: "WhatsApp",
        href: "/dashboard/automation/whatsapp",
        icon: MessageCircle,
        description: "Queued WhatsApp touchpoints",
      },
      {
        title: "Email",
        href: "/dashboard/automation/email",
        icon: Mail,
        description: "Queued email touchpoints",
      },
    ],
  },

  {
    title: "Calls",
    href: "/dashboard/calls",
    icon: PhoneCall,
    group: "Engagement",
    app: "lead-management",
    subNav: [
      {
        title: "Manual Call Logs",
        href: "/dashboard/calls",
        icon: PhoneCall,
        description: "Every logged conversation",
      },
      {
        title: "MyOperator Call Dashboard",
        href: "/dashboard/calls/myoperator",
        icon: Headphones,
        description: "Volume, connect rate, wait time",
      },
      {
        title: "MyOperator Call Report",
        href: "/dashboard/calls/myoperator-report",
        icon: BarChart3,
        description: "Per-agent and per-day, exportable",
      },
    ],
  },

  {
    title: "Sales",
    href: "/dashboard/sales/quotations",
    icon: FileText,
    group: "Sales",
    app: "lead-management",
    subNav: [
      {
        title: "Quotations",
        href: "/dashboard/sales/quotations",
        icon: FileText,
        description: "Priced offers and revisions",
      },
      {
        title: "Approvals",
        href: "/dashboard/sales/approvals",
        icon: ShieldCheck,
        description: "Discounts and space booking sign-off",
      },
      {
        title: "Customer Database",
        href: "/dashboard/sales/customers",
        icon: Database,
        description: "Clients and corporate bookers",
      },
    ],
  },

  /*
   * Inventory's home, and the only screen that sees all five stock types at
   * once. Every other app opens on an overview; this one opened on a catalogue,
   * which answered "what do we own" when the question is "what needs me today".
   */
  {
    title: "Overview",
    href: "/dashboard/inventory",
    icon: Warehouse,
    group: "Operations",
    app: "inventory",
    blueprint: [
      "Every stock type's headline figure, and what is committed out of it",
      "Papers lapsing — supplier licences and vehicle permits",
      "Trades with nobody free, and gate passes past their return date",
    ],
    subNav: [
      {
        title: "Inventory Overview",
        href: "/dashboard/inventory",
        icon: Warehouse,
        description: "All five stock types, and what wants looking at",
      },
    ],
  },

  /*
   * The week itself. Kept beside the overview rather than inside it because a
   * coordinator lives on this screen and should not reach it through another.
   */
  {
    title: "Day Sheet",
    href: "/dashboard/inventory/day-sheet",
    icon: ClipboardCheck,
    group: "Operations",
    app: "inventory",
    blueprint: [
      "Every gate pass going out and every one due back",
      "Crew on site, with reporting times and phone numbers",
      "Suppliers due, what they cost, and whether they have confirmed",
      "Trucks on the road and what they are carrying",
    ],
    subNav: [
      {
        title: "Day Sheet",
        href: "/dashboard/inventory/day-sheet",
        icon: ClipboardCheck,
        description: "Everything committed over a window, in one view",
      },
    ],
  },

  {
    title: "Venues",
    href: "/dashboard/inventory/venues/diary",
    icon: Building2,
    group: "Stock",
    app: "inventory",
    subNav: [
      {
        title: "Venue Diary",
        href: "/dashboard/inventory/venues/diary",
        icon: CalendarDays,
        description: "Week calendar — spaces × dates",
      },
      {
        title: "Venue Board",
        href: "/dashboard/inventory/venues/board",
        icon: LayoutGrid,
        description: "Day board by date and slot",
      },
      {
        title: "Venues & Spaces",
        href: "/dashboard/inventory/venues",
        icon: Warehouse,
        description: "Catalogue of halls and lawns",
      },
      {
        title: "Venue Packages",
        href: "/dashboard/inventory/venues/packages",
        icon: Package,
        description: "Multi-space bundles and peak windows",
      },
    ],
  },

  /*
   * Props sit apart from the venue inventory above on purpose. A hall is one
   * thing booked by the date; a prop is a countable piece booked by the
   * quantity across a window, and folding the two into one tab produced a
   * screen that answered neither question well.
   */
  {
    title: "Props & Décor",
    href: "/dashboard/inventory/props",
    icon: Sparkles,
    group: "Stock",
    app: "inventory",
    subNav: [
      {
        title: "Catalogue",
        href: "/dashboard/inventory/props",
        icon: Sparkles,
        description: "Every prop with its photograph and usable count",
      },
      {
        title: "Availability",
        href: "/dashboard/inventory/props/availability",
        icon: CalendarRange,
        description: "What is free across a dispatch-to-return window",
      },
      {
        title: "Gate Passes",
        href: "/dashboard/inventory/props/gate-passes",
        icon: Truck,
        description: "Dispatch to an event, and the count back in",
      },
      {
        title: "Kits",
        href: "/dashboard/inventory/props/kits",
        icon: Blocks,
        description: "Sets that travel together — mandap, entrance, stage",
      },
      {
        title: "Rate Card",
        href: "/dashboard/inventory/props/rates",
        icon: BadgeIndianRupee,
        description: "Price the register — what each piece hires and replaces at",
      },
      {
        title: "Utilisation",
        href: "/dashboard/inventory/props/utilisation",
        icon: TrendingUp,
        description: "What earns its shelf space and what has not moved",
      },
      {
        title: "Godown Overview",
        href: "/dashboard/inventory/props/overview",
        icon: Boxes,
        description: "On the shelf, out on events, overdue returns",
      },
    ],
  },

  /*
   * Suppliers, crew and the fleet each get their own tab now that Inventory is
   * its own app. They were bundled into one "Resources" tab while they were
   * guests on the Lead Management bar and space was short; on their own bar the
   * bundling only cost a click, because the person booking a caterer and the
   * person rostering bearers are rarely the same person on the same afternoon.
   */
  {
    title: "Suppliers",
    href: "/dashboard/inventory/vendors",
    icon: Handshake,
    group: "Resources",
    app: "inventory",
    subNav: [
      {
        title: "Vendor Directory",
        href: "/dashboard/inventory/vendors",
        icon: Handshake,
        description: "Caterers, DJs, florists — rates and who is free",
      },
      {
        title: "Purchase Orders",
        href: "/dashboard/inventory/vendors/orders",
        icon: FileSignature,
        description: "What each supplier owes the event, and what we owe them",
      },
    ],
  },

  {
    title: "Crew",
    href: "/dashboard/inventory/crew",
    icon: HardHat,
    group: "Resources",
    app: "inventory",
    subNav: [
      {
        title: "Crew Roster",
        href: "/dashboard/inventory/crew",
        icon: HardHat,
        description: "Staff, freelancers and gangs — who can work the dates",
      },
    ],
  },

  {
    title: "Fleet",
    href: "/dashboard/inventory/fleet",
    icon: Truck,
    group: "Resources",
    app: "inventory",
    subNav: [
      {
        title: "Vehicles & Trips",
        href: "/dashboard/inventory/fleet",
        icon: Truck,
        description: "Load capacity, statutory papers and the runs they are on",
      },
    ],
  },

  {
    title: "Reports",
    href: "/dashboard/reports",
    icon: BarChart3,
    group: "Analytics",
    app: "lead-management",
    subNav: [
      { title: "Reports", href: "/dashboard/reports", icon: BarChart3 },
    ],
  },

  /* ---------------- Post Sales ---------------- */
  {
    title: "Overview",
    href: "/dashboard/post-sales",
    icon: LayoutDashboard,
    group: "Operations",
    app: "post-sales",
    blueprint: [
      "Booked-to-registered funnel with ageing at every stage",
      "Money due this week against money actually received",
      "Units awaiting possession, grouped by tower",
    ],
  },
  {
    title: "Bookings",
    href: "/dashboard/post-sales/bookings",
    icon: ClipboardCheck,
    group: "Operations",
    app: "post-sales",
    blueprint: [
      "Every booking from token to allotment letter",
      "Cancellation and transfer requests with their approval trail",
      "A link straight back to the originating lead and quotation",
    ],
  },
  {
    title: "Collections",
    href: "/dashboard/post-sales/collections",
    icon: BadgeIndianRupee,
    group: "Operations",
    app: "post-sales",
    blueprint: [
      "Milestone demands raised against each payment plan",
      "Receipts, part-payments and outstanding interest",
      "The reminder ladder that runs before a demand turns overdue",
    ],
  },
  {
    title: "Tax invoices",
    href: "/dashboard/post-sales/billing",
    icon: Receipt,
    group: "Operations",
    app: "post-sales",
    blueprint: [
      "The statutory document behind each demand, with its own number series",
      "CGST and SGST split on the land-abated value, not the whole",
      "Credit notes that reverse an issued invoice instead of editing it",
    ],
  },
  {
    title: "Cheque register",
    href: "/dashboard/post-sales/cheques",
    icon: Wallet,
    group: "Operations",
    app: "post-sales",
    blueprint: [
      "Post-dated cheques held, and which are due at the bank this week",
      "Clearing raises the receipt and allocates it; a bounce reverses both",
      "Return history, so a credit period is granted with eyes open",
    ],
  },
  {
    title: "TDS certificates",
    href: "/dashboard/post-sales/tds",
    icon: ShieldCheck,
    group: "Operations",
    app: "post-sales",
    blueprint: [
      "Every 194-IA deduction a buyer withheld and has not certified",
      "Form 16B and the 26QB acknowledgement against each receipt",
      "Short deposits flagged rather than accepted",
    ],
  },
  {
    title: "GST profiles",
    href: "/dashboard/post-sales/gst-profiles",
    icon: Building2,
    group: "Operations",
    app: "post-sales",
    blueprint: [
      "The GSTIN and place of supply invoices are raised under",
      "Per project, because a two-state developer holds two registrations",
      "The occupancy-certificate date after which the supply leaves GST",
    ],
  },
  {
    title: "Templates",
    href: "/dashboard/post-sales/templates",
    icon: FileSignature,
    group: "Operations",
    app: "post-sales",
    blueprint: [
      "The letters this system writes, as editable drafts",
      "Merge fields published rather than left to be discovered",
      "Preview against a real booking before three hundred copies go out",
    ],
  },
  {
    title: "Documentation",
    href: "/dashboard/post-sales/documentation",
    icon: FileSignature,
    group: "Operations",
    app: "post-sales",
    blueprint: [
      "Agreement drafting, stamping and registration checkpoints",
      "KYC and loan-sanction document checklist per buyer",
      "Version history, so the executed copy is never in doubt",
    ],
  },
  {
    title: "Handover",
    href: "/dashboard/post-sales/handover",
    icon: KeyRound,
    group: "Operations",
    app: "post-sales",
    blueprint: [
      "Possession scheduling and the snag list per unit",
      "Fit-out approvals and final clearance sign-off",
      "Warranty windows tracked after the keys change hands",
    ],
  },
  {
    title: "Customers",
    href: "/dashboard/post-sales/customers",
    icon: UserRound,
    group: "Operations",
    app: "post-sales",
    blueprint: [
      "The owner record behind every sold unit",
      "Co-applicants, nominees and power-of-attorney holders",
      "Money and paperwork history on one timeline",
    ],
  },

  /* ---------------- HR ---------------- */
  {
    title: "Overview",
    href: "/dashboard/hr",
    icon: LayoutDashboard,
    group: "People",
    app: "hr",
    blueprint: [
      "Headcount by branch, function and employment type",
      "Attrition and tenure trend against the hiring plan",
      "Anything needing an HR decision this week",
    ],
  },
  {
    title: "Employees",
    href: "/dashboard/hr/employees",
    icon: UsersRound,
    group: "People",
    app: "hr",
    blueprint: [
      "One record per employee — personal, statutory and job data",
      "Reporting lines, and the org chart they produce",
      "Confirmation, transfer and exit workflows",
    ],
    subNav: [
      { title: "Directory", href: "/dashboard/hr/employees", icon: UsersRound, description: "Master list" },
      { title: "Org chart", href: "/dashboard/hr/org", icon: Network, description: "Reporting tree" },
      { title: "Lifecycle", href: "/dashboard/hr/lifecycle", icon: MapPinned, description: "Confirm, promote, transfer" },
      { title: "Assets", href: "/dashboard/hr/assets", icon: Package, description: "Issue and recover" },
    ],
  },
  {
    title: "Org chart",
    href: "/dashboard/hr/org",
    icon: Network,
    group: "People",
    app: "hr",
    overflow: true,
  },
  {
    title: "Lifecycle",
    href: "/dashboard/hr/lifecycle",
    icon: MapPinned,
    group: "People",
    app: "hr",
    overflow: true,
  },
  {
    title: "Assets",
    href: "/dashboard/hr/assets",
    icon: Package,
    group: "People",
    app: "hr",
    overflow: true,
  },
  {
    title: "Attendance",
    href: "/dashboard/hr/attendance",
    icon: CalendarClock,
    group: "People",
    app: "hr",
    blueprint: [
      "Daily punches, shifts and regularisation requests",
      "Field-staff check-ins geotagged to the site visited",
      "A monthly muster roll that feeds straight into payroll",
    ],
  },
  {
    title: "Leave",
    href: "/dashboard/hr/leave",
    icon: CalendarDays,
    group: "People",
    app: "hr",
    blueprint: [
      "Leave periods, policies and the allocation behind every balance",
      "Apply, approve and cancel with the manager in the loop",
      "Compensatory off for Sundays worked, encashment for what is left",
      "A blocked wedding season, so cover gaps show up before they bite",
    ],
    subNav: [
      {
        title: "Requests",
        href: "/dashboard/hr/leave",
        icon: CalendarDays,
        description: "Apply and approve",
      },
      {
        title: "Register",
        href: "/dashboard/hr/leave-register",
        icon: CalendarClock,
        description: "Balances and allocations",
      },
      {
        title: "Roster",
        href: "/dashboard/hr/roster",
        icon: CalendarRange,
        description: "Shifts for a day",
      },
    ],
  },
  {
    title: "Payroll",
    href: "/dashboard/hr/payroll",
    icon: Receipt,
    group: "People",
    app: "hr",
    blueprint: [
      "Salary structures, incentives and sales commission payouts",
      "PF, ESI, professional tax and TDS computation",
      "Payslips, the month's remittances and the statutory registers",
    ],
    subNav: [
      {
        title: "Runs",
        href: "/dashboard/hr/payroll",
        icon: Receipt,
        description: "Slips and remittances",
      },
      {
        title: "Pay structures",
        href: "/dashboard/hr/pay-structures",
        icon: Calculator,
        description: "Components and formulas",
      },
      {
        title: "Advances",
        href: "/dashboard/hr/advances",
        icon: HandCoins,
        description: "Advances and one-off pay",
      },
      {
        title: "Statutory",
        href: "/dashboard/hr/statutory",
        icon: Landmark,
        description: "Rates, slabs, declarations",
      },
    ],
  },
  {
    title: "Recruitment",
    href: "/dashboard/hr/recruitment",
    icon: Inbox,
    group: "Talent",
    app: "hr",
    blueprint: [
      "Requisitions raised against approved headcount",
      "Candidate pipeline from applied through to offer accepted",
      "Interview scorecards and offer letter generation",
    ],
    subNav: [
      { title: "Pipeline", href: "/dashboard/hr/recruitment", icon: Inbox, description: "Kanban stages" },
      { title: "Hiring", href: "/dashboard/hr/hiring", icon: Briefcase, description: "Plan, requisitions, offers, referrals" },
      { title: "Interviews", href: "/dashboard/hr/interviews", icon: TicketCheck, description: "Scorecards" },
      { title: "Performance", href: "/dashboard/hr/performance", icon: Goal, description: "Goals and cycles" },
      { title: "Appraisals", href: "/dashboard/hr/appraisals", icon: ClipboardCheck, description: "Templates, scoring, 360 feedback" },
      { title: "Training", href: "/dashboard/hr/training", icon: GraduationCap, description: "Enrolment" },
    ],
  },
  {
    title: "Interviews",
    href: "/dashboard/hr/interviews",
    icon: TicketCheck,
    group: "Talent",
    app: "hr",
    overflow: true,
  },
  {
    title: "Performance",
    href: "/dashboard/hr/performance",
    icon: Goal,
    group: "Talent",
    app: "hr",
    overflow: true,
  },
  {
    title: "Training",
    href: "/dashboard/hr/training",
    icon: GraduationCap,
    group: "Talent",
    app: "hr",
    overflow: true,
  },
  {
    title: "Helpdesk",
    href: "/dashboard/hr/helpdesk",
    icon: LifeBuoy,
    group: "Workplace",
    app: "hr",
    subNav: [
      { title: "Tickets", href: "/dashboard/hr/helpdesk", icon: LifeBuoy, description: "Employee tickets" },
      { title: "Expenses", href: "/dashboard/hr/expenses", icon: Wallet, description: "Claims" },
      { title: "Policies", href: "/dashboard/hr/policies", icon: Megaphone, description: "Handbook" },
      { title: "Holidays", href: "/dashboard/hr/holidays", icon: Landmark, description: "Calendar" },
      { title: "Workplace", href: "/dashboard/hr/workplace", icon: HeartHandshake, description: "Checklists, grievances, skills" },
      { title: "Timesheets", href: "/dashboard/hr/timesheets", icon: Timer, description: "Hours by event, and travel" },
    ],
  },
  {
    title: "Expenses",
    href: "/dashboard/hr/expenses",
    icon: Wallet,
    group: "Workplace",
    app: "hr",
    overflow: true,
  },
  {
    title: "Policies",
    href: "/dashboard/hr/policies",
    icon: Megaphone,
    group: "Workplace",
    app: "hr",
    overflow: true,
  },
  {
    title: "Holidays",
    href: "/dashboard/hr/holidays",
    icon: Landmark,
    group: "Workplace",
    app: "hr",
    overflow: true,
  },
  {
    title: "My HR",
    href: "/dashboard/hr/me",
    icon: UserRound,
    group: "People",
    app: "hr",
  },
  {
    // Deliberately not the roster. This screen exists to push a deployment's
    // overtime and incentive into that employee's payroll month; rostering —
    // including the freelancers and gangs who are not on payroll at all — is
    // Inventory's Crew tab.
    title: "Event crew payroll",
    href: "/dashboard/hr/crew",
    icon: CalendarCheck2,
    group: "People",
    app: "hr",
    subNav: [
      {
        title: "Event crew payroll",
        href: "/dashboard/hr/crew",
        icon: CalendarCheck2,
        description: "Employee deployments whose OT and incentive hit payroll",
      },
      {
        title: "Crew roster",
        href: "/dashboard/inventory/crew",
        icon: HardHat,
        description: "Staff, freelancers and gangs — who can work which dates",
      },
    ],
  },
  {
    title: "Exit & F&F",
    href: "/dashboard/hr/exit",
    icon: FileSignature,
    group: "People",
    app: "hr",
  },
  {
    title: "HR setup",
    href: "/dashboard/hr/setup",
    icon: Settings,
    group: "People",
    app: "hr",
  },

  /* ---------------- Customer Care ---------------- */
  {
    title: "Overview",
    href: "/dashboard/care",
    icon: LayoutDashboard,
    group: "Service",
    app: "customer-care",
    blueprint: [
      "Open, breached and about-to-breach tickets at a glance",
      "First-response and resolution time by queue",
      "What customers are complaining about most this month",
    ],
  },
  {
    title: "Tickets",
    href: "/dashboard/care/tickets",
    icon: TicketCheck,
    group: "Service",
    app: "customer-care",
    blueprint: [
      "Intake, triage and assignment driven by priority rules",
      "Linked to the unit, booking or lead the issue is about",
      "An escalation path once a ticket ages past its target",
    ],
  },
  {
    title: "Conversations",
    href: "/dashboard/care/conversations",
    icon: MessageCircle,
    group: "Service",
    app: "customer-care",
    blueprint: [
      "WhatsApp, email and call threads on one timeline",
      "Canned replies with the customer details merged in",
      "Internal notes kept separate from what the customer sees",
    ],
  },
  {
    title: "SLA",
    href: "/dashboard/care/sla",
    icon: Timer,
    group: "Service",
    app: "customer-care",
    blueprint: [
      "Response and resolution targets per priority and channel",
      "Business-hours calendars, including branch holidays",
      "Breach reporting, with the reason recorded against each",
    ],
  },
  {
    title: "Knowledge",
    href: "/dashboard/care/knowledge",
    icon: FileText,
    group: "Service",
    app: "customer-care",
    blueprint: [
      "Answer articles an agent can paste straight into a reply",
      "Project-specific FAQs kept beside the project record",
      "Which articles actually deflected a ticket",
    ],
  },

  /* ---------------- System Firmware ---------------- */
  {
    title: "Overview",
    href: "/dashboard/firmware",
    icon: LayoutDashboard,
    group: "Platform",
    app: "system-firmware",
    blueprint: [
      "Fleet health — online, stale and unreachable devices",
      "Version spread across the estate",
      "Rollouts in flight, and the failure rate of each",
    ],
  },
  {
    title: "Devices",
    href: "/dashboard/firmware/devices",
    icon: Radio,
    group: "Platform",
    app: "system-firmware",
    blueprint: [
      "Every registered device with its site, tower and unit",
      "Current firmware version, uptime and last check-in",
      "Provisioning, decommissioning and RMA history",
    ],
  },
  {
    title: "Builds",
    href: "/dashboard/firmware/builds",
    icon: Blocks,
    group: "Platform",
    app: "system-firmware",
    blueprint: [
      "Signed firmware artefacts with their release notes",
      "The hardware-revision compatibility matrix",
      "Promotion from internal to beta to general availability",
    ],
  },
  {
    title: "Rollouts",
    href: "/dashboard/firmware/rollouts",
    icon: UploadCloud,
    group: "Platform",
    app: "system-firmware",
    blueprint: [
      "Staged waves with a hold gate between each",
      "An automatic pause when the failure rate crosses its threshold",
      "One-click rollback to the previously good build",
    ],
  },
  {
    title: "Telemetry",
    href: "/dashboard/firmware/telemetry",
    icon: Signal,
    group: "Platform",
    app: "system-firmware",
    blueprint: [
      "Crash reports and error codes grouped by signature",
      "Battery, signal and sensor trends per device model",
      "Alerting when a metric drifts after a version change",
    ],
  },

  /* ---------------- Constructions ---------------- */
  {
    title: "Overview",
    href: "/dashboard/construction",
    icon: LayoutDashboard,
    group: "Operations",
    app: "constructions",
    blueprint: [
      "Every active project against its committed completion date",
      "Slippage, cost variance, and the reasons behind both",
      "Approvals and inspections due in the next fortnight",
    ],
  },
  {
    title: "Projects",
    href: "/dashboard/construction/projects",
    icon: Building2,
    group: "Operations",
    app: "constructions",
    blueprint: [
      "Project master with towers, phases and unit counts",
      "Statutory approvals — RERA, commencement, occupancy",
      "The same project records the sales inventory is built on",
    ],
  },
  {
    title: "Progress",
    href: "/dashboard/construction/progress",
    icon: TrendingUp,
    group: "Operations",
    app: "constructions",
    blueprint: [
      "Slab-wise and activity-wise completion percentages",
      "Site photographs stamped with date and location",
      "Construction-linked payment milestones triggered on sign-off",
    ],
  },
  {
    title: "Contractors",
    href: "/dashboard/construction/contractors",
    icon: HardHat,
    group: "Operations",
    app: "constructions",
    blueprint: [
      "Work orders, rate contracts and retention amounts",
      "Running account bills with measurement-book backing",
      "Contractor performance and safety record over time",
    ],
  },
  {
    title: "Materials",
    href: "/dashboard/construction/materials",
    icon: Boxes,
    group: "Operations",
    app: "constructions",
    blueprint: [
      "Indents, purchase orders and goods-receipt notes",
      "Site-wise stock, with consumption against estimate",
      "Wastage and reconciliation at the end of each phase",
    ],
  },
  {
    title: "Quality & Safety",
    href: "/dashboard/construction/safety",
    icon: ShieldAlert,
    group: "Operations",
    app: "constructions",
    blueprint: [
      "Checklists per activity, signed off before the next stage",
      "Incident reporting with root cause and corrective action",
      "Third-party inspection reports filed against the project",
    ],
  },

  /* ---------------- Setup ---------------- */
  //
  // Two tabs, not eleven. Setup carries its own left-hand tree and Quick Find,
  // so repeating those destinations on the context bar gave the same links two
  // places to disagree about what was active and cost a row of chrome for
  // nothing. Salesforce lands on the same answer: Setup's bar is Home and
  // Object Manager, and everything else is in the rail.
  {
    title: "Setup Home",
    href: "/dashboard/setup",
    icon: Settings,
    group: "Administration",
    app: "admin-console",
  },
  {
    title: "Object Manager",
    href: "/dashboard/companies/customisation",
    icon: Blocks,
    group: "Administration",
    app: "admin-console",
    subNav: [
      {
        title: "Custom fields",
        href: "/dashboard/companies/customisation",
        icon: Blocks,
        description: "What this company added to leads, contacts and deals",
      },
      {
        title: "Pick lists",
        href: "/dashboard/companies/customisation?tab=lists",
        icon: ListChecks,
        description: "Sources, loss reasons and the rest of the vocabulary",
      },
      {
        title: "Branding",
        href: "/dashboard/companies/customisation?tab=branding",
        icon: Palette,
        description: "What a customer sees on a quotation",
      },
    ],
  },
  {
    title: "Partners",
    href: "/dashboard/partners",
    icon: ShieldCheck,
    group: "Sales",
    app: "lead-management",
    comingSoon: true,
    overflow: true,
  },
  {
    title: "Documents",
    href: "/dashboard/documents",
    icon: FileSignature,
    group: "Administration",
    comingSoon: true,
    overflow: true,
  },
  {
    title: "Invoices",
    href: "/dashboard/invoices",
    icon: Receipt,
    group: "Administration",
    comingSoon: true,
    overflow: true,
  },
  {
    title: "Settings",
    href: "/dashboard/settings",
    icon: Settings,
    group: "Administration",
    comingSoon: true,
    overflow: true,
  },
];

/**
 * The modules one app shows: its own, plus the administration modules that
 * declare no `app` and therefore belong to all of them.
 */
export function modulesForApp(appId: AppId): ModuleDef[] {
  return MODULES.filter(
    (entry) => entry.app === undefined || entry.app === appId
  );
}

export function primaryModulesForApp(appId: AppId): ModuleDef[] {
  return modulesForApp(appId).filter((entry) => !entry.overflow);
}

export function overflowModulesForApp(appId: AppId): ModuleDef[] {
  return modulesForApp(appId).filter((entry) => entry.overflow);
}

export const MODULE_GROUPS: ModuleGroup[] = [
  "Sales",
  "Engagement",
  "Operations",
  "Stock",
  "Resources",
  "People",
  "Talent",
  "Workplace",
  "Service",
  "Platform",
  "Analytics",
  "Administration",
];

/**
 * Longest-prefix match over every destination, not just the tab's own href.
 *
 * Modules do not own a tidy URL prefix — Calls covers `/dashboard/calls/*`
 * while Engagement covers `/dashboard/engagement/*` — so matching on the tab
 * href alone would leave sub-pages highlighting the wrong tab.
 *
 * Scoped to one app, because several apps deliberately reuse the same module
 * title ("Overview") and an unscoped search would land on the wrong one.
 */
export function moduleForPathname(pathname: string, appId?: AppId): ModuleDef {
  const scope = modulesForApp(appId ?? appForPathname(pathname).id);
  let best = scope[0] ?? MODULES[0];
  let bestLength = -1;

  for (const entry of scope) {
    const candidates = [
      entry.href,
      ...(entry.subNav ?? []).map((item) => item.href),
    ];

    for (const href of candidates) {
      if (href === "/dashboard") {
        // The dashboard root would otherwise prefix-match everything.
        if (pathname === "/dashboard" && bestLength < 0) best = entry;
        continue;
      }

      if (pathname !== href && !pathname.startsWith(`${href}/`)) continue;
      if (href.length <= bestLength) continue;

      best = entry;
      bestLength = href.length;
    }
  }

  return best;
}

/** Every destination a search box can jump to, across all apps. */
export function navigableModules() {
  return MODULES.filter((entry) => !entry.comingSoon);
}

/** The module registered at exactly this URL — used by the scaffold pages. */
export function moduleByHref(href: string): ModuleDef | undefined {
  return MODULES.find((entry) => entry.href === href);
}
