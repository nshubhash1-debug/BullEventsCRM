"use client";

import * as React from "react";
import { Plus, UsersRound } from "lucide-react";
import { toast } from "sonner";

import type { GridColumn } from "@/components/crm/crm-grid";
import { ListShell, type QuickView } from "@/components/crm/list-shell";
import { Pill, StatusDot, type Metric } from "@/components/crm/metrics";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useCrmList } from "@/hooks/use-crm-list";
import { ApiError } from "@/lib/api";
import { formatDate, humanise } from "@/lib/crm-api";
import { emptyRoot, type FilterNode } from "@/lib/query";
import { hrApi, hrEmployeesApi, type HrEmployee } from "@/lib/hrms-api";

function condition(field: string, operator: string, value?: string): FilterNode {
  const root = emptyRoot();
  return { ...root, children: [{ key: `${root.key}-c`, field, operator, value }] };
}

export default function HrEmployeesPage() {
  const state = useCrmList<HrEmployee>(hrEmployeesApi, {
    facets: ["status", "employmentType", "departmentName"],
    dateFields: [{ id: "joiningDate", label: "Joined" }],
    sort: [{ field: "name", descending: false }],
    pageSize: 50,
  });

  const [open, setOpen] = React.useState(false);
  const [code, setCode] = React.useState("");
  const [name, setName] = React.useState("");
  const [phone, setPhone] = React.useState("");
  const [email, setEmail] = React.useState("");
  const [joining, setJoining] = React.useState(() => new Date().toISOString().slice(0, 10));
  const [deptId, setDeptId] = React.useState("");
  const [depts, setDepts] = React.useState<{ id: number; name: string }[]>([]);

  React.useEffect(() => {
    hrApi.departments().then((rows) => setDepts(rows)).catch(() => setDepts([]));
  }, []);

  const metrics: Metric[] = [
    { label: "In view", value: String(state.total) },
    {
      label: "On rolls",
      value: String(state.aggregates.headcount ?? 0),
      tone: "success",
    },
    { label: "Probation", value: String(state.aggregates.probation ?? 0), tone: "warning" },
    { label: "Notice", value: String(state.aggregates.notice ?? 0), tone: "danger" },
  ];

  const quickViews: QuickView[] = [
    {
      id: "rolls",
      label: "On rolls",
      build: () => {
        const root = emptyRoot();
        return {
          ...root,
          children: [
            {
              key: `${root.key}-c`,
              field: "status",
              operator: "in",
              values: ["Active", "Probation", "Confirmed", "NoticePeriod"],
            },
          ],
        };
      },
    },
    { id: "probation", label: "Probation", build: () => condition("status", "equals", "Probation") },
    { id: "notice", label: "Notice", build: () => condition("status", "equals", "NoticePeriod") },
  ];

  const columns: GridColumn<HrEmployee>[] = [
    {
      id: "code",
      label: "ID",
      sortField: "employeeCode",
      sticky: true,
      width: 88,
      render: (row) => <span className="font-medium tabular-nums">{row.employeeCode}</span>,
    },
    { id: "name", label: "Name", sortField: "name", width: 180, render: (row) => row.name },
    {
      id: "status",
      label: "Status",
      sortField: "status",
      width: 120,
      render: (row) => <StatusDot label={humanise(row.status)} tone="primary" />,
    },
    {
      id: "dept",
      label: "Department",
      sortField: "departmentName",
      width: 140,
      render: (row) => row.departmentName,
    },
    {
      id: "desig",
      label: "Designation",
      width: 140,
      render: (row) => row.designationName,
    },
    {
      id: "type",
      label: "Type",
      width: 110,
      render: (row) => <Pill tone="neutral">{humanise(row.employmentType)}</Pill>,
    },
    {
      id: "joined",
      label: "Joined",
      sortField: "joiningDate",
      width: 110,
      render: (row) => formatDate(row.joiningDate),
    },
  ];

  async function create() {
    if (!code.trim() || !name.trim()) {
      toast.error("Employee ID and name are required.");
      return;
    }
    try {
      await hrEmployeesApi.create({
        employeeCode: code.trim(),
        name: name.trim(),
        phone: phone || null,
        email: email || null,
        joiningDate: joining,
        employmentType: "Permanent",
        status: "Probation",
        collarType: "WhiteCollar",
        departmentId: deptId ? Number(deptId) : null,
      });
      toast.success("Employee created — onboarding checklist is ready.");
      setOpen(false);
      state.refresh();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not create.");
    }
  }

  return (
    <>
      <ListShell
        icon={UsersRound}
        title="Employees"
        storageKey="hr-employees"
        hint="Employee master: ID, department, designation, manager, status. Export from HR reports."
        state={state}
        columns={columns}
        metrics={metrics}
        quickViews={quickViews}
        searchPlaceholder="Name, ID, phone…"
        emptyMessage="No employees match this view."
        actions={
          <Button size="sm" className="h-8" onClick={() => setOpen(true)}>
            <Plus className="size-3.5" /> New employee
          </Button>
        }
      />
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New employee</DialogTitle>
          </DialogHeader>
          <div className="grid gap-3">
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label>Employee ID</Label>
                <Input value={code} onChange={(e) => setCode(e.target.value)} />
              </div>
              <div className="space-y-1">
                <Label>Joining date</Label>
                <Input type="date" value={joining} onChange={(e) => setJoining(e.target.value)} />
              </div>
            </div>
            <div className="space-y-1">
              <Label>Name</Label>
              <Input value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label>Phone</Label>
                <Input value={phone} onChange={(e) => setPhone(e.target.value)} />
              </div>
              <div className="space-y-1">
                <Label>Email</Label>
                <Input value={email} onChange={(e) => setEmail(e.target.value)} />
              </div>
            </div>
            <div className="space-y-1">
              <Label>Department</Label>
              <select
                className="h-9 w-full rounded-md border bg-background px-2 text-sm"
                value={deptId}
                onChange={(e) => setDeptId(e.target.value)}
              >
                <option value="">—</option>
                {depts.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => void create()}>Create</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
