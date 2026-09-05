"use client";

import * as React from "react";
import {
  AlertTriangle,
  Check,
  Copy,
  KeyRound,
  Loader2,
  Plug,
  Plus,
  RotateCcw,
  Trash2,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  ApiError,
  integrationsApi,
  type ApiKey,
  type ApiScope,
  type WebhookDelivery,
  type WebhookEndpoint,
  type WebhookEventType,
} from "@/lib/api";
import { cn } from "@/lib/utils";

export default function IntegrationsPage() {
  return (
    <PagePanel
      icon={Plug}
      title="Integrations"
      hint="Credentials for the systems that talk to this CRM, and the endpoints it talks back to. A key reaches the public API only — it can never open the CRM itself."
    >
      <Tabs defaultValue="keys" className="min-h-0 flex-1">
        <TabsList>
          <TabsTrigger value="keys">API keys</TabsTrigger>
          <TabsTrigger value="webhooks">Webhooks</TabsTrigger>
          <TabsTrigger value="deliveries">Delivery log</TabsTrigger>
        </TabsList>

        <TabsContent value="keys">
          <KeysPanel />
        </TabsContent>

        <TabsContent value="webhooks">
          <WebhooksPanel />
        </TabsContent>

        <TabsContent value="deliveries">
          <DeliveriesPanel />
        </TabsContent>
      </Tabs>
    </PagePanel>
  );
}

/* ------------------------------------------------------------------ *
 * API keys
 * ------------------------------------------------------------------ */

function KeysPanel() {
  const [keys, setKeys] = React.useState<ApiKey[] | null>(null);
  const [scopes, setScopes] = React.useState<ApiScope[]>([]);
  const [creating, setCreating] = React.useState(false);
  const [minted, setMinted] = React.useState<string | null>(null);

  const load = React.useCallback(() => {
    integrationsApi
      .keys()
      .then(setKeys)
      .catch((error: unknown) => {
        toast.error("Could not load API keys", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setKeys([]);
      });

    integrationsApi.scopes().then(setScopes).catch(() => setScopes([]));
  }, []);

  React.useEffect(load, [load]);

  async function revoke(key: ApiKey) {
    try {
      await integrationsApi.revokeKey(key.id);
      load();
      toast.success(`${key.name} revoked`);
    } catch (error) {
      toast.error("Could not revoke that key", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  if (keys === null) return <CrmLoadingState label="Loading API keys" />;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <p className="max-w-prose text-[12.5px] text-muted-foreground">
          A key authenticates a system rather than a person. Send it as an{" "}
          <code className="rounded bg-muted px-1 font-mono text-[11.5px]">X-Api-Key</code>{" "}
          header to{" "}
          <code className="rounded bg-muted px-1 font-mono text-[11.5px]">/api/v1</code>.
          It carries only the scopes you grant, and it is the whole of what a
          portal connector or a website form needs.
        </p>
        <Button size="sm" className="h-8" onClick={() => setCreating(true)}>
          <Plus /> New key
        </Button>
      </div>

      {keys.length === 0 ? (
        <p className="rounded-lg border border-dashed p-4 text-[12.5px] text-muted-foreground">
          No keys yet. Create one to let a portal feed, a website form or a
          reporting tool talk to this workspace.
        </p>
      ) : (
        <div className="overflow-x-auto rounded-lg border">
          <table className="w-full border-collapse text-[12.5px]">
            <thead className="bg-muted/60">
              <tr>
                <th className="border-b px-3 py-2 text-left font-medium">Key</th>
                <th className="border-b px-3 py-2 text-left font-medium">Scopes</th>
                <th className="border-b px-3 py-2 text-left font-medium">Last used</th>
                <th className="border-b px-3 py-2 text-right font-medium">Calls</th>
                <th className="border-b px-3 py-2" />
              </tr>
            </thead>
            <tbody>
              {keys.map((key) => (
                <tr key={key.id} className="border-b last:border-b-0">
                  <td className="px-3 py-2">
                    <p className="flex items-center gap-1.5 font-medium">
                      {key.name}
                      {key.isLive ? null : (
                        <Badge
                          variant="outline"
                          className="h-4 px-1 text-[9px] font-normal text-muted-foreground"
                        >
                          {key.revokedAt ? "Revoked" : "Expired"}
                        </Badge>
                      )}
                    </p>
                    <p className="font-mono text-[11px] text-muted-foreground">
                      {key.prefix}…
                    </p>
                  </td>
                  <td className="px-3 py-2">
                    <div className="flex flex-wrap gap-1">
                      {key.scopes.map((scope) => (
                        <Badge
                          key={scope}
                          variant="secondary"
                          className="h-4 px-1 font-mono text-[9.5px] font-normal"
                        >
                          {scope}
                        </Badge>
                      ))}
                    </div>
                  </td>
                  <td className="px-3 py-2 text-muted-foreground">
                    {key.lastUsedAt ? (
                      <>
                        {new Date(key.lastUsedAt).toLocaleString()}
                        {key.lastUsedIp ? (
                          <span className="block font-mono text-[10.5px]">
                            {key.lastUsedIp}
                          </span>
                        ) : null}
                      </>
                    ) : (
                      "Never"
                    )}
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {key.callCount.toLocaleString("en-IN")}
                  </td>
                  <td className="px-3 py-2 text-right">
                    {key.isLive ? (
                      <Button
                        size="sm"
                        variant="outline"
                        className="h-7"
                        onClick={() => void revoke(key)}
                      >
                        Revoke
                      </Button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <CreateKeyDialog
        open={creating}
        scopes={scopes}
        onOpenChange={setCreating}
        onCreated={(secret) => {
          setMinted(secret);
          load();
        }}
      />

      <SecretDialog
        secret={minted}
        title="Your new API key"
        description="This is the only time it will be shown. The server kept a hash, not the key — if it is lost, revoke it and make another."
        onClose={() => setMinted(null)}
      />
    </div>
  );
}

function CreateKeyDialog({
  open,
  scopes,
  onOpenChange,
  onCreated,
}: {
  open: boolean;
  scopes: ApiScope[];
  onOpenChange: (open: boolean) => void;
  onCreated: (secret: string) => void;
}) {
  const [name, setName] = React.useState("");
  const [granted, setGranted] = React.useState<string[]>([]);
  const [saving, setSaving] = React.useState(false);

  const [wasOpen, setWasOpen] = React.useState(open);
  if (open !== wasOpen) {
    setWasOpen(open);
    if (open) {
      setName("");
      setGranted([]);
    }
  }

  async function save() {
    setSaving(true);
    try {
      const minted = await integrationsApi.createKey({
        name,
        scopes: granted,
        expiresAt: null,
      });
      onCreated(minted.secret);
      onOpenChange(false);
    } catch (error) {
      toast.error("Could not create that key", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>New API key</DialogTitle>
          <DialogDescription>
            Grant only what this integration needs. A feed that pushes leads does
            not need to read your inventory.
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-4 py-4">
          <div className="space-y-1">
            <Label htmlFor="key-name">What is it for</Label>
            <Input
              id="key-name"
              value={name}
              placeholder="e.g. 99acres lead feed"
              onChange={(event) => setName(event.target.value)}
            />
          </div>

          <div className="space-y-1.5">
            <Label>Scopes</Label>
            <div className="flex flex-col gap-2 rounded-lg border p-3">
              {scopes.map((scope) => (
                <label
                  key={scope.scope}
                  className="flex items-start gap-2.5 text-[13px]"
                >
                  <Checkbox
                    className="mt-0.5"
                    checked={granted.includes(scope.scope)}
                    onCheckedChange={(value) =>
                      setGranted((current) =>
                        value === true
                          ? [...current, scope.scope]
                          : current.filter((s) => s !== scope.scope)
                      )
                    }
                  />
                  <span className="min-w-0">
                    <span className="block font-medium">{scope.label}</span>
                    <span className="block text-[11.5px] text-muted-foreground">
                      {scope.description}
                    </span>
                  </span>
                </label>
              ))}
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            disabled={saving || name.trim().length < 2 || granted.length === 0}
            onClick={() => void save()}
          >
            {saving ? <Loader2 className="animate-spin" /> : <KeyRound />}
            Create key
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/**
 * Shows a secret once and never again.
 *
 * Deliberately a blocking dialog with a copy button rather than a line in a
 * table: the value cannot be recovered, so the person creating it has to be
 * given a moment where taking it is the obvious next action.
 */
function SecretDialog({
  secret,
  title,
  description,
  onClose,
}: {
  secret: string | null;
  title: string;
  description: string;
  onClose: () => void;
}) {
  const [copied, setCopied] = React.useState(false);

  async function copy() {
    if (!secret) return;
    try {
      await navigator.clipboard.writeText(secret);
      setCopied(true);
      toast.success("Copied");
    } catch {
      toast.error("Could not copy — select it and copy by hand.");
    }
  }

  return (
    <Dialog open={secret !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <AlertTriangle className="size-4 text-amber-600 dark:text-amber-400" />
            {title}
          </DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <div className="flex gap-1.5 py-4">
          <Input readOnly value={secret ?? ""} className="font-mono text-[12.5px]" />
          <Button variant="outline" size="icon" aria-label="Copy" onClick={() => void copy()}>
            {copied ? <Check className="size-4" /> : <Copy className="size-4" />}
          </Button>
        </div>

        <DialogFooter>
          <Button onClick={onClose}>I have saved it</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/* ------------------------------------------------------------------ *
 * Webhooks
 * ------------------------------------------------------------------ */

function WebhooksPanel() {
  const [endpoints, setEndpoints] = React.useState<WebhookEndpoint[] | null>(null);
  const [events, setEvents] = React.useState<WebhookEventType[]>([]);
  const [creating, setCreating] = React.useState(false);
  const [minted, setMinted] = React.useState<string | null>(null);

  const load = React.useCallback(() => {
    integrationsApi
      .webhooks()
      .then(setEndpoints)
      .catch((error: unknown) => {
        toast.error("Could not load webhooks", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setEndpoints([]);
      });

    integrationsApi.events().then(setEvents).catch(() => setEvents([]));
  }, []);

  React.useEffect(load, [load]);

  async function toggle(endpoint: WebhookEndpoint, isActive: boolean) {
    try {
      await integrationsApi.updateWebhook(endpoint.id, {
        name: endpoint.name,
        url: endpoint.url,
        events: endpoint.events,
        isActive,
      });
      load();
    } catch (error) {
      toast.error("Could not change that endpoint", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  async function remove(endpoint: WebhookEndpoint) {
    try {
      await integrationsApi.deleteWebhook(endpoint.id);
      load();
      toast.success(`${endpoint.name} removed`);
    } catch (error) {
      toast.error("Could not remove that endpoint", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  if (endpoints === null) return <CrmLoadingState label="Loading webhooks" />;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <p className="max-w-prose text-[12.5px] text-muted-foreground">
          Each delivery is signed with the endpoint&apos;s secret over{" "}
          <code className="rounded bg-muted px-1 font-mono text-[11.5px]">
            {"{timestamp}.{body}"}
          </code>{" "}
          and sent as{" "}
          <code className="rounded bg-muted px-1 font-mono text-[11.5px]">
            X-BullEvents-Signature
          </code>
          . Verify it before trusting the payload, and reject a timestamp that is
          not recent.
        </p>
        <Button size="sm" className="h-8" onClick={() => setCreating(true)}>
          <Plus /> New endpoint
        </Button>
      </div>

      {endpoints.length === 0 ? (
        <p className="rounded-lg border border-dashed p-4 text-[12.5px] text-muted-foreground">
          Nothing is being notified yet.
        </p>
      ) : (
        <div className="flex flex-col gap-2">
          {endpoints.map((endpoint) => (
            <div key={endpoint.id} className="rounded-lg border p-3">
              <div className="flex flex-wrap items-center gap-2">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-[13px] font-medium">{endpoint.name}</p>
                  <p className="truncate font-mono text-[11px] text-muted-foreground">
                    {endpoint.url}
                  </p>
                </div>

                <Switch
                  aria-label={`${endpoint.name} active`}
                  checked={endpoint.isActive}
                  onCheckedChange={(value) => void toggle(endpoint, value)}
                />

                <Button
                  size="icon"
                  variant="ghost"
                  aria-label={`Remove ${endpoint.name}`}
                  className="size-7 text-muted-foreground"
                  onClick={() => void remove(endpoint)}
                >
                  <Trash2 className="size-3.5" />
                </Button>
              </div>

              <div className="mt-2 flex flex-wrap gap-1">
                {endpoint.events.map((event) => (
                  <Badge
                    key={event}
                    variant="secondary"
                    className="h-4 px-1 font-mono text-[9.5px] font-normal"
                  >
                    {event}
                  </Badge>
                ))}
              </div>

              {endpoint.disabledReason ? (
                <p className="mt-2 flex items-start gap-1.5 text-[11.5px] text-amber-600 dark:text-amber-400">
                  <AlertTriangle className="mt-0.5 size-3 shrink-0" />
                  {endpoint.disabledReason}
                </p>
              ) : endpoint.consecutiveFailures > 0 ? (
                <p className="mt-2 text-[11.5px] text-muted-foreground">
                  {endpoint.consecutiveFailures} failed{" "}
                  {endpoint.consecutiveFailures === 1 ? "delivery" : "deliveries"} in
                  a row.
                </p>
              ) : null}
            </div>
          ))}
        </div>
      )}

      <CreateWebhookDialog
        open={creating}
        events={events}
        onOpenChange={setCreating}
        onCreated={(secret) => {
          setMinted(secret);
          load();
        }}
      />

      <SecretDialog
        secret={minted}
        title="Your signing secret"
        description="Configure this on the receiving end so it can verify each delivery. Like an API key, it is shown once."
        onClose={() => setMinted(null)}
      />
    </div>
  );
}

function CreateWebhookDialog({
  open,
  events,
  onOpenChange,
  onCreated,
}: {
  open: boolean;
  events: WebhookEventType[];
  onOpenChange: (open: boolean) => void;
  onCreated: (secret: string) => void;
}) {
  const [name, setName] = React.useState("");
  const [url, setUrl] = React.useState("");
  const [chosen, setChosen] = React.useState<string[]>([]);
  const [saving, setSaving] = React.useState(false);

  const [wasOpen, setWasOpen] = React.useState(open);
  if (open !== wasOpen) {
    setWasOpen(open);
    if (open) {
      setName("");
      setUrl("");
      setChosen([]);
    }
  }

  async function save() {
    setSaving(true);
    try {
      const created = await integrationsApi.createWebhook({
        name,
        url,
        events: chosen,
        isActive: true,
      });
      if (created.secret) onCreated(created.secret);
      onOpenChange(false);
    } catch (error) {
      toast.error("Could not create that endpoint", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>New webhook endpoint</DialogTitle>
          <DialogDescription>
            Where to post the events you choose. Must be https — the payload
            carries customer data.
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-4 py-4">
          <div className="space-y-1">
            <Label htmlFor="hook-name">Name</Label>
            <Input
              id="hook-name"
              value={name}
              placeholder="e.g. Marketing automation"
              onChange={(event) => setName(event.target.value)}
            />
          </div>

          <div className="space-y-1">
            <Label htmlFor="hook-url">URL</Label>
            <Input
              id="hook-url"
              value={url}
              placeholder="https://hooks.example.com/bull-events"
              className="font-mono text-[12.5px]"
              onChange={(event) => setUrl(event.target.value)}
            />
          </div>

          <div className="space-y-1.5">
            <Label>Events</Label>
            <div className="flex flex-col gap-2 rounded-lg border p-3">
              {events.map((event) => (
                <label key={event.event} className="flex items-start gap-2.5 text-[13px]">
                  <Checkbox
                    className="mt-0.5"
                    checked={chosen.includes(event.event)}
                    onCheckedChange={(value) =>
                      setChosen((current) =>
                        value === true
                          ? [...current, event.event]
                          : current.filter((e) => e !== event.event)
                      )
                    }
                  />
                  <span className="min-w-0">
                    <span className="block font-mono text-[12px] font-medium">
                      {event.event}
                    </span>
                    <span className="block text-[11.5px] text-muted-foreground">
                      {event.description}
                    </span>
                  </span>
                </label>
              ))}
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            disabled={saving || name.trim().length < 2 || url.trim().length === 0 || chosen.length === 0}
            onClick={() => void save()}
          >
            {saving ? <Loader2 className="animate-spin" /> : <Check />}
            Create endpoint
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/* ------------------------------------------------------------------ *
 * Delivery log
 * ------------------------------------------------------------------ */

function DeliveriesPanel() {
  const [rows, setRows] = React.useState<WebhookDelivery[] | null>(null);
  const [status, setStatus] = React.useState<string>("");

  const load = React.useCallback(() => {
    integrationsApi
      .deliveries(undefined, status || undefined)
      .then(setRows)
      .catch((error: unknown) => {
        toast.error("Could not load the delivery log", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setRows([]);
      });
  }, [status]);

  React.useEffect(load, [load]);

  async function replay(delivery: WebhookDelivery) {
    try {
      await integrationsApi.replay(delivery.id);
      load();
      toast.success("Queued for another attempt");
    } catch (error) {
      toast.error("Could not replay that delivery", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  if (rows === null) return <CrmLoadingState label="Loading deliveries" />;

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-1.5">
        {["", "Pending", "Delivered", "Failed"].map((option) => (
          <Button
            key={option || "all"}
            size="sm"
            variant={status === option ? "secondary" : "ghost"}
            className="h-7"
            onClick={() => setStatus(option)}
          >
            {option || "All"}
          </Button>
        ))}
        <span className="ml-auto text-[12px] text-muted-foreground tabular-nums">
          {rows.length} shown
        </span>
      </div>

      {rows.length === 0 ? (
        <p className="rounded-lg border border-dashed p-4 text-[12.5px] text-muted-foreground">
          Nothing here yet. Deliveries appear as soon as an event fires for an
          endpoint that asked for it.
        </p>
      ) : (
        <div className="overflow-x-auto rounded-lg border">
          <table className="w-full border-collapse text-[12.5px]">
            <thead className="bg-muted/60">
              <tr>
                <th className="border-b px-3 py-2 text-left font-medium">Event</th>
                <th className="border-b px-3 py-2 text-left font-medium">Endpoint</th>
                <th className="border-b px-3 py-2 text-left font-medium">Status</th>
                <th className="border-b px-3 py-2 text-right font-medium">Tries</th>
                <th className="border-b px-3 py-2 text-left font-medium">When</th>
                <th className="border-b px-3 py-2" />
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id} className="border-b last:border-b-0">
                  <td className="px-3 py-2 font-mono text-[11.5px]">{row.event}</td>
                  <td className="px-3 py-2">{row.endpointName}</td>
                  <td className="px-3 py-2">
                    <Badge
                      variant="outline"
                      className={cn(
                        "h-5 px-1.5 text-[11px] font-normal",
                        row.status === "Delivered"
                          ? "text-emerald-600 dark:text-emerald-400"
                          : row.status === "Failed"
                            ? "text-rose-600 dark:text-rose-400"
                            : "text-muted-foreground"
                      )}
                    >
                      {row.status}
                      {row.responseCode ? ` · ${row.responseCode}` : ""}
                    </Badge>
                    {row.error ? (
                      <p className="mt-0.5 max-w-xs truncate text-[11px] text-muted-foreground">
                        {row.error}
                      </p>
                    ) : null}
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">{row.attempts}</td>
                  <td className="px-3 py-2 text-muted-foreground">
                    {new Date(row.createdAt).toLocaleString()}
                  </td>
                  <td className="px-3 py-2 text-right">
                    {row.status === "Failed" ? (
                      <Button
                        size="sm"
                        variant="outline"
                        className="h-7"
                        onClick={() => void replay(row)}
                      >
                        <RotateCcw /> Replay
                      </Button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <p className="text-[11px] text-muted-foreground">
        A replay re-sends the payload exactly as it was captured, not a rebuilt
        copy — the point is to deliver what the receiver missed, not today&apos;s
        version of a record that has since changed.
      </p>
    </div>
  );
}
