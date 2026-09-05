/**
 * Chart palette.
 *
 * Literal hex, not Tailwind theme vars: the v4 stylesheet prunes theme vars that
 * no static class references, and these are consumed from inline styles and SVG
 * fills. The first five track the SLDS `--chart-*` tokens in `globals.css`; the
 * rest extend the ramp so a group-by with high cardinality still reads cleanly.
 */

export const CHART_COLORS = [
  "#0176d3", // SLDS brand blue
  "#04844b", // green
  "#fe9339", // orange
  "#9050e9", // violet
  "#0b827c", // teal
  "#e5567f", // rose
  "#3296ed", // sky
  "#f5c518", // amber
  "#7f8de1", // periwinkle
  "#c23934", // red
  "#5867e8", // indigo
  "#47974a", // moss
] as const;

/** Dark-theme counterparts — lifted in value so they hold up on a dark ground. */
export const CHART_COLORS_DARK = [
  "#1b96ff",
  "#45c65a",
  "#fe9339",
  "#b780ff",
  "#06a59a",
  "#ff7ea0",
  "#78b9ff",
  "#ffd54a",
  "#a3aef0",
  "#ff6259",
  "#8b97f5",
  "#6bbf6e",
] as const;

export function colorAt(index: number) {
  return CHART_COLORS[index % CHART_COLORS.length];
}

/**
 * Stable colour for a series name — the same category keeps its colour when a
 * filter reorders or removes rows, which a positional index alone would not do.
 */
export function colorForSeries(name: string, order: readonly string[]) {
  const index = order.indexOf(name);
  return colorAt(index === -1 ? hashIndex(name) : index);
}

function hashIndex(value: string) {
  let hash = 0;
  for (let i = 0; i < value.length; i += 1) {
    hash = (hash * 31 + value.charCodeAt(i)) | 0;
  }
  return Math.abs(hash);
}

/** Sequential ramp for heatmap cells — 0 reads as empty, 1 as the hottest cell. */
export function heatColor(intensity: number) {
  const clamped = Math.max(0, Math.min(1, intensity));
  // Perceptually even-ish blue ramp, expressed as an alpha over the brand hue
  // so it works against both the light and dark card grounds.
  return `color-mix(in oklab, var(--chart-1) ${Math.round(clamped * 88 + 6)}%, transparent)`;
}

/** Semantic colours for delta / trend indicators. */
export const TREND_UP = "#04844b";
export const TREND_DOWN = "#c23934";
