"use client";

import * as React from "react";
import { TicketCheck } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { humanise } from "@/lib/crm-api";
import { hrApi, hrx } from "@/lib/hrms-api";

export default function HrInterviewsPage() {
  const [cands, setCands] = React.useState<{ id: number; name: string }[]>([]);
  const [rows, setRows] = React.useState<Awaited<ReturnType<typeof hrx.interviews>>>([]);
  const [candidateId, setCandidateId] = React.useState("");
  const [when, setWhen] = React.useState(() => new Date(Date.now() + 86400000).toISOString().slice(0, 16));
  const [panel, setPanel] = React.useState("Head HR");

  const load = React.useCallback(() => {
    hrx.interviews().then(setRows).catch(() => setRows([]));
    hrApi.candidates().then((list) => {
      setCands(list);
      setCandidateId((cur) => cur || (list[0] ? String(list[0].id) : ""));
    });
  }, []);
  React.useEffect(() => {
    load();
  }, [load]);

  return (
    <PagePanel
      icon={TicketCheck}
      title="Interviews"
      hint="Scorecards against the recruitment pipeline — Horilla recruitment interviews."
    >
      <div className="mb-4 flex flex-wrap gap-2">
        <select className="h-9 rounded-md border bg-background px-2 text-sm" value={candidateId} onChange={(e) => setCandidateId(e.target.value)}>
          {cands.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
        <Input className="h-9 w-52" type="datetime-local" value={when} onChange={(e) => setWhen(e.target.value)} />
        <Input className="h-9 w-40" placeholder="Panel" value={panel} onChange={(e) => setPanel(e.target.value)} />
        <Button
          size="sm"
          className="h-9"
          onClick={() =>
            void hrx
              .createInterview({
                candidateId: Number(candidateId),
                scheduledAt: new Date(when).toISOString(),
                panel,
                mode: "InPerson",
              })
              .then(() => {
                toast.success("Scheduled");
                load();
              })
          }
        >
          Schedule
        </Button>
      </div>
      <ul className="space-y-2">
        {rows.map((r) => (
          <li key={r.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 text-[13px]">
            <span>
              {r.candidateName} · {new Date(r.scheduledAt).toLocaleString()} · {r.panel ?? "—"} · {humanise(r.status)}
              {r.score != null ? ` · ${r.score}/10` : ""}
            </span>
            {r.status !== "Completed" ? (
              <Button size="sm" variant="outline" className="h-7" onClick={() => void hrx.scoreInterview(r.id, 8, "Hire").then(load)}>
                Score 8 · Hire
              </Button>
            ) : null}
          </li>
        ))}
      </ul>
    </PagePanel>
  );
}
