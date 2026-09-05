"use client";

import * as React from "react";
import { FileSignature } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { humanise } from "@/lib/crm-api";
import { hrApi, hrEmployeesApi, type HrEmployee } from "@/lib/hrms-api";

export default function HrExitPage() {
  const [rows, setRows] = React.useState<
    { id: number; employeeName: string; lastWorkingDay: string; status: string; employeeId: number }[]
  >([]);
  const [people, setPeople] = React.useState<HrEmployee[]>([]);
  const [employeeId, setEmployeeId] = React.useState("");
  const [lwd, setLwd] = React.useState(() => new Date().toISOString().slice(0, 10));
  const [reason, setReason] = React.useState("");

  const load = React.useCallback(() => {
    hrApi.resignations().then(setRows).catch(() => setRows([]));
  }, []);

  React.useEffect(() => {
    load();
    hrEmployeesApi.query({ page: 1, pageSize: 200, sort: [], filter: null }).then((r) => {
      setPeople(r.items);
      if (r.items[0]) setEmployeeId(String(r.items[0].id));
    });
  }, [load]);

  return (
    <PagePanel
      icon={FileSignature}
      title="Exit & F&F"
      hint="Resignation → manager/HR → notice → full & final → relieved."
    >
      <div className="mb-4 flex flex-wrap gap-2">
        <select className="h-9 rounded-md border bg-background px-2 text-sm" value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
          {people.map((p) => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>
        <Input type="date" className="h-9 w-40" value={lwd} onChange={(e) => setLwd(e.target.value)} />
        <Input className="h-9 w-52" placeholder="Reason" value={reason} onChange={(e) => setReason(e.target.value)} />
        <Button
          className="h-9"
          onClick={() =>
            void hrApi
              .resign({
                employeeId: Number(employeeId),
                resignationDate: new Date().toISOString().slice(0, 10),
                noticeDays: 30,
                lastWorkingDay: lwd,
                reason,
              })
              .then(() => {
                toast.success("Resignation submitted");
                load();
              })
              .catch((e) => toast.error(e instanceof ApiError ? e.message : "Failed"))
          }
        >
          Submit resignation
        </Button>
      </div>
      <ul className="space-y-2">
        {rows.map((r) => (
          <li key={r.id} className="flex flex-wrap items-center justify-between gap-2 rounded border p-2.5 text-[13px]">
            <span>
              {r.employeeName} · LWD {String(r.lastWorkingDay).slice(0, 10)} · {humanise(r.status)}
            </span>
            <span className="flex gap-1">
              {r.status.includes("Pending") ? (
                <>
                  <Button size="sm" variant="outline" className="h-7" onClick={() => void hrApi.decideResignation(r.id, true).then(load)}>Approve</Button>
                  <Button size="sm" variant="outline" className="h-7" onClick={() => void hrApi.decideResignation(r.id, false).then(load)}>Reject</Button>
                </>
              ) : null}
              <Button
                size="sm"
                variant="outline"
                className="h-7"
                onClick={() =>
                  void hrApi
                    .saveFnf({
                      employeeId: r.employeeId,
                      resignationId: r.id,
                      salaryDue: 0,
                      lopAmount: 0,
                      leaveEncashment: 0,
                      deductions: 0,
                      assetsRecovered: 0,
                    })
                    .then(() => {
                      toast.success("F&F closed — employee marked relieved.");
                      load();
                    })
                }
              >
                Close F&F
              </Button>
              <Button
                size="sm"
                variant="outline"
                className="h-7"
                onClick={() => void hrApi.generateLetter(r.employeeId, "Relieving").then(() => toast.success("Relieving letter generated"))}
              >
                Relieving letter
              </Button>
            </span>
          </li>
        ))}
      </ul>
    </PagePanel>
  );
}
