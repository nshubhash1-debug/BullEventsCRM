"use client";

import * as React from "react";
import Papa from "papaparse";
import { CircleAlert, FileUp, Loader2, Upload } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Pill, TONE_TEXT } from "@/components/crm/metrics";
import { RuleCard } from "@/components/admin/rule-list";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { ApiError, getBranches, type Branch } from "@/lib/api";
import { dataAdminApi, type ImportField, type ImportResult } from "@/lib/data-admin-api";
import { cn } from "@/lib/utils";

/** Ignore this column entirely. Empty string is the Select's own "no value". */
const SKIP = "__skip";

/**
 * CSV import.
 *
 * Three steps, in the order the work actually happens: read the file, say which
 * column is which, then import. The mapping step is the one that cannot be
 * skipped — every source exports different headers, and guessing wrong silently
 * puts phone numbers in the notes field.
 *
 * Headers are matched to fields automatically where the names line up, because
 * most files do line up and making somebody map eleven columns by hand when ten
 * were obvious is how an import tool earns a reputation for being slow.
 */
export default function ImportPage() {
  const [fields, setFields] = React.useState<ImportField[]>([]);
  const [branches, setBranches] = React.useState<Branch[]>([]);
  const [branchId, setBranchId] = React.useState("");

  const [fileName, setFileName] = React.useState<string | null>(null);
  const [headers, setHeaders] = React.useState<string[]>([]);
  const [rows, setRows] = React.useState<Record<string, string>[]>([]);
  const [mapping, setMapping] = React.useState<Record<string, string>>({});
  const [parseErrors, setParseErrors] = React.useState<string[]>([]);

  const [skipDuplicates, setSkipDuplicates] = React.useState(true);
  const [applyRules, setApplyRules] = React.useState(true);
  const [running, setRunning] = React.useState(false);
  const [result, setResult] = React.useState<ImportResult | null>(null);

  React.useEffect(() => {
    dataAdminApi.importFields().then(setFields).catch(() => setFields([]));

    getBranches()
      .then((list) => {
        setBranches(list);
        if (list.length > 0) setBranchId(String(list[0].id));
      })
      .catch(() => setBranches([]));
  }, []);

  function onFile(file: File) {
    setResult(null);
    setParseErrors([]);

    Papa.parse<Record<string, string>>(file, {
      header: true,
      skipEmptyLines: "greedy",
      // Trimmed here rather than per cell later: a header of " Phone " is the
      // single commonest reason an otherwise obvious column fails to map.
      transformHeader: (h) => h.trim(),
      complete: (parsed) => {
        const cols = (parsed.meta.fields ?? []).filter(Boolean);

        setFileName(file.name);
        setHeaders(cols);
        setRows(parsed.data);
        setParseErrors(parsed.errors.slice(0, 5).map((e) => `Row ${e.row}: ${e.message}`));
        setMapping(guess(cols, fields));

        toast.success(`${parsed.data.length} rows read`, { description: file.name });
      },
      error: (error) => toast.error("Could not read the file", { description: error.message }),
    });
  }

  const mapped = Object.entries(mapping).filter(([, field]) => field && field !== SKIP);
  const requiredMissing = fields
    .filter((f) => f.required)
    .filter((f) => !mapped.some(([, field]) => field === f.name));

  async function run() {
    if (!branchId) {
      toast.error("Choose a branch to import into.");
      return;
    }

    setRunning(true);
    try {
      const payload = rows.map((row) => {
        const values: Record<string, string | null> = {};
        for (const [header, field] of mapped) values[field] = row[header] ?? null;
        return { values };
      });

      const outcome = await dataAdminApi.runImport({
        object: "Lead",
        branchId: Number(branchId),
        skipDuplicates,
        applyAssignmentRules: applyRules,
        rows: payload,
      });

      setResult(outcome);

      toast.success(`${outcome.created} leads created`, {
        description:
          outcome.skipped + outcome.failed > 0
            ? `${outcome.skipped} skipped as duplicates, ${outcome.failed} could not be imported.`
            : "Every row imported.",
        duration: 8000,
      });
    } catch (error) {
      toast.error("The import failed", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setRunning(false);
    }
  }

  return (
    <PagePanel
      icon={FileUp}
      title="Import"
      hint="Bring leads in from a CSV. Rows are imported one at a time, so a bad row does not stop the rest."
    >
      <div className="flex flex-col gap-4">
        {/* ---------------- 1. the file ---------------- */}
        <RuleCard title="1 · The file" hint={fileName ?? "No file chosen"}>
          <div className="px-4 py-4">
            <label
              className={cn(
                "flex cursor-pointer flex-col items-center justify-center gap-2 rounded-lg border border-dashed px-4 py-8 text-center transition-colors",
                "hover:border-primary/40 hover:bg-accent/40"
              )}
            >
              <Upload className="size-5 text-muted-foreground" />
              <span className="text-[13px] font-medium">
                {fileName ? "Choose a different file" : "Choose a CSV file"}
              </span>
              <span className="text-[12px] text-muted-foreground">
                The first row is read as the column headings.
              </span>
              <input
                type="file"
                accept=".csv,text/csv"
                className="hidden"
                onChange={(event) => {
                  const file = event.target.files?.[0];
                  if (file) onFile(file);
                  event.target.value = "";
                }}
              />
            </label>

            {parseErrors.length > 0 ? (
              <div className="mt-3 rounded-md border border-amber-500/25 bg-amber-500/10 px-3 py-2">
                <p className={cn("text-[12.5px] font-medium", TONE_TEXT.warning)}>
                  The file has some malformed rows
                </p>
                <ul className="mt-1 space-y-0.5 text-[11.5px] text-muted-foreground">
                  {parseErrors.map((problem) => (
                    <li key={problem}>{problem}</li>
                  ))}
                </ul>
              </div>
            ) : null}
          </div>
        </RuleCard>

        {/* ---------------- 2. the mapping ---------------- */}
        {headers.length > 0 ? (
          <RuleCard
            title="2 · Which column is which"
            count={mapped.length}
            hint={`of ${headers.length} columns mapped`}
          >
            <div className="overflow-x-auto">
              <table className="w-full min-w-[620px] text-[12.5px]">
                <thead>
                  <tr className="border-b bg-muted/50 text-[10.5px] tracking-wide text-muted-foreground uppercase">
                    <th className="px-4 py-2 text-left font-medium">Column in the file</th>
                    <th className="px-4 py-2 text-left font-medium">First value</th>
                    <th className="px-4 py-2 text-left font-medium">Import as</th>
                  </tr>
                </thead>
                <tbody>
                  {headers.map((header) => (
                    <tr key={header} className="border-b last:border-0">
                      <td className="px-4 py-2 font-medium">{header}</td>
                      <td className="max-w-[220px] truncate px-4 py-2 text-muted-foreground">
                        {rows[0]?.[header] || "—"}
                      </td>
                      <td className="px-4 py-2">
                        <Select
                          value={mapping[header] ?? SKIP}
                          onValueChange={(field) =>
                            setMapping((current) => ({ ...current, [header]: field }))
                          }
                        >
                          <SelectTrigger size="sm" className="w-[220px] text-[12px]">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value={SKIP}>Do not import</SelectItem>
                            {fields.map((field) => (
                              <SelectItem key={field.name} value={field.name}>
                                {field.label}
                                {field.required ? " *" : ""}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {requiredMissing.length > 0 ? (
              <p
                className={cn(
                  "flex items-center gap-2 border-t px-4 py-2.5 text-[12.5px]",
                  TONE_TEXT.danger
                )}
              >
                <CircleAlert className="size-4 shrink-0" />
                Still needs a column for {requiredMissing.map((f) => f.label).join(" and ")}.
              </p>
            ) : null}
          </RuleCard>
        ) : null}

        {/* ---------------- 3. the run ---------------- */}
        {headers.length > 0 ? (
          <RuleCard title="3 · Import">
            <div className="grid gap-3 px-4 py-4">
              <div className="grid max-w-xs gap-1.5">
                <Label>Into which branch</Label>
                <Select value={branchId} onValueChange={setBranchId}>
                  <SelectTrigger>
                    <SelectValue placeholder="Choose a branch" />
                  </SelectTrigger>
                  <SelectContent>
                    {branches.map((branch) => (
                      <SelectItem key={branch.id} value={String(branch.id)}>
                        {branch.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <label className="flex items-center gap-2.5 text-[12.5px]">
                <Switch checked={skipDuplicates} onCheckedChange={setSkipDuplicates} />
                <span>
                  Skip rows that match an existing lead
                  <span className="block text-[11.5px] text-muted-foreground">
                    Uses the duplicate rules. Off means a repeat import creates everything twice.
                  </span>
                </span>
              </label>

              <label className="flex items-center gap-2.5 text-[12.5px]">
                <Switch checked={applyRules} onCheckedChange={setApplyRules} />
                <span>
                  Route them with the assignment rules
                  <span className="block text-[11.5px] text-muted-foreground">
                    Off leaves every imported lead unassigned for the desk to pick up.
                  </span>
                </span>
              </label>

              <div>
                <Button
                  className="gap-1.5"
                  disabled={running || requiredMissing.length > 0 || rows.length === 0}
                  onClick={run}
                >
                  {running ? (
                    <Loader2 className="size-4 animate-spin" />
                  ) : (
                    <FileUp className="size-4" />
                  )}
                  Import {rows.length} rows
                </Button>
              </div>
            </div>
          </RuleCard>
        ) : null}

        {/* ---------------- what happened ---------------- */}
        {result ? (
          <RuleCard title="Result">
            <div className="grid gap-px bg-border sm:grid-cols-4">
              {[
                { label: "Read", value: result.received, tone: "neutral" as const },
                { label: "Created", value: result.created, tone: "success" as const },
                { label: "Skipped", value: result.skipped, tone: "warning" as const },
                { label: "Failed", value: result.failed, tone: "danger" as const },
              ].map((cell) => (
                <div key={cell.label} className="bg-card px-4 py-3">
                  <p className={cn("text-xl font-semibold tabular-nums", TONE_TEXT[cell.tone])}>
                    {cell.value}
                  </p>
                  <p className="text-[10.5px] tracking-wide text-muted-foreground uppercase">
                    {cell.label}
                  </p>
                </div>
              ))}
            </div>

            {result.problems.length > 0 ? (
              <div className="border-t px-4 py-3">
                <p className="mb-1.5 text-[12px] font-medium">
                  Rows that could not be imported
                  <span className="ml-1.5 font-normal text-muted-foreground">
                    (row numbers match the file)
                  </span>
                </p>
                <ul className="space-y-0.5">
                  {result.problems.map((problem) => (
                    <li key={problem.row} className="text-[12px]">
                      <Pill tone="danger">Row {problem.row}</Pill>{" "}
                      <span className="text-muted-foreground">{problem.reason}</span>
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </RuleCard>
        ) : null}
      </div>
    </PagePanel>
  );
}

/**
 * Matches file headers to fields where the names obviously agree.
 *
 * Normalised on letters only, so "Mobile No.", "mobile_no" and "MobileNo" all
 * land on the same guess. Anything unrecognised is left unmapped rather than
 * guessed at — a wrong guess that looks confident is worse than no guess.
 */
function guess(headers: string[], fields: ImportField[]): Record<string, string> {
  const aliases: Record<string, string> = {
    name: "Name", fullname: "Name", leadname: "Name", customername: "Name",
    phone: "Phone", mobile: "Phone", mobileno: "Phone", contact: "Phone",
    contactnumber: "Phone", phoneno: "Phone",
    email: "Email", emailid: "Email", emailaddress: "Email",
    city: "City", location: "City",
    state: "State",
    source: "Source", leadsource: "Source",
    eventtype: "EventType", occasion: "EventType", event: "EventType",
    requirement: "EventType", functiontype: "EventType",
    eventdate: "EventDate", date: "EventDate", functiondate: "EventDate",
    weddingdate: "EventDate", dateofevent: "EventDate",
    guestcount: "GuestCount", guests: "GuestCount", noofguests: "GuestCount",
    pax: "GuestCount", headcount: "GuestCount",
    services: "ServicesNeeded", servicesneeded: "ServicesNeeded",
    budget: "BudgetMin", budgetmin: "BudgetMin", minbudget: "BudgetMin",
    budgetmax: "BudgetMax", maxbudget: "BudgetMax",
    notes: "Notes", remarks: "Notes", comment: "Notes", comments: "Notes",
  };

  const known = new Set(fields.map((f) => f.name.toLowerCase()));
  const result: Record<string, string> = {};

  for (const header of headers) {
    const key = header.toLowerCase().replace(/[^a-z]/g, "");
    const match = aliases[key] ?? (known.has(key) ? header : null);

    result[header] = match && fields.some((f) => f.name === match) ? match : SKIP;
  }

  return result;
}
