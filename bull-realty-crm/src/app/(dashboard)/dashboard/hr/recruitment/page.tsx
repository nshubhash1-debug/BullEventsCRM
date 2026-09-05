"use client";

import * as React from "react";
import { Inbox } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { humanise } from "@/lib/crm-api";
import { hrApi } from "@/lib/hrms-api";

const STAGES = ["Applied", "Shortlisted", "Interview", "Selected", "Offered", "Joined", "Rejected"];

export default function HrRecruitmentPage() {
  const [vacancies, setVacancies] = React.useState<{ id: number; position: string; location: string | null; status: string }[]>([]);
  const [candidates, setCandidates] = React.useState<
    { id: number; name: string; stage: string; phone: string | null; convertedEmployeeId: number | null }[]
  >([]);
  const [position, setPosition] = React.useState("");
  const [candName, setCandName] = React.useState("");
  const [phone, setPhone] = React.useState("");

  const load = React.useCallback(() => {
    hrApi.vacancies().then(setVacancies).catch(() => setVacancies([]));
    hrApi.candidates().then(setCandidates).catch(() => setCandidates([]));
  }, []);

  React.useEffect(() => {
    load();
  }, [load]);

  return (
    <PagePanel
      icon={Inbox}
      title="Recruitment"
      hint="Vacancy → kanban pipeline → interview scorecard → convert. Horilla recruitment without a second product."
    >
      <div className="mb-4 flex flex-wrap gap-2">
        <Input className="h-9 w-52" placeholder="Position" value={position} onChange={(e) => setPosition(e.target.value)} />
        <Button
          size="sm"
          className="h-9"
          onClick={() =>
            void hrApi
              .createVacancy({ position, status: "Open" })
              .then(() => {
                setPosition("");
                load();
              })
              .catch((e) => toast.error(e instanceof ApiError ? e.message : "Failed"))
          }
        >
          Add vacancy
        </Button>
        <Input className="h-9 w-44" placeholder="Candidate" value={candName} onChange={(e) => setCandName(e.target.value)} />
        <Input className="h-9 w-36" placeholder="Phone" value={phone} onChange={(e) => setPhone(e.target.value)} />
        <Button
          size="sm"
          variant="outline"
          className="h-9"
          onClick={() =>
            void hrApi
              .createCandidate({
                name: candName,
                phone,
                stage: "Applied",
                vacancyId: vacancies[0]?.id ?? null,
              })
              .then(() => {
                setCandName("");
                load();
              })
          }
        >
          Add candidate
        </Button>
      </div>
      <div className="mb-6">
        <h2 className="mb-2 text-[12px] font-medium uppercase text-muted-foreground">Vacancies</h2>
          <ul className="space-y-1 text-[13px]">
            {vacancies.map((v) => (
              <li key={v.id} className="rounded border px-3 py-2">
                {v.position} · {v.location ?? "—"} · {v.status}
              </li>
            ))}
          </ul>
        </div>
      <div className="grid gap-3 overflow-x-auto pb-2 lg:grid-cols-7">
        {STAGES.map((stage) => (
          <div key={stage} className="min-w-[160px] rounded-xl border bg-muted/20 p-2">
            <p className="mb-2 px-1 text-[11px] font-semibold uppercase text-muted-foreground">
              {humanise(stage)} · {candidates.filter((c) => c.stage === stage).length}
            </p>
            <ul className="space-y-2">
              {candidates
                .filter((c) => c.stage === stage)
                .map((c) => (
                  <li key={c.id} className="rounded-lg border bg-card p-2 text-[12px] shadow-xs">
                    <p className="font-medium">{c.name}</p>
                    <p className="text-muted-foreground">{c.phone ?? "—"}</p>
                    <div className="mt-2 flex flex-wrap gap-1">
                      {STAGES.filter((s) => s !== stage).slice(0, 2).map((s) => (
                        <Button
                          key={s}
                          size="sm"
                          variant="outline"
                          className="h-6 px-1.5 text-[10px]"
                          onClick={() => void hrApi.moveStage(c.id, s).then(load)}
                        >
                          {s}
                        </Button>
                      ))}
                      {c.stage === "Offered" || c.stage === "Selected" ? (
                        <Button
                          size="sm"
                          className="h-6 px-1.5 text-[10px]"
                          onClick={() =>
                            void hrApi.convertCandidate(c.id).then(() => {
                              toast.success("Converted to employee");
                              load();
                            })
                          }
                        >
                          Convert
                        </Button>
                      ) : null}
                    </div>
                  </li>
                ))}
            </ul>
          </div>
        ))}
      </div>
    </PagePanel>
  );
}
