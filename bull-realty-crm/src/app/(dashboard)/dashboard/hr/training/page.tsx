"use client";

import * as React from "react";
import { GraduationCap } from "lucide-react";
import { toast } from "sonner";

import { PeopleSelect, useHrPeople } from "@/components/hr/hr-kit";
import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { hrx } from "@/lib/hrms-api";

export default function HrTrainingPage() {
  const people = useHrPeople();
  const [employeeId, setEmployeeId] = React.useState("");
  const [title, setTitle] = React.useState("");
  const [trainer, setTrainer] = React.useState("");
  const [rows, setRows] = React.useState<Awaited<ReturnType<typeof hrx.trainings>>>([]);

  const load = React.useCallback(() => {
    hrx.trainings().then(setRows).catch(() => setRows([]));
  }, []);
  React.useEffect(() => {
    load();
  }, [load]);
  React.useEffect(() => {
    if (people[0] && !employeeId) setEmployeeId(String(people[0].id));
  }, [people, employeeId]);

  const today = new Date().toISOString().slice(0, 10);

  return (
    <PagePanel icon={GraduationCap} title="Training" hint="Sessions, trainers and enrolment — Frappe training events.">
      <div className="mb-4 flex flex-wrap gap-2">
        <Input className="h-9 w-52" placeholder="Programme" value={title} onChange={(e) => setTitle(e.target.value)} />
        <Input className="h-9 w-40" placeholder="Trainer" value={trainer} onChange={(e) => setTrainer(e.target.value)} />
        <Button
          size="sm"
          className="h-9"
          onClick={() =>
            void hrx
              .createTraining({ title, trainer, fromDate: today, toDate: today, venue: "HQ" })
              .then(() => {
                setTitle("");
                load();
              })
          }
        >
          Schedule
        </Button>
        <PeopleSelect value={employeeId} onChange={setEmployeeId} people={people} />
      </div>
      <ul className="space-y-2">
        {rows.map((t) => (
          <li key={t.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 text-[13px]">
            <span>
              <span className="font-medium">{t.title}</span>
              <span className="text-muted-foreground">
                {" "}
                · {t.trainer ?? "—"} · {String(t.fromDate).slice(0, 10)} · {t.enrolments} enrolled
              </span>
            </span>
            <Button
              size="sm"
              variant="outline"
              className="h-7"
              onClick={() =>
                void hrx.enrol(t.id, Number(employeeId)).then(() => {
                  toast.success("Enrolled");
                  load();
                })
              }
            >
              Enrol selected
            </Button>
          </li>
        ))}
      </ul>
    </PagePanel>
  );
}
