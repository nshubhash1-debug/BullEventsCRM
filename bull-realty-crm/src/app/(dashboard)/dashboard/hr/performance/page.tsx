"use client";

import * as React from "react";
import { Goal } from "lucide-react";
import { toast } from "sonner";

import { HrRow, PeopleSelect, useHrPeople } from "@/components/hr/hr-kit";
import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { humanise } from "@/lib/crm-api";
import { hrx } from "@/lib/hrms-api";

export default function HrPerformancePage() {
  const people = useHrPeople();
  const [employeeId, setEmployeeId] = React.useState("");
  const [title, setTitle] = React.useState("");
  const [kra, setKra] = React.useState("Revenue");
  const [goals, setGoals] = React.useState<Awaited<ReturnType<typeof hrx.goals>>>([]);
  const [cycles, setCycles] = React.useState<Awaited<ReturnType<typeof hrx.cycles>>>([]);
  const [apps, setApps] = React.useState<Awaited<ReturnType<typeof hrx.appraisals>>>([]);
  const [cycleName, setCycleName] = React.useState("");

  const load = React.useCallback(() => {
    hrx.goals().then(setGoals).catch(() => setGoals([]));
    hrx.cycles().then(setCycles).catch(() => setCycles([]));
    hrx.appraisals().then(setApps).catch(() => setApps([]));
  }, []);

  React.useEffect(() => {
    load();
  }, [load]);
  React.useEffect(() => {
    if (people[0] && !employeeId) setEmployeeId(String(people[0].id));
  }, [people, employeeId]);

  return (
    <PagePanel
      icon={Goal}
      title="Performance"
      hint="Appraisal cycles, weighted KRAs and self/manager scores — Frappe HR PMS, wired to this employee master."
    >
      <div className="mb-4 flex flex-wrap gap-2">
        <Input className="h-9 w-48" placeholder="Cycle name" value={cycleName} onChange={(e) => setCycleName(e.target.value)} />
        <Button
          size="sm"
          className="h-9"
          onClick={() => {
            const y = new Date().getFullYear();
            void hrx
              .createCycle({
                name: cycleName || `${y} review`,
                fromDate: `${y}-01-01`,
                toDate: `${y}-12-31`,
              })
              .then(() => {
                setCycleName("");
                load();
              });
          }}
        >
          New cycle
        </Button>
        <PeopleSelect value={employeeId} onChange={setEmployeeId} people={people} />
        <Input className="h-9 w-56" placeholder="Goal" value={title} onChange={(e) => setTitle(e.target.value)} />
        <Input className="h-9 w-32" placeholder="KRA" value={kra} onChange={(e) => setKra(e.target.value)} />
        <Button
          size="sm"
          variant="outline"
          className="h-9"
          onClick={() =>
            void hrx
              .createGoal({
                employeeId: Number(employeeId),
                cycleId: cycles[0]?.id ?? null,
                title,
                kra,
                weight: 25,
              })
              .then(() => {
                setTitle("");
                toast.success("Goal added");
                load();
              })
          }
        >
          Add goal
        </Button>
      </div>
      <div className="grid gap-6 lg:grid-cols-2">
        <div>
          <h2 className="mb-2 text-[12px] font-semibold uppercase text-muted-foreground">Goals</h2>
          <ul className="space-y-2">
            {goals.map((g) => (
              <HrRow key={g.id}>
                <span>
                  {g.employeeName} · {g.title}
                  <span className="ml-2 text-muted-foreground">
                    {g.kra} · {g.progress}% · {humanise(g.status)}
                  </span>
                </span>
                <span className="flex gap-1">
                  {[25, 50, 75, 100].map((p) => (
                    <Button key={p} size="sm" variant="outline" className="h-7 text-[11px]" onClick={() => void hrx.goalProgress(g.id, p).then(load)}>
                      {p}%
                    </Button>
                  ))}
                </span>
              </HrRow>
            ))}
          </ul>
        </div>
        <div>
          <h2 className="mb-2 text-[12px] font-semibold uppercase text-muted-foreground">Appraisals</h2>
          <ul className="space-y-2">
            {apps.map((a) => (
              <HrRow key={a.id}>
                <span>
                  {a.employeeName} · self {a.selfScore ?? "—"} · mgr {a.managerScore ?? "—"} · {humanise(a.status)}
                </span>
              </HrRow>
            ))}
          </ul>
          {cycles[0] && employeeId ? (
            <Button
              className="mt-3"
              size="sm"
              onClick={() =>
                void hrx
                  .saveAppraisal({
                    cycleId: cycles[0].id,
                    employeeId: Number(employeeId),
                    selfScore: 4,
                    managerScore: 4.2,
                    rating: "Exceeds",
                    comments: "On track vs KRAs.",
                  })
                  .then(() => {
                    toast.success("Review saved");
                    load();
                  })
              }
            >
              File review for selected employee
            </Button>
          ) : null}
        </div>
      </div>
    </PagePanel>
  );
}
