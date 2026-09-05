"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  ArrowDownToLine,
  ArrowLeftRight,
  ArrowUpFromLine,
  Check,
  Loader2,
  Plug,
  Plus,
} from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { CrmLoadingState } from "@/components/shell/crm-loader";
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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  ApiError,
  CONNECTOR_CATEGORIES,
  connectorsApi,
  type Connector,
  type ConnectorDefinition,
} from "@/lib/api";
import { cn } from "@/lib/utils";

const DIRECTION: Record<string, { label: string; icon: typeof ArrowDownToLine }> = {
  Inbound: { label: "They send to us", icon: ArrowDownToLine },
  Outbound: { label: "We send to them", icon: ArrowUpFromLine },
  Both: { label: "Both ways", icon: ArrowLeftRight },
};

/**
 * The connector catalogue.
 *
 * Laid out as a shop rather than a settings list: an administrator arrives
 * wanting "our 99acres leads in the CRM", not wanting to fill in a form. So the
 * card leads with what connecting it does, and the configuration is a step
 * after choosing — the same order Salesforce's AppExchange and Connected Apps
 * put it in.
 */
export default function ConnectorsPage() {
  const [catalogue, setCatalogue] = React.useState<ConnectorDefinition[] | null>(null);
  const [connectors, setConnectors] = React.useState<Connector[]>([]);
  const [adding, setAdding] = React.useState<ConnectorDefinition | null>(null);

  const load = React.useCallback(() => {
    connectorsApi
      .catalogue()
      .then(setCatalogue)
      .catch((error: unknown) => {
        toast.error("Could not load the connector catalogue", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
        setCatalogue([]);
      });

    connectorsApi.list().then(setConnectors).catch(() => setConnectors([]));
  }, []);

  React.useEffect(load, [load]);

  const live = connectors.filter((c) => c.status === "Connected" && c.isActive).length;
  const broken = connectors.filter((c) => c.status === "Error").length;

  return (
    <PagePanel
      icon={Plug}
      title="Connectors"
      hint="Every outside system this workspace talks to. Each connection gets its own endpoint, its own defaults and its own activity log, so a portal that stops sending is visible here rather than as a quiet gap in the pipeline."
      actions={
        connectors.length > 0 ? (
          <span className="text-[12px] text-muted-foreground tabular-nums">
            {live} connected
            {broken > 0 ? ` · ${broken} in error` : ""}
          </span>
        ) : null
      }
    >
      {catalogue === null ? (
        <CrmLoadingState label="Loading connectors" />
      ) : (
        <Tabs defaultValue="connected" className="min-h-0 flex-1">
          <TabsList>
            <TabsTrigger value="connected">
              Your connections{connectors.length > 0 ? ` (${connectors.length})` : ""}
            </TabsTrigger>
            <TabsTrigger value="catalogue">Add a connection</TabsTrigger>
          </TabsList>

          <TabsContent value="connected">
            {connectors.length === 0 ? (
              <p className="rounded-lg border border-dashed p-5 text-[12.5px] text-muted-foreground">
                Nothing is connected yet. Open <b>Add a connection</b> to wire up a
                portal feed, your telephony, or a form on your own website.
              </p>
            ) : (
              <div className="grid gap-3 lg:grid-cols-2">
                {connectors.map((connector) => (
                  <ConnectionCard key={connector.id} connector={connector} />
                ))}
              </div>
            )}
          </TabsContent>

          <TabsContent value="catalogue">
            <div className="flex flex-col gap-6">
              {CONNECTOR_CATEGORIES.map((category) => {
                const inCategory = catalogue.filter((d) => d.category === category);
                if (inCategory.length === 0) return null;

                return (
                  <section key={category} className="flex flex-col gap-2.5">
                    <h2 className="text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
                      {category}
                    </h2>

                    <div className="grid gap-3 lg:grid-cols-2">
                      {inCategory.map((definition) => (
                        <CatalogueCard
                          key={definition.provider}
                          definition={definition}
                          onAdd={() => setAdding(definition)}
                        />
                      ))}
                    </div>
                  </section>
                );
              })}
            </div>
          </TabsContent>
        </Tabs>
      )}

      <AddDialog
        definition={adding}
        onOpenChange={(open) => !open && setAdding(null)}
        onCreated={load}
      />
    </PagePanel>
  );
}

/* ------------------------------------------------------------------ *
 * Cards
 * ------------------------------------------------------------------ */

function ConnectionCard({ connector }: { connector: Connector }) {
  const tone =
    connector.status === "Connected" && connector.isActive
      ? "text-emerald-600 dark:text-emerald-400"
      : connector.status === "Error"
        ? "text-rose-600 dark:text-rose-400"
        : "text-muted-foreground";

  return (
    <Link
      href={`/dashboard/setup/connectors/${connector.id}`}
      className="flex flex-col gap-2 rounded-lg border p-3.5 transition-colors hover:bg-accent/40"
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="truncate text-[13.5px] font-semibold">{connector.name}</p>
          <p className="truncate text-[11.5px] text-muted-foreground">
            {connector.providerName} · {connector.category}
          </p>
        </div>

        <Badge
          variant="outline"
          className={cn("h-5 shrink-0 gap-1.5 px-1.5 text-[11px] font-normal", tone)}
        >
          <span className="size-1.5 rounded-full bg-current" />
          {!connector.isActive
            ? "Off"
            : connector.status === "NotConfigured"
              ? "Needs setup"
              : connector.status}
        </Badge>
      </div>

      {connector.lastError ? (
        <p className="line-clamp-2 text-[11.5px] text-rose-600 dark:text-rose-400">
          {connector.lastError}
        </p>
      ) : null}

      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[11.5px] text-muted-foreground">
        <span className="tabular-nums">
          {connector.eventCount.toLocaleString("en-IN")}{" "}
          {connector.eventCount === 1 ? "delivery" : "deliveries"}
        </span>
        <span>
          {connector.lastEventAt
            ? `last ${relative(connector.lastEventAt)}`
            : "nothing yet"}
        </span>
        {connector.defaultBranchName ? <span>{connector.defaultBranchName}</span> : null}
      </div>
    </Link>
  );
}

function CatalogueCard({
  definition,
  onAdd,
}: {
  definition: ConnectorDefinition;
  onAdd: () => void;
}) {
  const direction = DIRECTION[definition.direction] ?? DIRECTION.Inbound;
  const DirectionIcon = direction.icon;

  return (
    <div className="flex flex-col gap-2.5 rounded-lg border p-3.5">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="flex items-center gap-1.5 text-[13.5px] font-semibold">
            {definition.name}
            {definition.configuredCount > 0 ? (
              <Badge
                variant="secondary"
                className="h-4 px-1 text-[9.5px] font-normal tabular-nums"
              >
                {definition.configuredCount} connected
              </Badge>
            ) : null}
            {definition.live ? null : (
              <Badge
                variant="outline"
                className="h-4 px-1 text-[9px] font-normal text-amber-600 dark:text-amber-400"
              >
                Inbound only
              </Badge>
            )}
          </p>
          <p className="text-[12px] text-muted-foreground">{definition.tagline}</p>
        </div>

        <Button size="sm" variant="outline" className="h-7 shrink-0" onClick={onAdd}>
          <Plus /> Connect
        </Button>
      </div>

      <p className="text-[12.5px] text-muted-foreground">{definition.description}</p>

      <span className="flex items-center gap-1.5 text-[11px] text-muted-foreground">
        <DirectionIcon className="size-3" />
        {direction.label}
      </span>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Add
 * ------------------------------------------------------------------ */

/**
 * Names the connection and nothing else.
 *
 * Credentials, defaults and the endpoint all live on the detail page, because
 * a dialog that asked for everything would be six fields deep before the
 * administrator had seen what they were configuring.
 */
function AddDialog({
  definition,
  onOpenChange,
  onCreated,
}: {
  definition: ConnectorDefinition | null;
  onOpenChange: (open: boolean) => void;
  onCreated: () => void;
}) {
  const router = useRouter();
  const [name, setName] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  const [last, setLast] = React.useState(definition);
  if (definition !== last) {
    setLast(definition);
    if (definition) setName(`${definition.name} feed`);
  }

  async function save() {
    if (!definition) return;

    setSaving(true);
    try {
      const created = await connectorsApi.create({
        provider: definition.provider,
        name,
        isActive: true,
      });

      onCreated();
      onOpenChange(false);
      router.push(`/dashboard/setup/connectors/${created.id}`);
    } catch (error) {
      toast.error("Could not create that connection", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={definition !== null} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Connect {definition?.name}</DialogTitle>
          <DialogDescription>
            Name it for what it is for. A company usually holds one per project
            or per branch, so &ldquo;{definition?.name} feed&rdquo; is only right
            when there is one.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-1 py-4">
          <Label htmlFor="connector-name">Name</Label>
          <Input
            id="connector-name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="e.g. Trinity Tower 99acres feed"
          />
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button disabled={saving || name.trim().length < 2} onClick={() => void save()}>
            {saving ? <Loader2 className="animate-spin" /> : <Check />}
            Create and configure
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function relative(iso: string) {
  const minutes = Math.round((Date.now() - new Date(iso).getTime()) / 60_000);

  if (minutes < 2) return "just now";
  if (minutes < 60) return `${minutes} min ago`;

  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h ago`;

  return `${Math.round(hours / 24)}d ago`;
}
