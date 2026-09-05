import type { MeasureFormat } from "@/lib/dashboard/analytics";

/**
 * Value formatting for chart axes, tooltips and tiles.
 *
 * Money is written the way this market reads it — lakh and crore, not million —
 * because a pipeline of ₹18.6 Cr is what the numbers in every other screen of
 * the CRM say, and an axis reading ₹186M would not match.
 */

const compact = new Intl.NumberFormat("en-IN", { maximumFractionDigits: 1 });
const plain = new Intl.NumberFormat("en-IN", { maximumFractionDigits: 0 });
const precise = new Intl.NumberFormat("en-IN", { maximumFractionDigits: 2 });

export function formatValue(
  value: number,
  format: MeasureFormat,
  style: "axis" | "full" = "full"
): string {
  if (!Number.isFinite(value)) return "—";

  switch (format) {
    case "currency":
      return formatCurrency(value, style);
    case "percent":
      return `${precise.format(value)}%`;
    default:
      return style === "axis" ? formatCompact(value) : plain.format(value);
  }
}

/** ₹1,24,50,000 reads as ₹1.24 Cr. */
export function formatCurrency(value: number, style: "axis" | "full" = "full") {
  const absolute = Math.abs(value);
  const sign = value < 0 ? "-" : "";

  if (absolute >= 10_000_000) {
    return `${sign}₹${compact.format(absolute / 10_000_000)} Cr`;
  }
  if (absolute >= 100_000) {
    return `${sign}₹${compact.format(absolute / 100_000)} L`;
  }
  if (absolute >= 1_000 && style === "axis") {
    return `${sign}₹${compact.format(absolute / 1_000)}K`;
  }

  return `${sign}₹${plain.format(absolute)}`;
}

export function formatCompact(value: number) {
  const absolute = Math.abs(value);
  const sign = value < 0 ? "-" : "";

  if (absolute >= 10_000_000) return `${sign}${compact.format(absolute / 10_000_000)}Cr`;
  if (absolute >= 100_000) return `${sign}${compact.format(absolute / 100_000)}L`;
  if (absolute >= 1_000) return `${sign}${compact.format(absolute / 1_000)}K`;

  return `${sign}${plain.format(absolute)}`;
}

export function formatPercent(value: number, digits = 1) {
  if (!Number.isFinite(value)) return "—";
  return `${value.toFixed(digits)}%`;
}

/** Share of a total, guarding the divide-by-zero an empty result would cause. */
export function share(value: number, total: number) {
  if (!total) return 0;
  return (value / total) * 100;
}
