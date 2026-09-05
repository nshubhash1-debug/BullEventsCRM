import { apiRequest } from "@/lib/api";
import { readSession } from "@/lib/session";

/* ------------------------------------------------------------------ *
 * Import, export and mass transfer.
 *
 * Separate from the automation client because these are one-off operations an
 * administrator performs, not standing rules the system runs by itself — and
 * because the export path returns a file rather than JSON, which needs its own
 * handling.
 * ------------------------------------------------------------------ */

const base = "/api/admin/data";

export interface ImportField {
  name: string;
  label: string;
  required: boolean;
  note: string | null;
}

export interface ImportProblem {
  row: number;
  reason: string;
}

export interface ImportResult {
  received: number;
  created: number;
  skipped: number;
  failed: number;
  problems: ImportProblem[];
}

export interface MassTransferResult {
  matched: number;
  moved: number;
  wasPreview: boolean;
  sample: string[];
}

export const dataAdminApi = {
  importFields: () =>
    apiRequest<ImportField[]>(`${base}/import/fields`, { method: "GET", auth: true }),

  runImport: (body: {
    object: string;
    branchId: number;
    skipDuplicates: boolean;
    applyAssignmentRules: boolean;
    rows: Array<{ values: Record<string, string | null> }>;
  }) =>
    apiRequest<ImportResult>(`${base}/import`, {
      method: "POST",
      body: JSON.stringify(body),
      auth: true,
    }),

  massTransfer: (body: {
    object: string;
    fromUserId: number | null;
    toUserId: number;
    stage?: string | null;
    source?: string | null;
    city?: string | null;
    preview: boolean;
  }) =>
    apiRequest<MassTransferResult>(`${base}/mass-transfer`, {
      method: "POST",
      body: JSON.stringify(body),
      auth: true,
    }),
};

/**
 * Downloads the lead export.
 *
 * Fetched with the bearer token and turned into a blob rather than being opened
 * as a link: a plain anchor to the API carries no Authorization header, so the
 * endpoint would answer 401 and the browser would helpfully save the error page
 * as a .csv file.
 */
export async function downloadLeadExport(params: { stage?: string; ownerId?: number } = {}) {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") query.set(key, String(value));
  }

  const root = process.env.NEXT_PUBLIC_API_URL ?? "";
  const response = await fetch(`${root}${base}/export/leads?${query}`, {
    headers: { Authorization: `Bearer ${readSession()?.accessToken ?? ""}` },
  });

  if (!response.ok) {
    throw new Error(
      response.status === 403
        ? "You do not have permission to export."
        : "The export could not be produced."
    );
  }

  const blob = await response.blob();
  const url = URL.createObjectURL(blob);

  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `leads-${new Date().toISOString().slice(0, 10)}.csv`;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();

  // Released on the next tick: revoking it synchronously can cancel the
  // download in some browsers before it has started reading the blob.
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
