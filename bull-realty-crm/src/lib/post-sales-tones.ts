import type { Tone } from "@/components/crm/metrics";

/**
 * Colour for the post-sales vocabulary.
 *
 * Separate from `crm-tones` because these read in the opposite direction: on
 * the sales desk a warm colour means a deal is heating up, while here it means
 * money is late. Mixing the two mappings in one file would invite a stage and a
 * demand to be given the same colour for opposite reasons.
 */

export function bookingStatusTone(status: string): Tone {
  switch (status) {
    case "Booked":
      return "info";
    case "Allotted":
      return "primary";
    case "AgreementPending":
      return "warning";
    case "AgreementExecuted":
      return "violet";
    case "Registered":
      return "success";
    case "PossessionOffered":
      return "success";
    case "HandedOver":
      return "neutral";
    case "Cancelled":
      return "danger";
    case "Transferred":
      return "neutral";
    default:
      return "neutral";
  }
}

export function demandStatusTone(status: string): Tone {
  switch (status) {
    case "Raised":
      return "info";
    case "PartlyPaid":
      return "warning";
    case "Paid":
      return "success";
    case "Overdue":
      return "danger";
    case "Waived":
      return "neutral";
    case "Cancelled":
      return "neutral";
    default:
      return "neutral";
  }
}

export function receiptStatusTone(status: string): Tone {
  switch (status) {
    case "Cleared":
      return "success";
    case "Pending":
      return "warning";
    case "Bounced":
      return "danger";
    case "Cancelled":
      return "neutral";
    default:
      return "neutral";
  }
}

export function milestoneStatusTone(status: string): Tone {
  switch (status) {
    case "Pending":
      return "neutral";
    case "Demanded":
      return "info";
    case "PartlyPaid":
      return "warning";
    case "Paid":
      return "success";
    case "Waived":
      return "violet";
    default:
      return "neutral";
  }
}

export function documentStatusTone(status: string): Tone {
  switch (status) {
    case "Verified":
      return "success";
    case "Received":
      return "info";
    case "Pending":
      return "warning";
    case "Rejected":
      return "danger";
    case "NotApplicable":
      return "neutral";
    default:
      return "neutral";
  }
}

export function kycStatusTone(status: string): Tone {
  switch (status) {
    case "Verified":
      return "success";
    case "Submitted":
      return "info";
    case "Pending":
      return "warning";
    case "Rejected":
      return "danger";
    default:
      return "neutral";
  }
}

/**
 * Ageing reads as a ramp, not as a set of categories: the whole point of the
 * bucket is how far past due it is, so the colour has to carry that order.
 */
export function bucketTone(bucket: string): Tone {
  switch (bucket) {
    case "Current":
      return "success";
    case "1-30":
      return "info";
    case "31-60":
      return "warning";
    case "61-90":
      return "danger";
    case "90+":
      return "danger";
    default:
      return "neutral";
  }
}

/** Spaces a PascalCase status so it reads as English on screen. */
export function humanise(value: string | null | undefined) {
  if (!value) return "—";
  return value.replace(/([a-z])([A-Z])/g, "$1 $2");
}
