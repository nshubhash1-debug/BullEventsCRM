"use client";

import * as React from "react";
import { Laptop, Loader2, LogOut, ShieldCheck } from "lucide-react";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import {
  ApiError,
  getSessions,
  revokeOtherSessions,
  revokeSession,
  type DeviceSession,
} from "@/lib/api";

/**
 * The devices this account is signed in on, and the button that ends one.
 *
 * Worth a screen because the token is a JWT: it stays valid until it expires
 * whatever the browser does with it, so "I left it signed in on the office
 * machine" has no answer without this.
 */
export function SessionsDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const [sessions, setSessions] = React.useState<DeviceSession[] | null>(null);
  const [busy, setBusy] = React.useState<number | "others" | null>(null);

  // Cleared on the way open, during render rather than in the effect below, so
  // a reopen never shows the previous list while the new one loads.
  const [wasOpen, setWasOpen] = React.useState(open);
  if (open !== wasOpen) {
    setWasOpen(open);
    if (open) setSessions(null);
  }

  React.useEffect(() => {
    if (!open) return;

    getSessions()
      .then(setSessions)
      .catch((error: unknown) => {
        toast.error("Could not load your devices", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setSessions([]);
      });
  }, [open]);

  async function endOne(session: DeviceSession) {
    setBusy(session.id);
    try {
      await revokeSession(session.id);
      setSessions((current) =>
        (current ?? []).filter((s) => s.id !== session.id)
      );
      toast.success(`Signed out of ${session.device}`);
    } catch (error) {
      toast.error("Could not sign that device out", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  async function endOthers() {
    setBusy("others");
    try {
      const { ended } = await revokeOtherSessions();
      setSessions((current) => (current ?? []).filter((s) => s.isCurrent));
      toast.success(
        ended === 0
          ? "No other devices were signed in"
          : `Signed out of ${ended} other ${ended === 1 ? "device" : "devices"}`
      );
    } catch (error) {
      toast.error("Could not sign the other devices out", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setBusy(null);
    }
  }

  const others = (sessions ?? []).filter((s) => !s.isCurrent).length;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Signed-in devices</DialogTitle>
          <DialogDescription>
            Every browser holding a live session for this account. Signing one
            out takes effect within a few seconds, wherever it is.
          </DialogDescription>
        </DialogHeader>

        <div className="flex max-h-[26rem] flex-col gap-1.5 overflow-y-auto py-2">
          {sessions === null ? (
            <CrmLoadingState label="Loading devices" />
          ) : sessions.length === 0 ? (
            <p className="text-[12.5px] text-muted-foreground">
              No live sessions — which should be impossible while you are
              reading this. Try reloading.
            </p>
          ) : (
            sessions.map((session) => (
              <div
                key={session.id}
                className="flex items-center gap-3 rounded-md border px-3 py-2.5"
              >
                <span className="flex size-8 shrink-0 items-center justify-center rounded-md bg-muted text-muted-foreground">
                  <Laptop className="size-4" />
                </span>

                <div className="min-w-0 flex-1">
                  <p className="flex items-center gap-1.5 truncate text-[13px] font-medium">
                    {session.device}
                    {session.isCurrent ? (
                      <Badge
                        variant="outline"
                        className="h-4 px-1 text-[9px] font-normal text-emerald-600 dark:text-emerald-400"
                      >
                        This device
                      </Badge>
                    ) : null}
                  </p>
                  <p className="truncate text-[11px] text-muted-foreground">
                    {session.ipAddress ?? "Unknown address"} · last used{" "}
                    {relative(session.lastSeenAt)}
                  </p>
                </div>

                {session.isCurrent ? null : (
                  <Button
                    size="sm"
                    variant="outline"
                    className="h-7 shrink-0"
                    disabled={busy !== null}
                    onClick={() => void endOne(session)}
                  >
                    {busy === session.id ? (
                      <Loader2 className="animate-spin" />
                    ) : (
                      <LogOut />
                    )}
                    Sign out
                  </Button>
                )}
              </div>
            ))
          )}
        </div>

        <DialogFooter className="sm:justify-between">
          <span className="flex items-center gap-1.5 text-[11px] text-muted-foreground">
            <ShieldCheck className="size-3.5" />
            Changing your password ends every other session too.
          </span>
          <Button
            variant="outline"
            disabled={others === 0 || busy !== null}
            onClick={() => void endOthers()}
          >
            {busy === "others" ? <Loader2 className="animate-spin" /> : null}
            Sign out everywhere else
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function relative(iso: string) {
  const minutes = Math.round((Date.now() - new Date(iso).getTime()) / 60_000);

  if (minutes < 2) return "just now";
  if (minutes < 60) return `${minutes} minutes ago`;

  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} ${hours === 1 ? "hour" : "hours"} ago`;

  const days = Math.round(hours / 24);
  return `${days} ${days === 1 ? "day" : "days"} ago`;
}
