"use client";

import * as React from "react";
import { LifeBuoy } from "lucide-react";
import { toast } from "sonner";

import { HrRow, PeopleSelect, useHrPeople } from "@/components/hr/hr-kit";
import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { humanise } from "@/lib/crm-api";
import { hrx } from "@/lib/hrms-api";

const STATUSES = ["Open", "InProgress", "Waiting", "Resolved", "Closed"];

export default function HrHelpdeskPage() {
  const people = useHrPeople();
  const [employeeId, setEmployeeId] = React.useState("");
  const [subject, setSubject] = React.useState("");
  const [category, setCategory] = React.useState("General");
  const [priority, setPriority] = React.useState("Medium");
  const [body, setBody] = React.useState("");
  const [rows, setRows] = React.useState<Awaited<ReturnType<typeof hrx.tickets>>>([]);

  const load = React.useCallback(() => {
    hrx.tickets().then(setRows).catch(() => setRows([]));
  }, []);
  React.useEffect(() => {
    load();
  }, [load]);
  React.useEffect(() => {
    if (people[0] && !employeeId) setEmployeeId(String(people[0].id));
  }, [people, employeeId]);

  return (
    <PagePanel
      icon={LifeBuoy}
      title="HR helpdesk"
      hint="Internal employee tickets (Horilla helpdesk) — statutory, letters, devices, POSH. Not customer-care tickets."
    >
      <div className="mb-4 grid gap-2 sm:grid-cols-2 lg:grid-cols-6">
        <PeopleSelect value={employeeId} onChange={setEmployeeId} people={people} />
        <Input className="h-9" placeholder="Subject" value={subject} onChange={(e) => setSubject(e.target.value)} />
        <select className="h-9 rounded-md border bg-background px-2 text-sm" value={category} onChange={(e) => setCategory(e.target.value)}>
          {["General", "Payroll", "Statutory", "Letter", "Asset", "POSH", "IT"].map((c) => (
            <option key={c}>{c}</option>
          ))}
        </select>
        <select className="h-9 rounded-md border bg-background px-2 text-sm" value={priority} onChange={(e) => setPriority(e.target.value)}>
          {["Low", "Medium", "High", "Urgent"].map((c) => (
            <option key={c}>{c}</option>
          ))}
        </select>
        <Input className="h-9 lg:col-span-1" placeholder="Details" value={body} onChange={(e) => setBody(e.target.value)} />
        <Button
          className="h-9"
          onClick={() =>
            void hrx
              .createTicket({ employeeId: Number(employeeId), subject, category, priority, body })
              .then(() => {
                setSubject("");
                setBody("");
                toast.success("Ticket opened");
                load();
              })
          }
        >
          Open ticket
        </Button>
      </div>
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
        {STATUSES.map((s) => (
          <div key={s} className="rounded-xl border bg-muted/20 p-2">
            <p className="mb-2 px-1 text-[11px] font-semibold uppercase text-muted-foreground">{humanise(s)}</p>
            <ul className="space-y-2">
              {rows
                .filter((r) => r.status === s)
                .map((r) => (
                  <HrRow key={r.id}>
                    <span>
                      <span className="font-medium">{r.subject}</span>
                      <span className="block text-[11px] text-muted-foreground">
                        {r.employeeName} · {r.priority} · {r.category}
                      </span>
                    </span>
                    <span className="flex flex-wrap gap-1">
                      {STATUSES.filter((x) => x !== s).slice(0, 2).map((x) => (
                        <Button key={x} size="sm" variant="outline" className="h-6 text-[10px]" onClick={() => void hrx.ticketStatus(r.id, x).then(load)}>
                          {humanise(x)}
                        </Button>
                      ))}
                    </span>
                  </HrRow>
                ))}
            </ul>
          </div>
        ))}
      </div>
    </PagePanel>
  );
}
