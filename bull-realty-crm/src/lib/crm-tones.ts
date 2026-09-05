import type { Tone } from "@/components/crm/metrics";

/**
 * One mapping from domain values to the semantic palette.
 *
 * Kept out of the components so a stage reads the same colour on the list, the
 * Kanban board, the record page and the dashboard — colour is part of the
 * vocabulary here, not per-screen decoration.
 */

export function leadStageTone(stage: string): Tone {
  switch (stage) {
    case "New":
      return "info";
    case "Contacted":
      return "primary";
    case "Qualified":
      return "info";
    case "ObmVisit":
      return "violet";
    case "SiteVisit":
      return "violet";
    case "ProposalSent":
    case "Negotiation":
      return "warning";
    case "ContractSent":
      return "warning";
    case "Booked":
      return "success";
    case "Nurture":
      return "neutral";
    case "Closed":
      return "neutral";
    case "Lost":
      return "danger";
    default:
      return "neutral";
  }
}

export function opportunityStageTone(stage: string): Tone {
  switch (stage) {
    case "Qualification":
      return "info";
    case "NeedsAnalysis":
      return "primary";
    case "Proposal":
      return "violet";
    case "Negotiation":
      return "warning";
    case "ClosedWon":
      return "success";
    case "ClosedLost":
      return "danger";
    default:
      return "neutral";
  }
}

export function priorityTone(priority: string): Tone {
  switch (priority) {
    case "Hot":
      return "danger";
    case "High":
      return "warning";
    case "Medium":
      return "primary";
    default:
      return "neutral";
  }
}

export function slaTone(state: string): Tone {
  switch (state) {
    case "Breached":
      return "danger";
    case "AtRisk":
      return "warning";
    case "OnTrack":
      return "info";
    case "Met":
      return "success";
    default:
      return "neutral";
  }
}

export function visitStatusTone(status: string): Tone {
  switch (status) {
    case "Completed":
      return "success";
    case "Confirmed":
      return "primary";
    case "Scheduled":
      return "info";
    case "Rescheduled":
      return "warning";
    case "NoShow":
      return "danger";
    case "Cancelled":
      return "neutral";
    default:
      return "neutral";
  }
}

export function followUpStatusTone(status: string, isOverdue: boolean): Tone {
  if (status === "Completed") return "success";
  if (status === "Cancelled") return "neutral";
  if (isOverdue) return "danger";
  return status === "InProgress" ? "warning" : "info";
}

export function quotationStatusTone(status: string): Tone {
  switch (status) {
    case "Accepted":
      return "success";
    case "Rejected":
      return "danger";
    case "Negotiation":
      return "warning";
    case "Sent":
    case "UnderReview":
      return "primary";
    case "Expired":
      return "neutral";
    default:
      return "info";
  }
}

export function unitStatusTone(status: string): Tone {
  switch (status) {
    case "Available":
      return "success";
    case "Held":
      return "warning";
    case "Blocked":
      return "violet";
    case "Booked":
      return "primary";
    case "Sold":
      return "danger";
    case "Blackout":
      return "neutral";
    default:
      return "neutral";
  }
}

export function callOutcomeTone(outcome: string): Tone {
  switch (outcome) {
    case "Connected":
      return "success";
    case "NoAnswer":
    case "Busy":
      return "warning";
    case "WrongNumber":
      return "danger";
    default:
      return "neutral";
  }
}

export function sentimentTone(label: string | null): Tone {
  switch (label) {
    case "Positive":
      return "success";
    case "Negative":
      return "danger";
    default:
      return "neutral";
  }
}

export function dispositionTone(disposition: string | null): Tone {
  switch (disposition) {
    case "Interested":
    case "SiteVisitScheduled":
    case "Converted":
      return "success";
    case "CallBackLater":
      return "info";
    case "NotInterested":
    case "BudgetMismatch":
    case "DoNotCall":
      return "danger";
    default:
      return "neutral";
  }
}

export function interestTone(level: string | null): Tone {
  switch (level) {
    case "High":
      return "success";
    case "Medium":
      return "warning";
    case "Low":
      return "danger";
    default:
      return "neutral";
  }
}

export function winBandTone(band: string | null): Tone {
  switch (band) {
    case "Strong":
      return "success";
    case "Likely":
      return "primary";
    case "At risk":
      return "warning";
    case "Long shot":
      return "danger";
    default:
      return "neutral";
  }
}
