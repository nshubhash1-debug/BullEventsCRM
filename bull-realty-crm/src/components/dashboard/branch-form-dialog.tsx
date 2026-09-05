"use client";

import * as React from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2, Plus } from "lucide-react";
import { useForm } from "react-hook-form";
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
import { ApiError, createBranch, updateBranch, type Branch } from "@/lib/api";

const branchSchema = z.object({
  name: z.string().trim().min(2, "Name is too short"),
  city: z.string().trim().min(2, "City is too short"),
  address: z.string().trim().optional(),
  contactPhone: z.string().trim().optional(),
});

type BranchFormValues = z.infer<typeof branchSchema>;

export function BranchFormDialog({
  branch,
  onSaved,
  trigger,
}: {
  branch?: Branch;
  onSaved: (branch: Branch) => void;
  trigger?: React.ReactNode;
}) {
  const [open, setOpen] = React.useState(false);
  const isEdit = !!branch;

  const form = useForm<BranchFormValues>({
    resolver: zodResolver(branchSchema),
    defaultValues: {
      name: branch?.name ?? "",
      city: branch?.city ?? "",
      address: branch?.address ?? "",
      contactPhone: branch?.contactPhone ?? "",
    },
  });

  React.useEffect(() => {
    if (open) {
      form.reset({
        name: branch?.name ?? "",
        city: branch?.city ?? "",
        address: branch?.address ?? "",
        contactPhone: branch?.contactPhone ?? "",
      });
    }
  }, [open, branch, form]);

  async function onSubmit(values: BranchFormValues) {
    try {
      const result = isEdit
        ? await updateBranch(branch.id, values)
        : await createBranch(values);
      toast.success(isEdit ? "Branch updated" : "Branch created");
      onSaved(result);
      setOpen(false);
    } catch (error) {
      toast.error("Could not save branch", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        {trigger ?? (
          <Button size="sm">
            <Plus /> Add branch
          </Button>
        )}
      </DialogTrigger>
      <DialogContent>
        <form onSubmit={(e) => void form.handleSubmit(onSubmit)(e)}>
          <DialogHeader>
            <DialogTitle>{isEdit ? "Edit branch" : "Add branch"}</DialogTitle>
            <DialogDescription>
              {isEdit
                ? "Update this branch's details."
                : "Create a new office under your company."}
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-4 py-4">
            <div className="space-y-1">
              <Label htmlFor="branch-name">Name</Label>
              <Input
                id="branch-name"
                placeholder="e.g. Gurugram — Sector 44"
                aria-invalid={!!form.formState.errors.name}
                {...form.register("name")}
              />
              {form.formState.errors.name ? (
                <p className="text-xs text-destructive">
                  {form.formState.errors.name.message}
                </p>
              ) : null}
            </div>

            <div className="space-y-1">
              <Label htmlFor="branch-city">City</Label>
              <Input
                id="branch-city"
                placeholder="e.g. Gurugram"
                aria-invalid={!!form.formState.errors.city}
                {...form.register("city")}
              />
              {form.formState.errors.city ? (
                <p className="text-xs text-destructive">
                  {form.formState.errors.city.message}
                </p>
              ) : null}
            </div>

            <div className="space-y-1">
              <Label htmlFor="branch-address">Address (optional)</Label>
              <Input id="branch-address" {...form.register("address")} />
            </div>

            <div className="space-y-1">
              <Label htmlFor="branch-phone">Contact phone (optional)</Label>
              <Input id="branch-phone" {...form.register("contactPhone")} />
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
              {isEdit ? "Save changes" : "Create branch"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
