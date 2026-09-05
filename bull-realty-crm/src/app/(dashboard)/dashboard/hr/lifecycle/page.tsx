"use client";

import * as React from "react";
import { MapPinned } from "lucide-react";
import { toast } from "sonner";

import { HrRow, PeopleSelect, useHrPeople } from "@/components/hr/hr-kit";
import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { humanise } from "@/lib/crm-api";
import { hrx } from "@/lib/hrms-api";

const KINDS = ["Confirmation", "Promotion", "Transfer", "Increment", "Demotion"];

export default function HrLifecyclePage() {
  const people = useHrPeople();
  const [employeeId, setEmployeeId] = React.useState("");
  const [kind, setKind] = React.useState("Promotion");
  const [toValue, setToValue] = React.useState("");
  const [rows, setRows] = React.useState<Awaited<ReturnType<typeof hrx.lifecycle>>>([]);

  const load = React.useCallback(() => {
    hrx.lifecycle().then(setRows).catch(() => setRows([]));
  }, []);
  React.useEffect(() => {
    load();
  }, [load]);
  React.useEffect(() => {
    if (people[0] && !employeeId) setEmployeeId(String(people[0].id));
  }, [people, employeeId]);

  return (
    <PagePanel
      icon={MapPinned}
      title="Employee lifecycle"
      hint="Confirmation, promotion, transfer, increment — Frappe employee lifecycle events applied onto the master."
    >
      <div className="mb-4 flex flex-wrap gap-2">
        <PeopleSelect value={employeeId} onChange={setEmployeeId} people={people} />
        <select className="h-9 rounded-md border bg-background px-2 text-sm" value={kind} onChange={(e) => setKind(e.target.value)}>
          {KINDS.map((k) => (
            <option key={k}>{k}</option>
          ))}
        </select>
        <Input className="h-9 w-48" placeholder="To (role / location / CTC)" value={toValue} onChange={(e) => setToValue(e.target.value)} />
        <Button
          size="sm"
          className="h-9"
          onClick={() =>
            void hrx
              .createLifecycle({
                employeeId: Number(employeeId),
                kind,
                effectiveOn: new Date().toISOString().slice(0, 10),
                toValue,
              })
              .then(() => {
                setToValue("");
                toast.success("Event raised");
                load();
              })
          }
        >
          Raise
        </Button>
      </div>
      <ul className="space-y-2">
        {rows.map((r) => (
          <HrRow key={r.id}>
            <span>
              {r.employeeName} · {humanise(r.kind)} → {r.toValue ?? "—"} · {String(r.effectiveOn).slice(0, 10)} · {humanise(r.status)}
            </span>
            {r.status !== "Approved" ? (
              <Button size="sm" className="h-7" onClick={() => void hrx.applyLifecycle(r.id).then(load)}>
                Apply to master
              </Button>
            ) : null}
          </HrRow>
        ))}
      </ul>
    </PagePanel>
  );
}
