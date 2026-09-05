"use client";

import * as React from "react";
import { Package } from "lucide-react";
import { toast } from "sonner";

import { PeopleSelect, useHrPeople } from "@/components/hr/hr-kit";
import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { hrx } from "@/lib/hrms-api";

export default function HrAssetsPage() {
  const people = useHrPeople();
  const [employeeId, setEmployeeId] = React.useState("");
  const [assetType, setAssetType] = React.useState("Laptop");
  const [serial, setSerial] = React.useState("");
  const [rows, setRows] = React.useState<Awaited<ReturnType<typeof hrx.assets>>>([]);

  const load = React.useCallback(() => {
    hrx.assets().then(setRows).catch(() => setRows([]));
  }, []);
  React.useEffect(() => {
    load();
  }, [load]);
  React.useEffect(() => {
    if (people[0] && !employeeId) setEmployeeId(String(people[0].id));
  }, [people, employeeId]);

  const names = Object.fromEntries(people.map((p) => [p.id, p.name]));

  return (
    <PagePanel icon={Package} title="Assets" hint="Issue and recover company property — Horilla asset module, tied to F&F on exit.">
      <div className="mb-4 flex flex-wrap gap-2">
        <PeopleSelect value={employeeId} onChange={setEmployeeId} people={people} />
        <Input className="h-9 w-36" value={assetType} onChange={(e) => setAssetType(e.target.value)} />
        <Input className="h-9 w-40" placeholder="Serial" value={serial} onChange={(e) => setSerial(e.target.value)} />
        <Button
          size="sm"
          className="h-9"
          onClick={() =>
            void hrx
              .issueAsset({
                employeeId: Number(employeeId),
                assetType,
                serialNo: serial,
                issueDate: new Date().toISOString().slice(0, 10),
                condition: "Good",
              })
              .then(() => {
                setSerial("");
                toast.success("Issued");
                load();
              })
          }
        >
          Issue
        </Button>
      </div>
      <table className="w-full text-left text-[13px]">
        <thead className="text-[11px] uppercase text-muted-foreground">
          <tr>
            <th className="p-2">Employee</th>
            <th className="p-2">Asset</th>
            <th className="p-2">Serial</th>
            <th className="p-2">Issued</th>
            <th className="p-2">Returned</th>
            <th className="p-2" />
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.id} className="border-t">
              <td className="p-2">{names[r.employeeId] ?? r.employeeId}</td>
              <td className="p-2">{r.assetType}</td>
              <td className="p-2 font-mono text-[12px]">{r.serialNo ?? "—"}</td>
              <td className="p-2">{String(r.issueDate).slice(0, 10)}</td>
              <td className="p-2">{r.returnDate ? String(r.returnDate).slice(0, 10) : "Out"}</td>
              <td className="p-2">
                {!r.returnDate ? (
                  <Button size="sm" variant="outline" className="h-7" onClick={() => void hrx.returnAsset(r.id).then(load)}>
                    Recover
                  </Button>
                ) : null}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </PagePanel>
  );
}
