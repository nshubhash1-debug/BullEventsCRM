"use client";

import * as React from "react";
import { ArrowRight, History, Search } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  ApiError,
  getAuditTrail,
  type AuditEntry,
  type AuditPage,
} from "@/lib/api";
import { cn } from "@/lib/utils";

/** The sentinel a filter uses for "no filter". */
const ANY = "any";

const ACTION_TONE: Record<string, string> = {
  Create: "text-emerald-600 dark:text-emerald-400",
  Update: "text-sky-600 dark:text-sky-400",
  Delete: "text-rose-600 dark:text-rose-400",
};

/**
 * The change trail, read.
 *
 * The trail has been written on every save since the audit interceptor went in,
 * and nothing read it — "who changed this lead's owner" was a SQL query, which
 * is not an answer a compliance reviewer or an argument between two reps can
 * use.
 */
export default function AuditPage() {
  const [data, setData] = React.useState<AuditPage | null>(null);
  const [entity, setEntity] = React.useState(ANY);
  const [actor, setActor] = React.useState(ANY);
  const [action, setAction] = React.useState(ANY);
  const [recordId, setRecordId] = React.useState("");
  const [page, setPage] = React.useState(1);

  const load = React.useCallback(() => {
    getAuditTrail({
      entity: entity === ANY ? undefined : entity,
      actor: actor === ANY ? undefined : actor,
      action: action === ANY ? undefined : action,
      entityId: recordId.trim() || undefined,
      page,
      pageSize: 50,
    })
      .then(setData)
      .catch((error: unknown) => {
        toast.error("Could not load the audit trail", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
      });
  }, [entity, actor, action, recordId, page]);

  React.useEffect(load, [load]);

  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <PagePanel
      flush
      icon={History}
      title="Audit trail"
      hint="Written by an interceptor on every save, so nothing can be changed without leaving a line here. There is no write path into this screen — an audit trail somebody can edit is not one."
      toolbar={
        <>
          <Select
            value={entity}
            onValueChange={(value) => {
              setEntity(value);
              setPage(1);
            }}
          >
            <SelectTrigger className="h-8 w-44 text-[12.5px]">
              <SelectValue placeholder="Any record type" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>Any record type</SelectItem>
              {(data?.entities ?? []).map((name) => (
                <SelectItem key={name} value={name}>
                  {name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select
            value={actor}
            onValueChange={(value) => {
              setActor(value);
              setPage(1);
            }}
          >
            <SelectTrigger className="h-8 w-44 text-[12.5px]">
              <SelectValue placeholder="Anybody" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>Anybody</SelectItem>
              {(data?.actors ?? []).map((name) => (
                <SelectItem key={name} value={name}>
                  {name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select
            value={action}
            onValueChange={(value) => {
              setAction(value);
              setPage(1);
            }}
          >
            <SelectTrigger className="h-8 w-36 text-[12.5px]">
              <SelectValue placeholder="Any action" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>Any action</SelectItem>
              <SelectItem value="Create">Created</SelectItem>
              <SelectItem value="Update">Updated</SelectItem>
              <SelectItem value="Delete">Deleted</SelectItem>
            </SelectContent>
          </Select>

          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={recordId}
              placeholder="Record id"
              className="h-8 w-32 pl-8 text-[13px]"
              onChange={(event) => {
                setRecordId(event.target.value);
                setPage(1);
              }}
            />
          </div>

          <span className="text-[12px] text-muted-foreground tabular-nums">
            {data ? `${data.total.toLocaleString("en-IN")} entries` : "Loading…"}
          </span>
        </>
      }
    >
      {data === null ? (
        <div className="p-5">
          <CrmLoadingState label="Loading the trail" />
        </div>
      ) : data.items.length === 0 ? (
        <p className="p-5 text-[12.5px] text-muted-foreground">
          Nothing matches those filters.
        </p>
      ) : (
        <div className="flex flex-col">
          {data.items.map((entry) => (
            <Entry key={entry.id} entry={entry} />
          ))}

          {pages > 1 ? (
            <div className="flex items-center justify-between gap-2 border-t px-5 py-3">
              <span className="text-[12px] text-muted-foreground tabular-nums">
                Page {data.page} of {pages}
              </span>
              <div className="flex gap-1.5">
                <Button
                  size="sm"
                  variant="outline"
                  className="h-7"
                  disabled={data.page <= 1}
                  onClick={() => setPage((p) => p - 1)}
                >
                  Previous
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  className="h-7"
                  disabled={data.page >= pages}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          ) : null}
        </div>
      )}
    </PagePanel>
  );
}

function Entry({ entry }: { entry: AuditEntry }) {
  const [open, setOpen] = React.useState(false);

  const summary = entry.changes.slice(0, 4).map((c) => c.field).join(", ");
  const more = Math.max(0, entry.changes.length - 4);

  return (
    <div className="border-b px-5 py-2.5 last:border-b-0">
      <div className="flex flex-wrap items-center gap-2.5">
        <Avatar className="size-6 shrink-0">
          <AvatarFallback className="bg-primary/10 text-[9px] text-primary">
            {initials(entry.userName)}
          </AvatarFallback>
        </Avatar>

        <span className="text-[12.5px] font-medium">{entry.userName}</span>

        <Badge
          variant="outline"
          className={cn(
            "h-5 px-1.5 text-[11px] font-normal",
            ACTION_TONE[entry.action] ?? "text-muted-foreground"
          )}
        >
          {entry.action}
        </Badge>

        <span className="font-mono text-[12px] text-muted-foreground">
          {entry.entity}#{entry.entityId}
        </span>

        {summary ? (
          <button
            type="button"
            onClick={() => setOpen((value) => !value)}
            className="min-w-0 flex-1 truncate text-left text-[12px] text-muted-foreground hover:text-foreground"
          >
            {summary}
            {more > 0 ? ` +${more} more` : ""}
          </button>
        ) : (
          <span className="flex-1" />
        )}

        <span className="shrink-0 text-[11.5px] text-muted-foreground tabular-nums">
          {new Date(entry.at).toLocaleString()}
        </span>
      </div>

      {open && entry.changes.length > 0 ? (
        <div className="mt-2 ml-8 overflow-x-auto rounded-md border bg-muted/30">
          <table className="w-full border-collapse text-[12px]">
            <tbody>
              {entry.changes.map((change) => (
                <tr key={change.field} className="border-b last:border-b-0">
                  <td className="px-3 py-1.5 font-medium whitespace-nowrap">
                    {change.field}
                  </td>
                  <td className="px-3 py-1.5 text-muted-foreground">
                    {change.from ?? "—"}
                  </td>
                  <td className="w-4 px-1 py-1.5 text-muted-foreground">
                    <ArrowRight className="size-3" />
                  </td>
                  <td className="px-3 py-1.5">{change.to ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {entry.ipAddress ? (
            <p className="border-t px-3 py-1.5 font-mono text-[11px] text-muted-foreground">
              from {entry.ipAddress}
            </p>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

function initials(name: string) {
  return name
    .split(" ")
    .map((part) => part[0])
    .slice(0, 2)
    .join("")
    .toUpperCase();
}
