"use client";

import * as React from "react";
import { Settings } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { hrApi } from "@/lib/hrms-api";

export default function HrSetupPage() {
  const [depts, setDepts] = React.useState<{ id: number; name: string; location: string | null }[]>([]);
  const [shifts, setShifts] = React.useState<{ id: number; name: string; weeklyOff: string }[]>([]);
  const [types, setTypes] = React.useState<{ id: number; code: string; name: string; monthlyEntitlement: number }[]>([]);
  const [deptName, setDeptName] = React.useState("");
  const [shiftName, setShiftName] = React.useState("");
  const [leaveName, setLeaveName] = React.useState("");
  const [leaveCode, setLeaveCode] = React.useState("");

  const load = React.useCallback(() => {
    hrApi.departments().then(setDepts).catch(() => setDepts([]));
    hrApi.shifts().then(setShifts).catch(() => setShifts([]));
    hrApi.leaveTypes().then(setTypes).catch(() => setTypes([]));
  }, []);

  React.useEffect(() => {
    load();
  }, [load]);

  return (
    <PagePanel
      icon={Settings}
      title="HR setup"
      hint="Departments, shifts and leave rules — change these without a developer."
    >
      <div className="grid gap-6 lg:grid-cols-3">
        <section>
          <h2 className="mb-2 text-[12px] font-medium uppercase text-muted-foreground">Departments</h2>
          <div className="mb-2 flex gap-2">
            <Input className="h-9" value={deptName} onChange={(e) => setDeptName(e.target.value)} placeholder="Name" />
            <Button
              className="h-9"
              onClick={() =>
                void hrApi.createDepartment({ name: deptName, isActive: true }).then(() => {
                  setDeptName("");
                  load();
                  toast.success("Department added");
                })
              }
            >
              Add
            </Button>
          </div>
          <ul className="text-[13px]">
            {depts.map((d) => (
              <li key={d.id} className="border-b py-1.5">{d.name}{d.location ? ` · ${d.location}` : ""}</li>
            ))}
          </ul>
        </section>
        <section>
          <h2 className="mb-2 text-[12px] font-medium uppercase text-muted-foreground">Shifts</h2>
          <div className="mb-2 flex gap-2">
            <Input className="h-9" value={shiftName} onChange={(e) => setShiftName(e.target.value)} placeholder="Name" />
            <Button
              className="h-9"
              onClick={() =>
                void hrApi
                  .createShift({
                    name: shiftName,
                    startTime: "09:30:00",
                    endTime: "18:30:00",
                    graceMinutes: 15,
                    weeklyOff: "Sunday",
                    isActive: true,
                  })
                  .then(() => {
                    setShiftName("");
                    load();
                  })
              }
            >
              Add
            </Button>
          </div>
          <ul className="text-[13px]">
            {shifts.map((s) => (
              <li key={s.id} className="border-b py-1.5">{s.name} · off {s.weeklyOff}</li>
            ))}
          </ul>
        </section>
        <section>
          <h2 className="mb-2 text-[12px] font-medium uppercase text-muted-foreground">Leave types</h2>
          <div className="mb-2 flex gap-2">
            <Input className="h-9 w-20" value={leaveCode} onChange={(e) => setLeaveCode(e.target.value)} placeholder="CL" />
            <Input className="h-9" value={leaveName} onChange={(e) => setLeaveName(e.target.value)} placeholder="Name" />
            <Button
              className="h-9"
              onClick={() =>
                void hrApi
                  .createLeaveType({
                    code: leaveCode,
                    name: leaveName,
                    monthlyEntitlement: 1,
                    paid: true,
                    carryForward: false,
                    approvalLevels: 1,
                    isActive: true,
                  })
                  .then(() => {
                    setLeaveCode("");
                    setLeaveName("");
                    load();
                  })
              }
            >
              Add
            </Button>
          </div>
          <ul className="text-[13px]">
            {types.map((t) => (
              <li key={t.id} className="border-b py-1.5">
                {t.code} · {t.name} · {t.monthlyEntitlement}/mo
              </li>
            ))}
          </ul>
        </section>
      </div>
    </PagePanel>
  );
}
