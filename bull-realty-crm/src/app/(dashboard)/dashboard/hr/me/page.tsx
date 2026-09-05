"use client";

import * as React from "react";
import { UserRound } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { formatMoney } from "@/lib/crm-api";
import { ApiError } from "@/lib/api";
import { hrApi, hrx } from "@/lib/hrms-api";

export default function HrMePage() {
  const [data, setData] = React.useState<Awaited<ReturnType<typeof hrApi.me>> | null>(null);
  const [news, setNews] = React.useState<Awaited<ReturnType<typeof hrx.announcements>>>([]);
  const [holidays, setHolidays] = React.useState<Awaited<ReturnType<typeof hrx.holidays>>>([]);
  const [missing, setMissing] = React.useState(false);

  React.useEffect(() => {
    hrApi.me().then(setData).catch(() => {
      setData(null);
      setMissing(true);
    });
    hrx.announcements().then(setNews).catch(() => setNews([]));
    hrx.holidays().then(setHolidays).catch(() => setHolidays([]));
  }, []);

  async function punch(kind: "In" | "Out") {
    try {
      let latitude: number | undefined;
      let longitude: number | undefined;
      if (kind === "In" && navigator.geolocation) {
        try {
          const pos = await new Promise<GeolocationPosition>((resolve, reject) =>
            navigator.geolocation.getCurrentPosition(resolve, reject, { timeout: 8000 })
          );
          latitude = pos.coords.latitude;
          longitude = pos.coords.longitude;
        } catch {
          /* optional */
        }
      }
      await hrx.punch({ kind, latitude, longitude, device: "ESS" });
      toast.success(kind === "In" ? "Checked in" : "Checked out");
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Punch failed");
    }
  }

  if (missing && !data) {
    return (
      <PagePanel icon={UserRound} title="My HR" hint="Your profile, leave balances and payslips.">
        <p className="text-[13px] text-muted-foreground">
          No employee record is linked to this login yet. Ask HR to set your User on the employee master.
        </p>
      </PagePanel>
    );
  }

  if (!data) {
    return (
      <PagePanel icon={UserRound} title="My HR" hint="Self-service.">
        <p className="text-[13px] text-muted-foreground">Loading…</p>
      </PagePanel>
    );
  }

  return (
    <PagePanel icon={UserRound} title="My HR" hint="Employee self-service: punch, balances, slips, handbook.">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-[15px] font-medium">
          {data.employee.name} · {data.employee.employeeCode} · {data.employee.departmentName}
        </p>
        <div className="flex gap-2">
          <Button size="sm" onClick={() => void punch("In")}>Check-in</Button>
          <Button size="sm" variant="outline" onClick={() => void punch("Out")}>Check-out</Button>
        </div>
      </div>
      <div className="mt-6 grid gap-6 lg:grid-cols-3">
        <section>
          <h2 className="text-[12px] font-medium uppercase text-muted-foreground">Leave balances</h2>
          <ul className="mt-1 text-[13px]">
            {data.balances.map((b) => (
              <li key={b.leaveType}>
                {b.leaveType}: {b.closing}
              </li>
            ))}
          </ul>
        </section>
        <section>
          <h2 className="text-[12px] font-medium uppercase text-muted-foreground">Payslips</h2>
          <ul className="mt-1 text-[13px]">
            {data.recentPayslips.map((s) => (
              <li key={s.id}>
                Run {s.payrollRunId} · {formatMoney(s.net)}
              </li>
            ))}
          </ul>
        </section>
        <section>
          <h2 className="text-[12px] font-medium uppercase text-muted-foreground">Holidays</h2>
          <ul className="mt-1 text-[13px]">
            {holidays.slice(0, 6).map((h) => (
              <li key={h.id}>
                {String(h.onDate).slice(0, 10)} · {h.name}
              </li>
            ))}
          </ul>
        </section>
      </div>
      <h2 className="mt-6 text-[12px] font-medium uppercase text-muted-foreground">News</h2>
      <ul className="mt-1 space-y-1 text-[13px]">
        {news.map((n) => (
          <li key={n.id}>
            <span className="font-medium">{n.title}</span> — {n.body}
          </li>
        ))}
      </ul>
    </PagePanel>
  );
}
