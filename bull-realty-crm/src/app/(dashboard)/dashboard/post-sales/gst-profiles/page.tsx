"use client";

import * as React from "react";
import { Building2, Landmark, Loader2, Plus, Save } from "lucide-react";
import { toast } from "sonner";

import { Pill } from "@/components/crm/metrics";
import { CrmLoadingState } from "@/components/shell/crm-loader";
import { PagePanel } from "@/components/shell/page-panel";
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
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api";
import { billingApi, type BillingReference, type GstProfile } from "@/lib/billing-api";
import { cn } from "@/lib/utils";

/** A blank profile, so "add" and "edit" are the same form. */
const BLANK: GstProfile = {
  id: 0,
  projectId: null,
  projectName: null,
  legalName: "",
  tradeName: null,
  gstin: "",
  pan: null,
  stateName: "",
  stateCode: "",
  registeredAddress: null,
  defaultTreatment: "ResidentialUnderConstruction",
  defaultSacCode: "9954",
  occupancyCertificateOn: null,
  invoicePrefix: "INV",
  creditNotePrefix: "CN",
  bankAccountName: null,
  bankAccountNumber: null,
  bankIfsc: null,
  bankBranch: null,
  isActive: true,
};

/**
 * The developer's own tax identity.
 *
 * Held per project rather than per company because that is how the registration
 * actually works: a developer building in two states holds two GSTINs, and the
 * place of supply for immovable property is the state the building stands in —
 * never the state the buyer lives in. One company-wide number would put the
 * wrong GSTIN on half the invoices.
 */
export default function GstProfilesPage() {
  const [profiles, setProfiles] = React.useState<GstProfile[] | null>(null);
  const [reference, setReference] = React.useState<BillingReference | null>(null);
  const [selected, setSelected] = React.useState<GstProfile | null>(null);

  const load = React.useCallback(() => {
    billingApi
      .profiles()
      .then((rows: GstProfile[]) => {
        setProfiles(rows);
        setSelected((current) =>
          current && current.id !== 0
            ? (rows.find((r: GstProfile) => r.id === current.id) ?? rows[0] ?? BLANK)
            : (current ?? rows[0] ?? BLANK)
        );
      })
      .catch(() => {
        setProfiles([]);
        setSelected(BLANK);
      });
  }, []);

  React.useEffect(load, [load]);

  React.useEffect(() => {
    billingApi.reference().then(setReference).catch(() => setReference(null));
  }, []);

  if (!profiles || !selected) {
    return (
      <PagePanel icon={Building2} title="GST profiles">
        <CrmLoadingState label="Loading profiles" />
      </PagePanel>
    );
  }

  return (
    <PagePanel
      icon={Building2}
      title="GST profiles"
      hint="A tax invoice needs a GSTIN and a place of supply to be a valid document. Nothing can be invoiced until at least one profile exists."
      actions={
        <Button
          size="sm"
          variant="outline"
          className="h-8 gap-1.5 text-[12.5px]"
          onClick={() => setSelected(BLANK)}
        >
          <Plus className="size-3.5" />
          Add a profile
        </Button>
      }
    >
      <div className="flex min-h-0 flex-1 gap-4 px-5 pb-4">
        <aside className="w-60 shrink-0 overflow-y-auto rounded-xl border bg-card shadow-xs">
          {profiles.length === 0 ? (
            <p className="px-3 py-4 text-[12px] text-muted-foreground">
              Nothing configured yet.
            </p>
          ) : (
            profiles.map((profile) => (
              <button
                key={profile.id}
                type="button"
                onClick={() => setSelected(profile)}
                className={cn(
                  "flex w-full flex-col items-start gap-0.5 border-b px-3 py-2.5 text-left transition-colors last:border-0",
                  selected.id === profile.id ? "bg-primary/10" : "hover:bg-accent/50"
                )}
              >
                <span className="flex w-full items-center gap-1.5 text-[12.5px] font-medium">
                  <span className="min-w-0 flex-1 truncate">
                    {profile.projectName ?? "Company-wide"}
                  </span>
                  {profile.projectId === null ? <Pill tone="neutral">fallback</Pill> : null}
                </span>
                <span className="font-mono text-[10.5px] text-muted-foreground">
                  {profile.gstin}
                </span>
              </button>
            ))
          )}
        </aside>

        <ProfileForm
          key={selected.id}
          profile={selected}
          treatments={reference?.treatments ?? []}
          onSaved={load}
        />
      </div>
    </PagePanel>
  );
}

function ProfileForm({
  profile,
  treatments,
  onSaved,
}: {
  profile: GstProfile;
  treatments: Array<{ value: string; label: string; rate: number }>;
  onSaved: () => void;
}) {
  const [form, setForm] = React.useState(profile);
  const [saving, setSaving] = React.useState(false);

  const set = <K extends keyof GstProfile>(key: K, value: GstProfile[K]) =>
    setForm((current) => ({ ...current, [key]: value }));

  // The first two digits of a GSTIN are the state code, so it is shown rather
  // than asked for — a typed code that disagrees with the GSTIN is a rejected
  // return nobody notices until the filing bounces.
  const derivedStateCode = form.gstin.trim().slice(0, 2);
  const gstinLooksRight = /^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9A-Z]{3}$/.test(
    form.gstin.trim().toUpperCase()
  );

  const valid = form.legalName.trim() !== "" && form.gstin.trim().length === 15
    && form.stateName.trim() !== "";

  async function save() {
    setSaving(true);
    try {
      await billingApi.saveProfile({
        ...form,
        legalName: form.legalName.trim(),
        gstin: form.gstin.trim().toUpperCase(),
        stateName: form.stateName.trim(),
      });

      toast.success("Profile saved", {
        description: "Invoices raised from now on will carry these details.",
      });

      onSaved();
    } catch (error) {
      toast.error("The profile could not be saved", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="min-w-0 flex-1 overflow-y-auto rounded-xl border bg-card p-4 shadow-xs">
      <div className="grid gap-4">
        {/* ---------------- identity ---------------- */}
        <Section title="Who is raising the invoice">
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Legal name" htmlFor="g-legal">
              <Input
                id="g-legal"
                value={form.legalName}
                onChange={(e) => set("legalName", e.target.value)}
                placeholder="Bull Events Pvt Ltd"
              />
            </Field>

            <Field label="Trade name" htmlFor="g-trade">
              <Input
                id="g-trade"
                value={form.tradeName ?? ""}
                onChange={(e) => set("tradeName", e.target.value || null)}
              />
            </Field>

            <Field
              label="GSTIN"
              htmlFor="g-gstin"
              hint={
                form.gstin.trim().length === 0
                  ? "Fifteen characters. The first two are the state code."
                  : gstinLooksRight
                    ? `State code ${derivedStateCode} — derived, not typed.`
                    : "That does not look like a GSTIN. Check it before invoices go out on it."
              }
              bad={form.gstin.trim().length > 0 && !gstinLooksRight}
            >
              <Input
                id="g-gstin"
                value={form.gstin}
                onChange={(e) => set("gstin", e.target.value.toUpperCase())}
                className="font-mono"
                maxLength={15}
                placeholder="27AABCB1234C1Z5"
              />
            </Field>

            <Field label="PAN" htmlFor="g-pan">
              <Input
                id="g-pan"
                value={form.pan ?? ""}
                onChange={(e) => set("pan", e.target.value.toUpperCase() || null)}
                className="font-mono"
                maxLength={10}
              />
            </Field>

            <Field
              label="Place of supply"
              htmlFor="g-state"
              hint="The state the building stands in — not where the buyer lives. Immovable property is supplied where it is, which is why every invoice splits CGST and SGST."
            >
              <Input
                id="g-state"
                value={form.stateName}
                onChange={(e) => set("stateName", e.target.value)}
                placeholder="Maharashtra"
              />
            </Field>

            <Field label="Registered address" htmlFor="g-addr">
              <Textarea
                id="g-addr"
                value={form.registeredAddress ?? ""}
                onChange={(e) => set("registeredAddress", e.target.value || null)}
                className="min-h-[38px] text-[12.5px]"
              />
            </Field>
          </div>
        </Section>

        {/* ---------------- how it is taxed ---------------- */}
        <Section title="How this project is taxed">
          <div className="grid gap-3 sm:grid-cols-2">
            <Field
              label="Default treatment"
              hint="Under-construction residential is 5% on two-thirds of the value — the other third is deemed to be land and is not taxed. That works out to 3.33% of the whole."
            >
              <Select
                value={form.defaultTreatment}
                onValueChange={(value) => set("defaultTreatment", value)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {treatments.map((t) => (
                    <SelectItem key={t.value} value={t.value}>
                      {t.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>

            <Field label="SAC code" htmlFor="g-sac" hint="9954 is construction services.">
              <Input
                id="g-sac"
                value={form.defaultSacCode}
                onChange={(e) => set("defaultSacCode", e.target.value)}
                className="font-mono"
              />
            </Field>

            <Field
              label="Occupancy certificate issued on"
              htmlFor="g-oc"
              hint="Once the OC is in, the supply stops being construction and leaves GST. Set this and invoices raised after it carry no tax."
            >
              <Input
                id="g-oc"
                type="date"
                value={form.occupancyCertificateOn?.slice(0, 10) ?? ""}
                onChange={(e) => set("occupancyCertificateOn", e.target.value || null)}
              />
            </Field>

            <div className="grid grid-cols-2 gap-3">
              <Field label="Invoice prefix" htmlFor="g-ip">
                <Input
                  id="g-ip"
                  value={form.invoicePrefix}
                  onChange={(e) => set("invoicePrefix", e.target.value)}
                  className="font-mono"
                />
              </Field>
              <Field label="Credit note prefix" htmlFor="g-cp">
                <Input
                  id="g-cp"
                  value={form.creditNotePrefix}
                  onChange={(e) => set("creditNotePrefix", e.target.value)}
                  className="font-mono"
                />
              </Field>
            </div>
          </div>
        </Section>

        {/* ---------------- escrow ---------------- */}
        <Section
          title="Where the money goes"
          hint="Printed on demands, so payment reaches the RERA account rather than a general one."
        >
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Account name" htmlFor="g-acc">
              <Input
                id="g-acc"
                value={form.bankAccountName ?? ""}
                onChange={(e) => set("bankAccountName", e.target.value || null)}
                placeholder="Trinity Towers RERA Escrow"
              />
            </Field>
            <Field label="Account number" htmlFor="g-accno">
              <Input
                id="g-accno"
                value={form.bankAccountNumber ?? ""}
                onChange={(e) => set("bankAccountNumber", e.target.value || null)}
                className="font-mono"
              />
            </Field>
            <Field label="IFSC" htmlFor="g-ifsc">
              <Input
                id="g-ifsc"
                value={form.bankIfsc ?? ""}
                onChange={(e) => set("bankIfsc", e.target.value.toUpperCase() || null)}
                className="font-mono"
                maxLength={11}
              />
            </Field>
            <Field label="Branch" htmlFor="g-branch">
              <Input
                id="g-branch"
                value={form.bankBranch ?? ""}
                onChange={(e) => set("bankBranch", e.target.value || null)}
              />
            </Field>
          </div>
        </Section>

        <div className="flex items-center justify-between border-t pt-3">
          <label className="flex items-center gap-2 text-[12.5px]">
            <Switch
              checked={form.isActive}
              onCheckedChange={(checked) => set("isActive", checked)}
            />
            Active
          </label>

          <Button onClick={save} disabled={saving || !valid} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
            Save profile
          </Button>
        </div>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ *
 * Small pieces
 * ------------------------------------------------------------------ */

function Section({
  title,
  hint,
  children,
}: {
  title: string;
  hint?: string;
  children: React.ReactNode;
}) {
  return (
    <div>
      <p className="mb-0.5 flex items-center gap-1.5 text-[11px] font-semibold tracking-wide text-muted-foreground uppercase">
        <Landmark className="size-3" />
        {title}
      </p>
      {hint ? <p className="mb-2 text-[11.5px] text-muted-foreground">{hint}</p> : null}
      <div className={hint ? "" : "mt-2"}>{children}</div>
    </div>
  );
}

function Field({
  label,
  htmlFor,
  hint,
  bad = false,
  children,
}: {
  label: string;
  htmlFor?: string;
  hint?: string;
  bad?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div className="grid content-start gap-1.5">
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
      {hint ? (
        <p
          className={cn(
            "text-[11px]",
            bad ? "text-red-600 dark:text-red-400" : "text-muted-foreground"
          )}
        >
          {hint}
        </p>
      ) : null}
    </div>
  );
}
