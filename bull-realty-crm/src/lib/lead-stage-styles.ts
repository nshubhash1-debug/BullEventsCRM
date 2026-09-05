export const stageStyles: Record<
  string,
  { dot: string; badge: string; bar: string }
> = {
  New: {
    dot: "bg-blue-500",
    badge:
      "border-blue-500/20 bg-blue-500/10 text-blue-700 dark:text-blue-400",
    bar: "bg-blue-500",
  },
  Contacted: {
    dot: "bg-sky-500",
    badge: "border-sky-500/20 bg-sky-500/10 text-sky-700 dark:text-sky-400",
    bar: "bg-sky-500",
  },
  Qualified: {
    dot: "bg-teal-500",
    badge: "border-teal-500/20 bg-teal-500/10 text-teal-700 dark:text-teal-400",
    bar: "bg-teal-500",
  },
  /** Surfaced as "Client Meeting" — the planner visits the client. */
  ObmVisit: {
    dot: "bg-cyan-500",
    badge: "border-cyan-500/20 bg-cyan-500/10 text-cyan-700 dark:text-cyan-400",
    bar: "bg-cyan-500",
  },
  /** Surfaced as "Venue Visit" — the client tours the venue. */
  SiteVisit: {
    dot: "bg-amber-500",
    badge:
      "border-amber-500/20 bg-amber-500/10 text-amber-700 dark:text-amber-400",
    bar: "bg-amber-500",
  },
  FollowUp: {
    dot: "bg-yellow-500",
    badge:
      "border-yellow-500/20 bg-yellow-500/10 text-yellow-700 dark:text-yellow-400",
    bar: "bg-yellow-500",
  },
  ProposalSent: {
    dot: "bg-orange-500",
    badge:
      "border-orange-500/20 bg-orange-500/10 text-orange-700 dark:text-orange-400",
    bar: "bg-orange-500",
  },
  ContractSent: {
    dot: "bg-rose-500",
    badge:
      "border-rose-500/20 bg-rose-500/10 text-rose-700 dark:text-rose-400",
    bar: "bg-rose-500",
  },
  Negotiation: {
    dot: "bg-orange-500",
    badge:
      "border-orange-500/20 bg-orange-500/10 text-orange-700 dark:text-orange-400",
    bar: "bg-orange-500",
  },
  Booked: {
    dot: "bg-emerald-500",
    badge:
      "border-emerald-500/20 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400",
    bar: "bg-emerald-500",
  },
  Closed: {
    dot: "bg-violet-500",
    badge:
      "border-violet-500/20 bg-violet-500/10 text-violet-700 dark:text-violet-400",
    bar: "bg-violet-500",
  },
  Lost: {
    dot: "bg-zinc-400",
    badge:
      "border-zinc-400/25 bg-zinc-400/10 text-zinc-600 dark:text-zinc-400",
    bar: "bg-zinc-400",
  },
  Nurture: {
    dot: "bg-slate-400",
    badge:
      "border-slate-400/25 bg-slate-400/10 text-slate-600 dark:text-slate-400",
    bar: "bg-slate-400",
  },
};

export function getStageStyle(stage: string) {
  return stageStyles[stage] ?? stageStyles.New;
}
