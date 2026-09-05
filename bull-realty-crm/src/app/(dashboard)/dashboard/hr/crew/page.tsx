"use client";

import * as React from "react";
import { CalendarCheck2 } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { formatMoney } from "@/lib/crm-api";
import { hrApi, hrEmployeesApi, type HrEmployee } from "@/lib/hrms-api";

export default function HrCrewPage() {
  const [rows, setRows] = React.useState<
    { id: number; employeeName: string; eventName: string; eventDate: string; roleOnSite: string; overtimeHours: number; incentiveAmount: number }[]
  >([]);
  const [people, setPeople] = React.useState<HrEmployee[]>([]);
  const [employeeId, setEmployeeId] = React.useState("");
  const [eventName, setEventName] = React.useState("");
  const [eventDate, setEventDate] = React.useState(() => new Date().toISOString().slice(0, 10));
  const [role, setRole] = React.useState("Crew");
  const [ot, setOt] = React.useState("0");
  const [incentive, setIncentive] = React.useState("0");

  const load = React.useCallback(() => {
    hrApi.deployments().then(setRows).catch(() => setRows([]));
  }, []);

  React.useEffect(() => {
    load();
    hrEmployeesApi.query({ page: 1, pageSize: 200, sort: [], filter: null }).then((r) => {
      setPeople(r.items);
      if (r.items[0]) setEmployeeId(String(r.items[0].id));
    });
  }, [load]);

  async function save() {
    try {
      await hrApi.deploy({
        employeeId: Number(employeeId),
        eventName,
        eventDate,
        roleOnSite: role,
        attendanceStatus: "Present",
        overtimeHours: Number(ot) || 0,
        incentiveAmount: Number(incentive) || 0,
      });
      toast.success("Crew assigned. Approved OT/incentive flows into that month's payroll.");
      setEventName("");
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not assign.");
    }
  }

  return (
    <PagePanel
      icon={CalendarCheck2}
      title="Event crew"
      hint="Bull Events: employee → event → venue → shift. OT and incentive hit payroll."
    >
      <div className="mb-4 flex flex-wrap gap-2">
        <select className="h-9 rounded-md border bg-background px-2 text-sm" value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
          {people.map((p) => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>
        <Input className="h-9 w-44" placeholder="Event" value={eventName} onChange={(e) => setEventName(e.target.value)} />
        <Input type="date" className="h-9 w-40" value={eventDate} onChange={(e) => setEventDate(e.target.value)} />
        <Input className="h-9 w-28" placeholder="Role" value={role} onChange={(e) => setRole(e.target.value)} />
        <Input className="h-9 w-20" placeholder="OT h" value={ot} onChange={(e) => setOt(e.target.value)} />
        <Input className="h-9 w-24" placeholder="Incentive" value={incentive} onChange={(e) => setIncentive(e.target.value)} />
        <Button className="h-9" onClick={() => void save()}>Assign</Button>
      </div>
      <ul className="space-y-1 text-[13px]">
        {rows.map((r) => (
          <li key={r.id} className="rounded border px-3 py-2">
            {String(r.eventDate).slice(0, 10)} · {r.eventName} · {r.employeeName} · {r.roleOnSite}
            {r.overtimeHours > 0 ? ` · OT ${r.overtimeHours}h` : ""}
            {r.incentiveAmount > 0 ? ` · ${formatMoney(r.incentiveAmount)}` : ""}
          </li>
        ))}
      </ul>
    </PagePanel>
  );
}
