"use client";

import * as React from "react";
import { CalendarDays } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { humanise } from "@/lib/crm-api";
import { hrApi, hrEmployeesApi, type HrEmployee } from "@/lib/hrms-api";

export default function HrLeavePage() {
  const [rows, setRows] = React.useState<
    { id: number; employeeName: string; leaveType: string; fromDate: string; toDate: string; status: string }[]
  >([]);
  const [types, setTypes] = React.useState<{ id: number; name: string }[]>([]);
  const [people, setPeople] = React.useState<HrEmployee[]>([]);
  const [employeeId, setEmployeeId] = React.useState("");
  const [typeId, setTypeId] = React.useState("");
  const [from, setFrom] = React.useState(() => new Date().toISOString().slice(0, 10));
  const [to, setTo] = React.useState(() => new Date().toISOString().slice(0, 10));
  const [reason, setReason] = React.useState("");

  const load = React.useCallback(() => {
    hrApi.leaveRequests().then(setRows).catch(() => setRows([]));
  }, []);

  React.useEffect(() => {
    load();
    hrApi.leaveTypes().then(setTypes).catch(() => setTypes([]));
    hrEmployeesApi.query({ page: 1, pageSize: 200, sort: [], filter: null }).then((r) => {
      setPeople(r.items);
      if (r.items[0]) setEmployeeId(String(r.items[0].id));
    });
  }, [load]);

  React.useEffect(() => {
    if (types[0] && !typeId) setTypeId(String(types[0].id));
  }, [types, typeId]);

  async function apply() {
    try {
      await hrApi.applyLeave({
        employeeId: Number(employeeId),
        leaveTypeId: Number(typeId),
        fromDate: from,
        toDate: to,
        halfDay: false,
        reason,
      });
      toast.success("Leave submitted — manager first, then HR if the type needs two levels.");
      setReason("");
      load();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not apply.");
    }
  }

  return (
    <PagePanel
      icon={CalendarDays}
      title="Leave"
      hint="Employee applies → Manager → HR (when the leave type needs two levels) → balance and attendance update."
    >
      <div className="mb-4 grid gap-2 sm:grid-cols-2 lg:grid-cols-6">
        <select className="h-9 rounded-md border bg-background px-2 text-sm" value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
          {people.map((p) => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>
        <select className="h-9 rounded-md border bg-background px-2 text-sm" value={typeId} onChange={(e) => setTypeId(e.target.value)}>
          {types.map((t) => (
            <option key={t.id} value={t.id}>{t.name}</option>
          ))}
        </select>
        <Input type="date" className="h-9" value={from} onChange={(e) => setFrom(e.target.value)} />
        <Input type="date" className="h-9" value={to} onChange={(e) => setTo(e.target.value)} />
        <Input className="h-9" placeholder="Reason" value={reason} onChange={(e) => setReason(e.target.value)} />
        <Button className="h-9" onClick={() => void apply()}>Apply</Button>
      </div>
      <ul className="space-y-2">
        {rows.map((r) => (
          <li key={r.id} className="flex flex-wrap items-center justify-between gap-2 rounded border p-2.5 text-[13px]">
            <span>
              {r.employeeName} · {r.leaveType} · {String(r.fromDate).slice(0, 10)} → {String(r.toDate).slice(0, 10)} · {humanise(r.status)}
            </span>
            {r.status.includes("Pending") ? (
              <span className="flex gap-1">
                <Button size="sm" variant="outline" className="h-7" onClick={() => void hrApi.decideLeave(r.id, true).then(load)}>Approve</Button>
                <Button size="sm" variant="outline" className="h-7" onClick={() => void hrApi.decideLeave(r.id, false).then(load)}>Reject</Button>
              </span>
            ) : null}
          </li>
        ))}
      </ul>
    </PagePanel>
  );
}
