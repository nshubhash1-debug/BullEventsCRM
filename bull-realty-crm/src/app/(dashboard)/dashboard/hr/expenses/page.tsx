"use client";

import * as React from "react";
import { Wallet } from "lucide-react";
import { toast } from "sonner";

import { HrRow, PeopleSelect, useHrPeople } from "@/components/hr/hr-kit";
import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { formatMoney, humanise } from "@/lib/crm-api";
import { hrx } from "@/lib/hrms-api";

export default function HrExpensesPage() {
  const people = useHrPeople();
  const [employeeId, setEmployeeId] = React.useState("");
  const [amount, setAmount] = React.useState("500");
  const [category, setCategory] = React.useState("Travel");
  const [desc, setDesc] = React.useState("");
  const [rows, setRows] = React.useState<Awaited<ReturnType<typeof hrx.expenses>>>([]);

  const load = React.useCallback(() => {
    hrx.expenses().then(setRows).catch(() => setRows([]));
  }, []);
  React.useEffect(() => {
    load();
  }, [load]);
  React.useEffect(() => {
    if (people[0] && !employeeId) setEmployeeId(String(people[0].id));
  }, [people, employeeId]);

  return (
    <PagePanel
      icon={Wallet}
      title="Expense claims"
      hint="Employee advance/claim flow from Frappe HR: submit → manager → HR/Accounts."
    >
      <div className="mb-4 flex flex-wrap gap-2">
        <PeopleSelect value={employeeId} onChange={setEmployeeId} people={people} />
        <select className="h-9 rounded-md border bg-background px-2 text-sm" value={category} onChange={(e) => setCategory(e.target.value)}>
          {["Travel", "Food", "Stay", "Client entertainment", "Materials", "Other"].map((c) => (
            <option key={c}>{c}</option>
          ))}
        </select>
        <Input className="h-9 w-28" type="number" value={amount} onChange={(e) => setAmount(e.target.value)} />
        <Input className="h-9 w-56" placeholder="Bill / reason" value={desc} onChange={(e) => setDesc(e.target.value)} />
        <Button
          size="sm"
          className="h-9"
          onClick={() =>
            void hrx
              .createExpense({
                employeeId: Number(employeeId),
                claimDate: new Date().toISOString().slice(0, 10),
                category,
                amount: Number(amount),
                description: desc,
              })
              .then(() => {
                setDesc("");
                toast.success("Claim submitted");
                load();
              })
          }
        >
          Submit
        </Button>
      </div>
      <ul className="space-y-2">
        {rows.map((r) => (
          <HrRow key={r.id}>
            <span>
              {r.employeeName} · {r.category} · {formatMoney(r.amount)} · {r.description ?? "—"} · {humanise(r.status)}
            </span>
            {r.status.includes("Pending") ? (
              <span className="flex gap-1">
                <Button size="sm" variant="outline" className="h-7" onClick={() => void hrx.decideExpense(r.id, true).then(load)}>
                  Approve
                </Button>
                <Button size="sm" variant="outline" className="h-7" onClick={() => void hrx.decideExpense(r.id, false).then(load)}>
                  Reject
                </Button>
              </span>
            ) : null}
          </HrRow>
        ))}
      </ul>
    </PagePanel>
  );
}
