"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  ArrowLeft,
  Check,
  Copy,
  FlaskConical,
  Loader2,
  Plug,
  RefreshCw,
  Trash2,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  ApiError,
  connectorsApi,
  getBranches,
  getUsers,
  type Branch,
  type Connector,
  type ConnectorDefinition,
  type ConnectorEvent,
  type ConnectorTestResult,
  type UserListItem,
} from "@/lib/api";
import { cn } from "@/lib/utils";

/** The sentinel a picker uses for "leave it unset". */
const NONE = "none";

export default function ConnectorDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const id = Number(params.id);

  const [connector, setConnector] = React.useState<Connector | null>(null);
  const [definition, setDefinition] = React.useState<ConnectorDefinition | null>(null);
  const [branches, setBranches] = React.useState<Branch[]>([]);
  const [people, setPeople] = React.useState<UserListItem[]>([]);

  const load = React.useCallback(() => {
    connectorsApi
      .get(id)
      .then(async (found) => {
        setConnector(found);

        const catalogue = await connectorsApi.catalogue();
        setDefinition(
          catalogue.find((d) => d.provider === found.provider) ?? null
        );
      })
      .catch((error: unknown) => {
        toast.error("Could not load that connection", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
      });

    getBranches().then(setBranches).catch(() => setBranches([]));
    getUsers().then(setPeople).catch(() => setPeople([]));
  }, [id]);

  React.useEffect(load, [load]);

  async function remove() {
    if (!connector) return;

    try {
      await connectorsApi.remove(connector.id);
      toast.success(`${connector.name} removed`);
      router.push("/dashboard/setup/connectors");
    } catch (error) {
      toast.error("Could not remove that connection", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  if (!connector || !definition) {
    return (
      <PagePanel icon={Plug} title="Connection">
        <CrmLoadingState label="Loading connection" />
      </PagePanel>
    );
  }

  const tone =
    connector.status === "Connected" && connector.isActive
      ? "text-emerald-600 dark:text-emerald-400"
      : connector.status === "Error"
        ? "text-rose-600 dark:text-rose-400"
        : "text-muted-foreground";

  return (
    <PagePanel
      icon={Plug}
      title={connector.name}
      hint={definition.description}
      actions={
        <>
          <Badge
            variant="outline"
            className={cn("h-6 gap-1.5 px-2 text-[11px] font-normal", tone)}
          >
            <span className="size-1.5 rounded-full bg-current" />
            {!connector.isActive
              ? "Switched off"
              : connector.status === "NotConfigured"
                ? "Needs setup"
                : connector.status}
          </Badge>

          <Button size="sm" variant="outline" className="h-8" asChild>
            <Link href="/dashboard/setup/connectors">
              <ArrowLeft /> All connectors
            </Link>
          </Button>

          <Button
            size="icon"
            variant="ghost"
            aria-label="Remove this connection"
            title="Remove this connection"
            className="size-8 text-muted-foreground"
            onClick={() => void remove()}
          >
            <Trash2 className="size-4" />
          </Button>
        </>
      }
    >
      {connector.lastError ? (
        <div className="mb-4 rounded-lg border border-l-[3px] border-l-rose-500 bg-muted/40 px-4 py-3">
          <p className="text-[12.5px]">
            <b>Last failure.</b> {connector.lastError}
          </p>
        </div>
      ) : null}

      <Tabs defaultValue="setup" className="min-h-0 flex-1">
        <TabsList>
          <TabsTrigger value="setup">Setup</TabsTrigger>
          <TabsTrigger value="settings">Where leads land</TabsTrigger>
          <TabsTrigger value="activity">Activity</TabsTrigger>
        </TabsList>

        <TabsContent value="setup">
          <SetupTab connector={connector} definition={definition} onSaved={setConnector} />
        </TabsContent>

        <TabsContent value="settings">
          <SettingsTab
            connector={connector}
            branches={branches}
            people={people}
            onSaved={setConnector}
          />
        </TabsContent>

        <TabsContent value="activity">
          <ActivityTab connector={connector} />
        </TabsContent>
      </Tabs>
    </PagePanel>
  );
}

/* ------------------------------------------------------------------ *
 * Setup — the endpoint, the credentials, the steps
 * ------------------------------------------------------------------ */

function SetupTab({
  connector,
  definition,
  onSaved,
}: {
  connector: Connector;
  definition: ConnectorDefinition;
  onSaved: (next: Connector) => void;
}) {
  const [draft, setDraft] = React.useState<Record<string, string>>({});
  const [saving, setSaving] = React.useState(false);

  async function save() {
    setSaving(true);
    try {
      const next = await connectorsApi.update(connector.id, {
        provider: connector.provider,
        name: connector.name,
        isActive: connector.isActive,
        defaultBranchId: connector.defaultBranchId,
        defaultOwnerId: connector.defaultOwnerId,
        sourceLabel: connector.sourceLabel,
        credentials: draft,
      });

      onSaved(next);
      setDraft({});
      toast.success("Credentials saved");
    } catch (error) {
      toast.error("Could not save those credentials", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  async function rotate() {
    try {
      onSaved(await connectorsApi.rotate(connector.id));
      toast.success("A new endpoint was issued", {
        description: "Paste it into the provider — the old URL stops working now.",
      });
    } catch (error) {
      toast.error("Could not rotate the endpoint", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  return (
    <div className="flex max-w-3xl flex-col gap-6">
      {connector.inboundUrl ? (
        <section className="flex flex-col gap-2">
          <h3 className="text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
            Endpoint
          </h3>
          <p className="text-[12.5px] text-muted-foreground">
            Paste this into {definition.name}. It is the credential as well as
            the address, so treat it like a password — anyone holding it can post
            leads into this workspace.
          </p>

          <div className="flex gap-1.5">
            <Input
              readOnly
              value={connector.inboundUrl}
              className="font-mono text-[12px]"
              onFocus={(event) => event.currentTarget.select()}
            />
            <CopyButton value={connector.inboundUrl} />
            <Button
              variant="outline"
              size="icon"
              aria-label="Issue a new endpoint"
              title="Issue a new endpoint"
              onClick={() => void rotate()}
            >
              <RefreshCw className="size-4" />
            </Button>
          </div>
        </section>
      ) : null}

      <section className="flex flex-col gap-2">
        <h3 className="text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
          On {definition.name}&rsquo;s side
        </h3>
        <ol className="flex list-decimal flex-col gap-1 pl-5">
          {definition.setupSteps.map((step) => (
            <li key={step} className="text-[12.5px]">
              {step}
            </li>
          ))}
        </ol>
      </section>

      {definition.credentials.length > 0 ? (
        <section className="flex flex-col gap-3">
          <div>
            <h3 className="text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
              Credentials
            </h3>
            <p className="text-[12.5px] text-muted-foreground">
              A field already set shows as stored rather than as its value — the
              server keeps it, and a screen that reprints a secret leaks it to
              whoever is watching.
            </p>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            {definition.credentials.map((field) => {
              const stored = connector.credentialsSet.includes(field.key);

              return (
                <div key={field.key} className="space-y-1">
                  <Label htmlFor={`cred-${field.key}`}>
                    {field.label}
                    {field.required ? null : (
                      <span className="ml-1 text-[11px] font-normal text-muted-foreground">
                        optional
                      </span>
                    )}
                  </Label>
                  <Input
                    id={`cred-${field.key}`}
                    type={field.secret ? "password" : "text"}
                    autoComplete="off"
                    value={draft[field.key] ?? ""}
                    placeholder={stored ? "•••••••• stored" : ""}
                    onChange={(event) =>
                      setDraft({ ...draft, [field.key]: event.target.value })
                    }
                  />
                  <p className="text-[11px] text-muted-foreground">{field.help}</p>
                </div>
              );
            })}
          </div>

          <div>
            <Button
              disabled={saving || Object.keys(draft).length === 0}
              onClick={() => void save()}
            >
              {saving ? <Loader2 className="animate-spin" /> : <Check />}
              Save credentials
            </Button>
          </div>
        </section>
      ) : null}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Settings — where what arrives ends up
 * ------------------------------------------------------------------ */

function SettingsTab({
  connector,
  branches,
  people,
  onSaved,
}: {
  connector: Connector;
  branches: Branch[];
  people: UserListItem[];
  onSaved: (next: Connector) => void;
}) {
  const [name, setName] = React.useState(connector.name);
  const [active, setActive] = React.useState(connector.isActive);
  const [branch, setBranch] = React.useState(
    connector.defaultBranchId ? String(connector.defaultBranchId) : NONE
  );
  const [owner, setOwner] = React.useState(
    connector.defaultOwnerId ? String(connector.defaultOwnerId) : NONE
  );
  const [source, setSource] = React.useState(connector.sourceLabel ?? "");
  const [saving, setSaving] = React.useState(false);

  async function save() {
    setSaving(true);
    try {
      const next = await connectorsApi.update(connector.id, {
        provider: connector.provider,
        name,
        isActive: active,
        defaultBranchId: branch === NONE ? null : Number(branch),
        defaultOwnerId: owner === NONE ? null : Number(owner),
        sourceLabel: source.trim() || null,
      });

      onSaved(next);
      toast.success("Settings saved");
    } catch (error) {
      toast.error("Could not save those settings", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <div className="space-y-1">
        <Label htmlFor="connector-rename">Name</Label>
        <Input
          id="connector-rename"
          value={name}
          onChange={(event) => setName(event.target.value)}
        />
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1">
          <Label>Branch</Label>
          <Select value={branch} onValueChange={setBranch}>
            <SelectTrigger className="w-full">
              <SelectValue placeholder="The company's first branch" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>
                <span className="text-muted-foreground">First branch</span>
              </SelectItem>
              {branches.map((b) => (
                <SelectItem key={b.id} value={String(b.id)}>
                  {b.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <p className="text-[11px] text-muted-foreground">
            Which office these leads belong to.
          </p>
        </div>

        <div className="space-y-1">
          <Label>Owner</Label>
          <Select value={owner} onValueChange={setOwner}>
            <SelectTrigger className="w-full">
              <SelectValue placeholder="Leave unassigned" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>
                <span className="text-muted-foreground">Leave unassigned</span>
              </SelectItem>
              {people
                .filter((p) => p.isActive)
                .map((p) => (
                  <SelectItem key={p.id} value={String(p.id)}>
                    {p.name}
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>
          <p className="text-[11px] text-muted-foreground">
            Unassigned leads arrive with a one-hour response clock and nobody
            watching. Pick somebody until assignment rules exist.
          </p>
        </div>
      </div>

      <div className="space-y-1">
        <Label htmlFor="connector-source">Record the source as</Label>
        <Input
          id="connector-source"
          value={source}
          placeholder="e.g. 99acres"
          onChange={(event) => setSource(event.target.value)}
        />
        <p className="text-[11px] text-muted-foreground">
          Set here rather than taken from the payload — a portal sends whatever
          it sends, and the source report fills with three spellings of one name.
        </p>
      </div>

      <div className="flex items-center justify-between rounded-lg border p-3">
        <div>
          <Label htmlFor="connector-active">Accepting deliveries</Label>
          <p className="text-xs text-muted-foreground">
            Switched off, deliveries are logged and refused rather than dropped
            silently.
          </p>
        </div>
        <Switch id="connector-active" checked={active} onCheckedChange={setActive} />
      </div>

      <div>
        <Button disabled={saving} onClick={() => void save()}>
          {saving ? <Loader2 className="animate-spin" /> : <Check />}
          Save settings
        </Button>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Activity
 * ------------------------------------------------------------------ */

function ActivityTab({ connector }: { connector: Connector }) {
  const [events, setEvents] = React.useState<ConnectorEvent[] | null>(null);
  const [outcome, setOutcome] = React.useState("");
  const [test, setTest] = React.useState<ConnectorTestResult | null>(null);
  const [testing, setTesting] = React.useState(false);
  const [expanded, setExpanded] = React.useState<number | null>(null);

  const load = React.useCallback(() => {
    connectorsApi
      .events(connector.id, outcome || undefined)
      .then(setEvents)
      .catch(() => setEvents([]));
  }, [connector.id, outcome]);

  React.useEffect(load, [load]);

  async function runTest() {
    setTesting(true);
    try {
      setTest(await connectorsApi.test(connector.id));
      load();
      toast.success("Mapping test run", {
        description: "No lead was created — this shows how a real delivery would land.",
      });
    } catch (error) {
      toast.error("Could not run the test", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setTesting(false);
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-1.5">
        {["", "Created", "Duplicate", "Rejected", "Failed"].map((option) => (
          <Button
            key={option || "all"}
            size="sm"
            variant={outcome === option ? "secondary" : "ghost"}
            className="h-7"
            onClick={() => setOutcome(option)}
          >
            {option || "All"}
          </Button>
        ))}

        <Button
          size="sm"
          variant="outline"
          className="ml-auto h-7"
          disabled={testing}
          onClick={() => void runTest()}
        >
          {testing ? <Loader2 className="animate-spin" /> : <FlaskConical />}
          Test the mapping
        </Button>
      </div>

      {test ? (
        <div className="grid gap-3 rounded-lg border p-3 lg:grid-cols-2">
          <div className="min-w-0">
            <p className="text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
              A delivery like {connector.providerName} sends
            </p>
            <pre className="mt-1.5 overflow-x-auto rounded bg-muted/50 p-2.5 font-mono text-[11px]">
              {JSON.stringify(test.sample, null, 2)}
            </pre>
          </div>
          <div className="min-w-0">
            <p className="text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
              Becomes this lead
            </p>
            <pre className="mt-1.5 overflow-x-auto rounded bg-muted/50 p-2.5 font-mono text-[11px]">
              {JSON.stringify(test.mapped, null, 2)}
            </pre>
          </div>
        </div>
      ) : null}

      {events === null ? (
        <CrmLoadingState label="Loading activity" />
      ) : events.length === 0 ? (
        <p className="rounded-lg border border-dashed p-4 text-[12.5px] text-muted-foreground">
          Nothing has come through yet. Paste the endpoint into{" "}
          {connector.providerName} and send a test enquiry — every delivery shows
          here, including the ones that were refused and why.
        </p>
      ) : (
        <div className="overflow-hidden rounded-lg border">
          {events.map((event) => (
            <div key={event.id} className="border-b last:border-b-0">
              <button
                type="button"
                onClick={() => setExpanded(expanded === event.id ? null : event.id)}
                className="flex w-full flex-wrap items-center gap-2.5 px-3 py-2 text-left hover:bg-accent/40"
              >
                <Badge
                  variant="outline"
                  className={cn(
                    "h-5 shrink-0 px-1.5 text-[11px] font-normal",
                    event.outcome === "Created"
                      ? "text-emerald-600 dark:text-emerald-400"
                      : event.outcome === "Failed" || event.outcome === "Rejected"
                        ? "text-rose-600 dark:text-rose-400"
                        : "text-muted-foreground"
                  )}
                >
                  {event.outcome}
                </Badge>

                <span className="min-w-0 flex-1 truncate text-[12.5px]">
                  {event.detail ?? event.kind}
                </span>

                {event.leadId ? (
                  <Link
                    href={`/dashboard/leads/${event.leadId}`}
                    className="shrink-0 text-[12px] text-primary hover:underline"
                    onClick={(clickEvent) => clickEvent.stopPropagation()}
                  >
                    Lead #{event.leadId}
                  </Link>
                ) : null}

                <span className="shrink-0 text-[11.5px] text-muted-foreground tabular-nums">
                  {new Date(event.at).toLocaleString()}
                </span>
              </button>

              {expanded === event.id ? (
                <div className="border-t bg-muted/30 px-3 py-2">
                  <p className="mb-1.5 text-[10.5px] tracking-widest text-muted-foreground uppercase">
                    Exactly what arrived{event.ipAddress ? ` · from ${event.ipAddress}` : ""}
                  </p>
                  <pre className="overflow-x-auto font-mono text-[11px]">
                    {pretty(event.payload)}
                  </pre>
                </div>
              ) : null}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Bits
 * ------------------------------------------------------------------ */

function CopyButton({ value }: { value: string }) {
  const [copied, setCopied] = React.useState(false);

  async function copy() {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      toast.success("Copied");
    } catch {
      toast.error("Could not copy — select it and copy by hand.");
    }
  }

  return (
    <Button variant="outline" size="icon" aria-label="Copy" onClick={() => void copy()}>
      {copied ? <Check className="size-4" /> : <Copy className="size-4" />}
    </Button>
  );
}

/** Pretty-prints a stored payload, falling back to the raw text if it is not JSON. */
function pretty(payload: string) {
  try {
    return JSON.stringify(JSON.parse(payload), null, 2);
  } catch {
    return payload;
  }
}
