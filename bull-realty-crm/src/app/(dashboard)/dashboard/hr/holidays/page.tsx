"use client";

import * as React from "react";
import { Landmark } from "lucide-react";

import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { hrx } from "@/lib/hrms-api";

export default function HrHolidaysPage() {
  const [rows, setRows] = React.useState<Awaited<ReturnType<typeof hrx.holidays>>>([]);
  const [name, setName] = React.useState("");
  const [onDate, setOnDate] = React.useState(() => new Date().toISOString().slice(0, 10));

  const load = React.useCallback(() => {
    hrx.holidays().then(setRows).catch(() => setRows([]));
  }, []);
  React.useEffect(() => {
    load();
  }, [load]);

  return (
    <PagePanel
      icon={Landmark}
      title="Holiday calendar"
      hint="Company holidays feed attendance (weekly-off / holiday status) the same way Frappe holiday lists do."
    >
      <div className="mb-4 flex flex-wrap gap-2">
        <Input className="h-9 w-48" placeholder="Holiday" value={name} onChange={(e) => setName(e.target.value)} />
        <Input className="h-9 w-40" type="date" value={onDate} onChange={(e) => setOnDate(e.target.value)} />
        <Button
          size="sm"
          className="h-9"
          onClick={() =>
            void hrx.createHoliday({ name, onDate, optional: false }).then(() => {
              setName("");
              load();
            })
          }
        >
          Add
        </Button>
      </div>
      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
        {rows.map((h) => (
          <div key={h.id} className="rounded-xl border p-3">
            <p className="text-[13px] font-medium">{h.name}</p>
            <p className="text-[12px] tabular-nums text-muted-foreground">{String(h.onDate).slice(0, 10)}</p>
            {h.optional ? <p className="text-[11px] text-primary">Optional</p> : null}
          </div>
        ))}
      </div>
    </PagePanel>
  );
}
