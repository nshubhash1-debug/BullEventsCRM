"use client";

import * as React from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2, Pencil, Plus } from "lucide-react";
import { Controller, useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  ApiError,
  COMPANY_STATUSES,
  createCompany,
  PLAN_TIERS,
  updateCompany,
  type Company,
} from "@/lib/api";

const editSchema = z.object({
  name: z.string().trim().min(2, "Name is too short"),
  planTier: z.string(),
  status: z.string(),
});

/**
 * Creating a tenant also creates its head office and the admin who will run it.
 * Asking for all three here is what stops half-built companies — a company row
 * nobody can sign into is worse than no company at all.
 */
const createSchema = editSchema.omit({ status: true }).extend({
  headOfficeCity: z.string().trim().min(2, "City is too short"),
  headOfficeName: z.string().trim().optional(),
  adminName: z.string().trim().min(2, "Name is too short"),
  adminEmail: z.string().trim().email("Enter a valid email address"),
  adminPassword: z.string().min(8, "Use at least 8 characters"),
});

type EditValues = z.infer<typeof editSchema>;
type CreateValues = z.infer<typeof createSchema>;

function Field({
  id,
  label,
  error,
  children,
  className,
}: {
  id: string;
  label: string;
  error?: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={className}>
      <Label htmlFor={id} className="mb-1 block">
        {label}
      </Label>
      {children}
      {error ? <p className="mt-1 text-xs text-destructive">{error}</p> : null}
    </div>
  );
}

export function CompanyFormDialog({
  company,
  canManagePlan = false,
  onSaved,
}: {
  company: Company;
  /** Platform admins can move a tenant between plans and suspend it. */
  canManagePlan?: boolean;
  onSaved: (company: Company) => void;
}) {
  const [open, setOpen] = React.useState(false);

  const form = useForm<EditValues>({
    resolver: zodResolver(editSchema),
    defaultValues: {
      name: company.name,
      planTier: company.planTier,
      status: company.status,
    },
  });

  const { reset } = form;
  React.useEffect(() => {
    if (open) {
      reset({
        name: company.name,
        planTier: company.planTier,
        status: company.status,
      });
    }
  }, [open, company, reset]);

  async function onSubmit(values: EditValues) {
    try {
      const result = await updateCompany(company.id, {
        name: values.name,
        planTier: canManagePlan ? values.planTier : undefined,
        status: canManagePlan ? values.status : undefined,
      });
      toast.success("Company updated");
      onSaved(result);
      setOpen(false);
    } catch (error) {
      toast.error("Could not save company", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          <Pencil /> Edit
        </Button>
      </DialogTrigger>
      <DialogContent>
        <form onSubmit={(e) => void form.handleSubmit(onSubmit)(e)}>
          <DialogHeader>
            <DialogTitle>Edit company</DialogTitle>
            <DialogDescription>
              {canManagePlan
                ? "Rename the tenant, move it between plans, or suspend it."
                : "Update your company's display name."}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-3 py-4">
            <Field
              id="company-name"
              label="Company name"
              error={form.formState.errors.name?.message}
            >
              <Input
                id="company-name"
                aria-invalid={!!form.formState.errors.name}
                {...form.register("name")}
              />
            </Field>

            {canManagePlan ? (
              <div className="grid gap-3 sm:grid-cols-2">
                <Field id="company-plan" label="Plan">
                  <Controller
                    control={form.control}
                    name="planTier"
                    render={({ field }) => (
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger id="company-plan" className="w-full">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {PLAN_TIERS.map((tier) => (
                            <SelectItem key={tier} value={tier}>
                              {tier}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </Field>

                <Field id="company-status" label="Status">
                  <Controller
                    control={form.control}
                    name="status"
                    render={({ field }) => (
                      <Select value={field.value} onValueChange={field.onChange}>
                        <SelectTrigger id="company-status" className="w-full">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {COMPANY_STATUSES.map((status) => (
                            <SelectItem key={status} value={status}>
                              {status}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                </Field>
              </div>
            ) : null}
          </div>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => setOpen(false)}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting ? (
                <Loader2 className="animate-spin" />
              ) : null}
              Save changes
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

export function CreateCompanyDialog({
  onCreated,
}: {
  onCreated: (company: Company) => void;
}) {
  const [open, setOpen] = React.useState(false);

  const form = useForm<CreateValues>({
    resolver: zodResolver(createSchema),
    defaultValues: {
      name: "",
      planTier: "Starter",
      headOfficeCity: "",
      headOfficeName: "",
      adminName: "",
      adminEmail: "",
      adminPassword: "",
    },
  });

  const { reset } = form;
  React.useEffect(() => {
    if (!open) reset();
  }, [open, reset]);

  async function onSubmit(values: CreateValues) {
    try {
      const result = await createCompany({
        name: values.name,
        planTier: values.planTier,
        headOfficeCity: values.headOfficeCity,
        headOfficeName: values.headOfficeName || undefined,
        adminName: values.adminName,
        adminEmail: values.adminEmail,
        adminPassword: values.adminPassword,
      });
      toast.success(`${result.name} created`, {
        description: `${values.adminEmail} can now sign in as its company admin.`,
      });
      onCreated(result);
      setOpen(false);
    } catch (error) {
      toast.error("Could not create the company", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  const errors = form.formState.errors;

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm">
          <Plus /> New company
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <form onSubmit={(e) => void form.handleSubmit(onSubmit)(e)}>
          <DialogHeader>
            <DialogTitle>New company</DialogTitle>
            <DialogDescription>
              Sets up the tenant, its head office branch, and the admin account
              that runs it. Its data is isolated from every other company from
              the first record.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            <div className="grid gap-3 sm:grid-cols-[1.4fr_1fr]">
              <Field
                id="new-company-name"
                label="Company name"
                error={errors.name?.message}
              >
                <Input
                  id="new-company-name"
                  placeholder="Skyline Estates"
                  aria-invalid={!!errors.name}
                  {...form.register("name")}
                />
              </Field>

              <Field id="new-company-plan" label="Plan">
                <Controller
                  control={form.control}
                  name="planTier"
                  render={({ field }) => (
                    <Select value={field.value} onValueChange={field.onChange}>
                      <SelectTrigger id="new-company-plan" className="w-full">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {PLAN_TIERS.map((tier) => (
                          <SelectItem key={tier} value={tier}>
                            {tier}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                />
              </Field>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <Field
                id="new-company-city"
                label="Head office city"
                error={errors.headOfficeCity?.message}
              >
                <Input
                  id="new-company-city"
                  placeholder="Bengaluru"
                  aria-invalid={!!errors.headOfficeCity}
                  {...form.register("headOfficeCity")}
                />
              </Field>

              <Field id="new-company-branch" label="Branch name (optional)">
                <Input
                  id="new-company-branch"
                  placeholder="Bengaluru HQ"
                  {...form.register("headOfficeName")}
                />
              </Field>
            </div>

            <div className="space-y-3 rounded-md border bg-muted/30 p-3">
              <p className="text-[11px] font-semibold tracking-widest text-muted-foreground uppercase">
                Company admin
              </p>

              <div className="grid gap-3 sm:grid-cols-2">
                <Field
                  id="new-company-admin-name"
                  label="Full name"
                  error={errors.adminName?.message}
                >
                  <Input
                    id="new-company-admin-name"
                    placeholder="Anita Desai"
                    aria-invalid={!!errors.adminName}
                    {...form.register("adminName")}
                  />
                </Field>

                <Field
                  id="new-company-admin-email"
                  label="Work email"
                  error={errors.adminEmail?.message}
                >
                  <Input
                    id="new-company-admin-email"
                    type="email"
                    placeholder="anita@skylineestates.com"
                    aria-invalid={!!errors.adminEmail}
                    {...form.register("adminEmail")}
                  />
                </Field>
              </div>

              <Field
                id="new-company-admin-password"
                label="Temporary password"
                error={errors.adminPassword?.message}
              >
                <Input
                  id="new-company-admin-password"
                  type="password"
                  autoComplete="new-password"
                  placeholder="At least 8 characters"
                  aria-invalid={!!errors.adminPassword}
                  {...form.register("adminPassword")}
                />
              </Field>
            </div>
          </div>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => setOpen(false)}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting ? (
                <Loader2 className="animate-spin" />
              ) : null}
              Create company
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
