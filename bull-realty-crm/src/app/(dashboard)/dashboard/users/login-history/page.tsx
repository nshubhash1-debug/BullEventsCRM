"use client";

import * as React from "react";
import {
  History,
  Loader2,
  Monitor,
  Search,
  ShieldOff,
  Smartphone,
  Users,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { MetricStrip, Pill, TONE_TEXT, type Metric } from "@/components/crm/metrics";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { accessApi, ApiError, type LoginHistory, type LoginSession } from "@/lib/api";
import { cn } from "@/lib/utils";

const WINDOWS = [
  { value: "1", label: "Last 24 hours" },
  { value: "7", label: "Last 7 days" },
  { value: "30", label: "Last 30 days" },
  { value: "90", label: "Last 90 days" },
];

/**
 * Login history.
 *
 * Reads the same session table the request guard consults on every call, so
 * this is a window onto enforcement rather than a second record of it: a row
 * that says live means that token really does still open the CRM, and revoking
 * it here ends the session within seconds.
 *
 * That is the whole reason the screen earns its place. An audit log tells an
 * administrator what happened; this tells them what is happening, and lets them
 * stop it.
 */
export default function LoginHistoryPage() {
  const [data, setData] = React.useState<LoginHistory | null>(null);
  const [days, setDays] = React.useState("30");
  const [liveOnly, setLiveOnly] = React.useState(false);
  const [search, setSearch] = React.useState("");
  const [busy, setBusy] = React.useState<number | null>(null);

  const load = React.useCallback(() => {
    accessApi
      .loginHistory({ days: Number(days), liveOnly: liveOnly || undefined })
      .then(setData)
      .catch((error: unknown) => {
        toast.error("Could not load login history", {
          description: error instanceof ApiError ? error.message : "Network error.",
        });
        setData({
          sessions: [],
          live: 0,
          revoked: 0,
          expired: 0,
          distinctUsers: 0,
          windowDays: Number(days),
        });
      });
  }, [days, liveOnly]);

  React.useEffect(load, [load]);

  async function revoke(session: LoginSession) {
    setBusy(session.id);
    try {
      await accessApi.revokeSession(session.id);
      toast.success("Session ended", {
        description: `${session.userName} is signed out on that device.`,
      });
      load();
    } catch (error) {
      toast.error("Could not end the session", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  const rows = React.useMemo(() => {
    const list = data?.sessions ?? [];
    const needle = search.trim().toLowerCase();
    if (!needle) return list;

    return list.filter(
      (s) =>
        s.userName.toLowerCase().includes(needle) ||
        s.userEmail.toLowerCase().includes(needle) ||
        (s.ipAddress?.toLowerCase().includes(needle) ?? false) ||
        (s.device?.toLowerCase().includes(needle) ?? false)
    );
  }, [data, search]);

  const metrics: Metric[] = [
    {
      label: "Live now",
      value: data?.live ?? 0,
      tone: "success",
      icon: Monitor,
      hint: "Sessions that still open the CRM",
    },
    { label: "People", value: data?.distinctUsers ?? 0, icon: Users, hint: "Signed in over the window" },
    { label: "Revoked", value: data?.revoked ?? 0, tone: "danger", icon: ShieldOff },
    { label: "Expired", value: data?.expired ?? 0, tone: "neutral", hint: "Ran out on their own" },
    { label: "Sign-ins", value: data?.sessions.length ?? 0, icon: History },
  ];

  return (
    <PagePanel
      icon={History}
      title="Login history"
      hint="Every sign-in, the device and address it came from, and whether it is still open."
      toolbar={
        <div className="flex flex-wrap items-center gap-2">
          <div className="relative min-w-0 flex-1 sm:max-w-xs">
            <Search className="absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Name, email, IP or device"
              className="h-8 pl-8 text-[12.5px]"
            />
          </div>

          <Select
            value={days}
            onValueChange={(next) => {
              setData(null);
              setDays(next);
            }}
          >
            <SelectTrigger size="sm" className="w-[160px] text-[12.5px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {WINDOWS.map((w) => (
                <SelectItem key={w.value} value={w.value}>
                  {w.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Button
            variant={liveOnly ? "default" : "outline"}
            size="sm"
            className="h-8 gap-1.5 text-[12.5px]"
            onClick={() => {
              setData(null);
              setLiveOnly((on) => !on);
            }}
          >
            <Monitor className="size-3.5" />
            Live only
          </Button>
        </div>
      }
    >
      <div className="flex min-h-0 flex-col gap-3">
        <MetricStrip metrics={metrics} loading={data === null} />

        {data === null ? (
          <CrmLoadingState label="Loading login history" />
        ) : rows.length === 0 ? (
          <p className="py-12 text-center text-[13px] text-muted-foreground">
            No sign-in matches these filters.
          </p>
        ) : (
          <div className="min-h-0 flex-1 overflow-auto rounded-lg border">
            <table className="w-full min-w-[940px] text-[12.5px]">
              <thead className="sticky top-0 z-10 bg-muted/70 backdrop-blur">
                <tr className="text-[10.5px] tracking-wide text-muted-foreground uppercase">
                  <th className="px-3 py-2 text-left font-medium">Who</th>
                  <th className="px-3 py-2 text-left font-medium">Signed in</th>
                  <th className="px-3 py-2 text-left font-medium">Last seen</th>
                  <th className="px-3 py-2 text-left font-medium">From</th>
                  <th className="px-3 py-2 text-left font-medium">State</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>

              <tbody>
                {rows.map((session) => (
                  <tr key={session.id} className="border-t align-top hover:bg-muted/40">
                    <td className="px-3 py-2">
                      <p className="font-medium">{session.userName}</p>
                      <p className="text-[11px] text-muted-foreground">
                        {session.userEmail} · {session.role}
                      </p>
                    </td>

                    <td className="px-3 py-2 whitespace-nowrap">{stamp(session.issuedAt)}</td>

                    <td className="px-3 py-2 whitespace-nowrap">
                      {session.isLive ? ago(session.lastSeenAt) : stamp(session.lastSeenAt)}
                    </td>

                    <td className="px-3 py-2">
                      <p className="flex items-center gap-1.5">
                        {/* The device string is a user agent, so it is only ever
                            a hint — shown as one rather than parsed into a
                            certainty the header cannot actually support. */}
                        {mobile(session.device) ? (
                          <Smartphone className="size-3.5 text-muted-foreground" />
                        ) : (
                          <Monitor className="size-3.5 text-muted-foreground" />
                        )}
                        <span className="tabular-nums">{session.ipAddress ?? "—"}</span>
                      </p>
                      {session.device ? (
                        <p
                          className="max-w-[280px] truncate text-[11px] text-muted-foreground"
                          title={session.device}
                        >
                          {session.device}
                        </p>
                      ) : null}
                    </td>

                    <td className="px-3 py-2">
                      {session.isLive ? (
                        <Pill tone="success">Live</Pill>
                      ) : session.revokedAt ? (
                        <>
                          <Pill tone="danger">Revoked</Pill>
                          {session.revokedReason ? (
                            <p className="mt-0.5 text-[11px] text-muted-foreground">
                              {session.revokedReason}
                            </p>
                          ) : null}
                        </>
                      ) : (
                        <Pill tone="neutral">Expired</Pill>
                      )}
                    </td>

                    <td className="px-3 py-2 text-right whitespace-nowrap">
                      {busy === session.id ? (
                        <Loader2 className="ml-auto size-3.5 animate-spin text-muted-foreground" />
                      ) : session.isLive ? (
                        <Button
                          size="sm"
                          variant="outline"
                          className="h-6 gap-1 px-2 text-[11px]"
                          onClick={() => revoke(session)}
                        >
                          <ShieldOff className="size-3" />
                          End session
                        </Button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {data !== null && data.sessions.length >= 1000 ? (
          <p className={cn("text-[11.5px]", TONE_TEXT.warning)}>
            Showing the most recent 1,000 sign-ins. Narrow the window to see further back.
          </p>
        ) : null}
      </div>
    </PagePanel>
  );
}

function stamp(iso: string) {
  return new Date(iso).toLocaleString("en-IN", {
    day: "numeric",
    month: "short",
    hour: "numeric",
    minute: "2-digit",
  });
}

/**
 * "4 min ago" for a session somebody is holding right now.
 *
 * A live session is the one case where the clock time is the wrong answer: the
 * question being asked is "is anyone actually on this", and an elapsed figure
 * answers it without arithmetic.
 */
function ago(iso: string) {
  const seconds = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);

  if (seconds < 90) return "just now";
  if (seconds < 3600) return `${Math.round(seconds / 60)} min ago`;
  if (seconds < 86400) return `${Math.round(seconds / 3600)} h ago`;
  return `${Math.round(seconds / 86400)} d ago`;
}

function mobile(device: string | null) {
  if (!device) return false;
  return /android|iphone|ipad|mobile/i.test(device);
}
