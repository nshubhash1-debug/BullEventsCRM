"use client";

import * as React from "react";

import { cn } from "@/lib/utils";
import { hrEmployeesApi, type HrEmployee } from "@/lib/hrms-api";

export function useHrPeople() {
  const [people, setPeople] = React.useState<HrEmployee[]>([]);
  React.useEffect(() => {
    hrEmployeesApi
      .query({ page: 1, pageSize: 200, sort: [], filter: null })
      .then((r) => setPeople(r.items))
      .catch(() => setPeople([]));
  }, []);
  return people;
}

export function PeopleSelect({
  value,
  onChange,
  people,
  className,
}: {
  value: string;
  onChange: (id: string) => void;
  people: HrEmployee[];
  className?: string;
}) {
  return (
    <select
      className={cn("h-9 rounded-md border bg-background px-2 text-sm", className)}
      value={value}
      onChange={(e) => onChange(e.target.value)}
    >
      {people.map((p) => (
        <option key={p.id} value={p.id}>
          {p.employeeCode} · {p.name}
        </option>
      ))}
    </select>
  );
}

export function HrBadge({ children }: { children: React.ReactNode }) {
  return (
    <span className="rounded-full border bg-muted/60 px-2 py-0.5 text-[11px] font-medium tracking-wide">
      {children}
    </span>
  );
}

export function HrRow({ children }: { children: React.ReactNode }) {
  return (
    <li className="flex flex-wrap items-center justify-between gap-2 rounded-lg border bg-card px-3 py-2.5 text-[13px]">
      {children}
    </li>
  );
}
