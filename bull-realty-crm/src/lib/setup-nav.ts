import {
  Blocks,
  Boxes,
  Building2,
  Clock,
  Database,
  Download,
  Fingerprint,
  Gauge,
  History,
  KeyRound,
  Layers,
  ListChecks,
  Lock,
  MapPinned,
  Palette,
  Plug,
  Receipt,
  Repeat,
  ScrollText,
  ShieldCheck,
  Split,
  Timer,
  UploadCloud,
  UserRound,
  UsersRound,
  Workflow,
  type LucideIcon,
} from "lucide-react";

/**
 * The Setup tree.
 *
 * Setup is a different shell from the rest of the CRM, and deliberately so. A
 * working app is a short bar of tabs somebody uses all day; setup is a deep,
 * shallow-visited tree that an administrator lands in two or three times a
 * month knowing what they want and not where it lives. Salesforce answers that
 * with a left-hand tree plus Quick Find, and thirty years of admins have been
 * trained on it — which is the strongest argument for copying the pattern
 * rather than inventing one.
 *
 * Every entry carries `keywords` because Quick Find is the actual navigation.
 * The tree is how you browse when you do not know the name; the search box is
 * how you get there when you do, and it only works if the entry answers to the
 * words people already use — "password", "IP", "SSO" all have to reach the
 * login-policy screen whatever it ends up being called.
 */
export interface SetupItem {
  title: string;
  href: string;
  icon: LucideIcon;
  /** One line under the title in Quick Find and on the section page. */
  description: string;
  /** Other words that should find this. Never shown; only matched. */
  keywords: string[];
  /** Listed and findable, but not yet built. */
  soon?: boolean;
}

export interface SetupSection {
  title: string;
  /** Grouping heading in the rail — Salesforce's "Administration" / "Platform Tools". */
  group: SetupGroup;
  icon: LucideIcon;
  items: SetupItem[];
}

export type SetupGroup = "Administration" | "Platform Tools" | "Security" | "Monitoring";

export const SETUP_GROUPS: SetupGroup[] = [
  "Administration",
  "Platform Tools",
  "Security",
  "Monitoring",
];

export const SETUP_HOME = "/dashboard/setup";

export const SETUP_TREE: SetupSection[] = [
  /* ---------------- Administration ---------------- */
  {
    title: "Users",
    group: "Administration",
    icon: UsersRound,
    items: [
      {
        title: "Users",
        href: "/dashboard/users",
        icon: UserRound,
        description: "Everyone with a seat, and what each one reaches.",
        keywords: ["people", "staff", "seats", "invite", "deactivate", "employee", "agent", "rep"],
      },
      {
        title: "Roles & access",
        href: "/dashboard/users/roles",
        icon: ShieldCheck,
        description: "What each role means, and the permission matrix behind it.",
        keywords: ["permission", "profile", "matrix", "crud", "object", "field level security", "fls", "scope"],
      },
      {
        title: "Teams",
        href: "/dashboard/users/teams",
        icon: UsersRound,
        description: "Managers and their people. Move a batch at once.",
        keywords: ["manager", "team", "roster", "reassign", "org chart", "span of control"],
      },
      {
        title: "Reporting lines",
        href: "/dashboard/users/hierarchy",
        icon: Workflow,
        description: "The tree, one person at a time.",
        keywords: ["hierarchy", "reports to", "org chart", "manager", "escalation"],
      },
      {
        title: "Permission sets",
        href: "/dashboard/users/permission-sets",
        icon: Layers,
        description: "Additive grants for the person who needs one more thing.",
        keywords: ["permission set", "temporary", "elevated", "grant", "expiry", "additive"],
      },
      {
        title: "Login history",
        href: "/dashboard/users/login-history",
        icon: Fingerprint,
        description: "Who signed in, from where, and what was refused.",
        keywords: ["sign in", "login", "audit", "failed", "ip", "attempts", "lockout"],
      },
    ],
  },
  {
    title: "Company",
    group: "Administration",
    icon: Building2,
    items: [
      {
        title: "Company information",
        href: "/dashboard/companies",
        icon: Building2,
        description: "The tenants on this platform and their details.",
        keywords: ["organisation", "org", "tenant", "workspace", "company"],
      },
      {
        title: "Branches",
        href: "/dashboard/branches",
        icon: MapPinned,
        description: "Every office. Users, leads and venues are scoped to these.",
        keywords: ["office", "location", "territory", "city", "region"],
      },
      {
        title: "Subscription",
        href: "/dashboard/companies/subscription",
        icon: Receipt,
        description: "Plan, limits, usage and what it costs.",
        keywords: ["plan", "billing", "seats", "licence", "license", "quota", "trial", "upgrade", "invoice"],
      },
      {
        title: "Setup checklist",
        href: "/dashboard/companies/setup",
        icon: ListChecks,
        description: "What this workspace still needs, counted from real data.",
        keywords: ["onboarding", "getting started", "wizard", "first run", "todo"],
      },
      {
        title: "Business hours & holidays",
        href: "/dashboard/companies/business-hours",
        icon: Clock,
        description: "When the clock runs, so an SLA does not breach over a weekend.",
        keywords: ["sla", "working hours", "holiday", "calendar", "timezone", "shift"],
      },
    ],
  },
  {
    title: "Data",
    group: "Administration",
    icon: Database,
    items: [
      {
        title: "Import",
        href: "/dashboard/data/import",
        icon: UploadCloud,
        description: "Bring in leads, contacts and inventory from a spreadsheet.",
        keywords: ["import", "csv", "excel", "upload", "migrate", "bulk", "frappe"],
      },
      {
        title: "Export",
        href: "/dashboard/data/export",
        icon: Download,
        description: "A full copy of this workspace's data, on demand or scheduled.",
        keywords: ["export", "backup", "csv", "download", "dpdp", "gdpr", "portability"],
      },
      {
        title: "Duplicate rules",
        href: "/dashboard/data/duplicates",
        icon: Repeat,
        description: "What counts as the same person, and what happens when one arrives.",
        keywords: ["duplicate", "dedupe", "merge", "matching", "same lead"],
      },
      {
        title: "Mass transfer",
        href: "/dashboard/data/transfer",
        icon: Split,
        description: "Move a departing rep's whole book to somebody else.",
        keywords: ["reassign", "transfer", "bulk owner", "leaver", "handover"],
      },
    ],
  },

  /* ---------------- Platform Tools ---------------- */
  {
    title: "Objects & fields",
    group: "Platform Tools",
    icon: Boxes,
    items: [
      {
        title: "Object manager",
        href: "/dashboard/companies/customisation",
        icon: Blocks,
        description: "The fields this company added to leads, contacts and deals.",
        keywords: ["custom field", "field", "schema", "object", "lead form", "layout", "picklist"],
      },
      {
        title: "Pick lists",
        href: "/dashboard/companies/customisation?tab=lists",
        icon: ListChecks,
        description: "Lead sources, loss reasons and the rest of your vocabulary.",
        keywords: ["dropdown", "options", "source", "loss reason", "stage", "values", "vocabulary"],
      },
      {
        title: "Branding",
        href: "/dashboard/companies/customisation?tab=branding",
        icon: Palette,
        description: "What a customer sees on a quotation and its public link.",
        keywords: ["logo", "colour", "color", "theme", "white label", "quotation", "pdf"],
      },
    ],
  },
  {
    title: "Automation",
    group: "Platform Tools",
    icon: Workflow,
    items: [
      {
        title: "Assignment rules",
        href: "/dashboard/automation/assignment",
        icon: Split,
        description: "Who a new enquiry lands on, decided by the enquiry.",
        keywords: ["routing", "round robin", "assign", "owner", "distribution", "lead routing"],
      },
      {
        title: "Approval processes",
        href: "/dashboard/automation/approvals",
        icon: ShieldCheck,
        description: "What needs sign-off, from whom, and what happens on refusal.",
        keywords: ["approval", "discount", "sign off", "escalate", "workflow"],
      },
      {
        title: "Escalation & SLA rules",
        href: "/dashboard/automation/escalation",
        icon: Timer,
        description: "What happens when nobody responds in time.",
        keywords: ["sla", "escalation", "breach", "overdue", "response time", "alert"],
      },
      {
        title: "Scheduled jobs",
        href: "/dashboard/automation/jobs",
        icon: Clock,
        description: "The work the platform runs on a clock, and whether it ran.",
        keywords: ["cron", "background", "job", "schedule", "batch", "nightly"],
      },
    ],
  },
  {
    title: "Integrations",
    group: "Platform Tools",
    icon: Plug,
    items: [
      {
        title: "Connectors",
        href: "/dashboard/setup/connectors",
        icon: Plug,
        description: "Portals, telephony, Meta and your own website, in one place.",
        keywords: [
          "connector", "connected app", "integration", "portal", "99acres",
          "magicbricks", "housing", "meta", "facebook", "instagram", "lead ads",
          "google ads", "myoperator", "telephony", "call", "whatsapp",
          "website", "web to lead", "form", "landing page",
        ],
      },
      {
        title: "API keys",
        href: "/dashboard/companies/integrations",
        icon: KeyRound,
        description: "Credentials for systems calling the CRM's own API.",
        keywords: ["api", "key", "token", "bearer", "public api", "scope"],
      },
      {
        title: "Webhooks",
        href: "/dashboard/companies/integrations?tab=webhooks",
        icon: Plug,
        description: "Where this CRM posts events, and how they are signed.",
        keywords: ["webhook", "event", "callback", "notify", "hmac", "signature"],
      },
      {
        title: "Delivery log",
        href: "/dashboard/companies/integrations?tab=deliveries",
        icon: ScrollText,
        description: "What was sent, what came back, and what is still queued.",
        keywords: ["delivery", "retry", "failed", "replay", "webhook log"],
      },
    ],
  },

  /* ---------------- Security ---------------- */
  {
    title: "Security",
    group: "Security",
    icon: Lock,
    items: [
      {
        title: "Record visibility",
        href: "/dashboard/users/roles?tab=visibility",
        icon: ShieldCheck,
        description: "The floor everything else opens up from, per object.",
        keywords: ["sharing", "org wide default", "owd", "private", "public read", "visibility"],
      },
      {
        title: "Sharing rules",
        href: "/dashboard/security/sharing-rules",
        icon: Split,
        description: "Standing rules that widen who sees a slice of records.",
        keywords: ["sharing", "rule", "criteria", "owner based", "widen", "grant access"],
      },
      {
        title: "Login policies",
        href: "/dashboard/security/login-policies",
        icon: Fingerprint,
        description: "Which hours, which days and which addresses a seat may sign in from.",
        keywords: ["ip range", "password policy", "hours", "restrict", "cidr", "idle timeout", "mfa", "sso"],
      },
      {
        title: "Security health check",
        href: "/dashboard/security/health",
        icon: Gauge,
        description: "This workspace's settings against the recommended baseline.",
        keywords: ["health", "score", "baseline", "hardening", "risk", "compliance"],
      },
    ],
  },

  /* ---------------- Monitoring ---------------- */
  {
    title: "Monitoring",
    group: "Monitoring",
    icon: History,
    items: [
      {
        title: "Audit trail",
        href: "/dashboard/companies/audit",
        icon: History,
        description: "Who changed what, and when.",
        keywords: ["audit", "history", "change log", "who changed", "trail", "compliance"],
      },
      {
        title: "Usage",
        href: "/dashboard/companies/subscription?tab=plan",
        icon: Gauge,
        description: "Seats, records and activity over time.",
        keywords: ["usage", "metering", "counters", "storage", "limits", "consumption"],
      },
    ],
  },
];

/** Every destination, flattened — what Quick Find searches. */
export const SETUP_ITEMS: (SetupItem & { section: string; group: SetupGroup })[] =
  SETUP_TREE.flatMap((section) =>
    section.items.map((item) => ({
      ...item,
      section: section.title,
      group: section.group,
    }))
  );

/**
 * Ranks setup destinations against what somebody typed.
 *
 * Ranked rather than filtered because an administrator types three or four
 * characters and expects the thing they meant at the top — "perm" has to reach
 * Roles & access before Permission sets, since one is built and one is not.
 * A title match outranks a section match, which outranks a keyword, and
 * anything not yet built sinks below everything that is.
 */
export function searchSetup(query: string) {
  const needle = query.trim().toLowerCase();
  if (!needle) return [];

  return SETUP_ITEMS.map((item) => {
    const title = item.title.toLowerCase();

    let score = 0;
    if (title === needle) score = 100;
    else if (title.startsWith(needle)) score = 80;
    else if (title.includes(needle)) score = 60;
    else if (item.section.toLowerCase().includes(needle)) score = 40;
    else if (item.keywords.some((k) => k.includes(needle))) score = 30;
    else if (item.description.toLowerCase().includes(needle)) score = 10;

    // A destination that does not exist yet is still worth finding — it tells
    // the administrator the answer is "not yet" rather than "look harder" — but
    // it must not outrank a screen they can actually open.
    //
    // The penalty is 55 rather than a flat "built always wins" because both
    // failure modes are real. Too small and "perm" leads with Permission sets
    // over the Roles & access matrix that exists; too absolute and typing a
    // soon item's exact name buries it under whatever merely mentions the word.
    // 55 is the width of one match tier plus a bit: an exact title still beats a
    // keyword hit, and a keyword hit no longer beats a built title.
    //
    // Floored at 1 rather than dropped, so a soon item stays findable at the
    // bottom instead of looking like it does not exist.
    if (item.soon) score = Math.max(1, score - 55);

    return { item, score };
  })
    .filter((row) => row.score > 0)
    .sort((a, b) => b.score - a.score || a.item.title.localeCompare(b.item.title))
    .slice(0, 12)
    .map((row) => row.item);
}

/** The tree entry that owns a URL, for the rail's active state and the breadcrumb. */
export function setupItemFor(pathname: string) {
  let best: (SetupItem & { section: string; group: SetupGroup }) | null = null;

  for (const item of SETUP_ITEMS) {
    const path = item.href.split("?")[0];
    if (pathname !== path) continue;
    // The bare path wins over a tab-qualified sibling, which is what the rail
    // should highlight when somebody lands without a tab in the URL.
    if (best === null || !item.href.includes("?")) best = item;
  }

  return best;
}
