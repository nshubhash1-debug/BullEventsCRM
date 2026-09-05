"use client";

import * as React from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2, RefreshCw, UserPlus } from "lucide-react";
import { Controller, useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
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
import { ScrollArea } from "@/components/ui/scroll-area";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import {
  ApiError,
  createUser,
  ROLES,
  updateUser,
  type Branch,
  type RoleSummary,
  type UserListItem,
} from "@/lib/api";

/** The sentinel the manager picker uses for "reports to nobody". */
const NO_MANAGER = "none";

const baseSchema = {
  role: z.string().min(1, "Select a role"),
  branchIds: z.array(z.number()),
  managerId: z.number().nullable(),
};

const createUserSchema = z.object({
  name: z.string().trim().min(2, "Name is too short"),
  email: z.string().trim().email("Enter a valid email address"),
  password: z.string().min(8, "Password must be at least 8 characters"),
  isActive: z.boolean(),
  mustChangePassword: z.boolean(),
  ...baseSchema,
});

const editUserSchema = z.object({
  isActive: z.boolean(),
  mustChangePassword: z.boolean(),
  ...baseSchema,
});

type FormValues = {
  name?: string;
  email?: string;
  password?: string;
  role: string;
  isActive: boolean;
  mustChangePassword: boolean;
  branchIds: number[];
  managerId: number | null;
};

const ALPHABET = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

function suggestPassword(length = 14) {
  const bytes = new Uint32Array(length);
  crypto.getRandomValues(bytes);
  return [...bytes].map((n) => ALPHABET[n % ALPHABET.length]).join("");
}

export function UserFormDialog({
  user,
  branches,
  /** Candidate managers. Empty is fine — the field then offers only "top of the line". */
  people = [],
  /**
   * The live role catalogue, when the page has it. Falls back to the static
   * list so the dialog still works before that request lands.
   */
  roles,
  onSaved,
  trigger,
}: {
  user?: UserListItem;
  branches: Branch[];
  people?: UserListItem[];
  roles?: RoleSummary[];
  onSaved: (user: UserListItem) => void;
  trigger?: React.ReactNode;
}) {
  const [open, setOpen] = React.useState(false);
  const isEdit = !!user;

  const roleOptions = React.useMemo(
    () =>
      roles?.length
        ? roles.map((role) => ({
            value: role.key,
            label: role.name,
            hint: role.scopeLabel,
          }))
        : ROLES.map((role) => ({ value: role.value, label: role.label, hint: null })),
    [roles]
  );

  const defaults = React.useCallback(
    (): FormValues => ({
      name: "",
      email: "",
      password: "",
      role: user?.role ?? "SalesExecutive",
      isActive: user?.isActive ?? true,
      mustChangePassword: user?.mustChangePassword ?? true,
      branchIds: user?.branchIds ?? [],
      managerId: user?.managerId ?? null,
    }),
    [user]
  );

  const form = useForm<FormValues>({
    resolver: zodResolver(isEdit ? editUserSchema : createUserSchema),
    defaultValues: defaults(),
  });

  React.useEffect(() => {
    if (!open) return;
    form.reset({
      ...defaults(),
      password: isEdit ? "" : suggestPassword(),
    });
  }, [open, isEdit, defaults, form]);

  const selectedRole = form.watch("role");
  const roleScope = roles?.find((role) => role.key === selectedRole)?.scopeLabel;

  // Somebody cannot manage themselves. Their wider subtree is filtered on the
  // reporting-lines screen, which has the tree; here the server is the backstop
  // and rejects a loop with a message this dialog surfaces.
  const managerOptions = people
    .filter((person) => person.id !== user?.id)
    .sort((a, b) => a.name.localeCompare(b.name));

  async function onSubmit(values: FormValues) {
    try {
      const result = isEdit
        ? await updateUser(user.id, {
            role: values.role,
            isActive: values.isActive,
            branchIds: values.branchIds,
            managerId: values.managerId,
          })
        : await createUser({
            name: values.name!,
            email: values.email!,
            password: values.password!,
            role: values.role,
            branchIds: values.branchIds,
            managerId: values.managerId,
            mustChangePassword: values.mustChangePassword,
          });

      toast.success(isEdit ? "User updated" : "User invited");
      onSaved(result);
      setOpen(false);
    } catch (error) {
      toast.error("Could not save user", {
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
            <UserPlus /> Invite user
          </Button>
        )}
      </DialogTrigger>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <form onSubmit={(e) => void form.handleSubmit(onSubmit)(e)}>
          <DialogHeader>
            <DialogTitle>{isEdit ? "Edit user" : "Invite user"}</DialogTitle>
            <DialogDescription>
              {isEdit
                ? "Role, reporting line, branches and access."
                : "Add a teammate, then set what they can see and who they report to."}
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-4 py-4">
            {!isEdit ? (
              <>
                <div className="space-y-1">
                  <Label htmlFor="user-name">Full name</Label>
                  <Input
                    id="user-name"
                    placeholder="e.g. Rohan Mehta"
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
                  <Label htmlFor="user-email">Work email</Label>
                  <Input
                    id="user-email"
                    type="email"
                    placeholder="name@company.com"
                    aria-invalid={!!form.formState.errors.email}
                    {...form.register("email")}
                  />
                  {form.formState.errors.email ? (
                    <p className="text-xs text-destructive">
                      {form.formState.errors.email.message}
                    </p>
                  ) : null}
                </div>

                <div className="space-y-1">
                  <Label htmlFor="user-password">Temporary password</Label>
                  <div className="flex gap-1.5">
                    <Input
                      id="user-password"
                      type="text"
                      autoComplete="off"
                      spellCheck={false}
                      placeholder="Min. 8 characters"
                      aria-invalid={!!form.formState.errors.password}
                      className="font-mono"
                      {...form.register("password")}
                    />
                    <Button
                      type="button"
                      variant="outline"
                      size="icon"
                      aria-label="Generate another"
                      title="Generate another"
                      onClick={() =>
                        form.setValue("password", suggestPassword(), {
                          shouldValidate: true,
                        })
                      }
                    >
                      <RefreshCw className="size-4" />
                    </Button>
                  </div>
                  {form.formState.errors.password ? (
                    <p className="text-xs text-destructive">
                      {form.formState.errors.password.message}
                    </p>
                  ) : (
                    <p className="text-xs text-muted-foreground">
                      Nothing is emailed yet — pass this on over a channel you
                      trust.
                    </p>
                  )}
                </div>
              </>
            ) : (
              <div className="rounded-lg border bg-muted/40 px-3 py-2">
                <p className="text-sm font-medium">{user.name}</p>
                <p className="text-xs text-muted-foreground">{user.email}</p>
              </div>
            )}

            <div className="space-y-1">
              <Label>Role</Label>
              <Controller
                control={form.control}
                name="role"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger className="w-full">
                      <SelectValue placeholder="Select a role" />
                    </SelectTrigger>
                    <SelectContent>
                      {roleOptions.map((role) => (
                        <SelectItem key={role.value} value={role.value}>
                          <span className="flex flex-col">
                            <span>{role.label}</span>
                            {role.hint ? (
                              <span className="text-[11px] text-muted-foreground">
                                {role.hint}
                              </span>
                            ) : null}
                          </span>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
              {roleScope ? (
                <p className="text-xs text-muted-foreground">{roleScope}.</p>
              ) : null}
            </div>

            <div className="space-y-1">
              <Label>Reports to</Label>
              <Controller
                control={form.control}
                name="managerId"
                render={({ field }) => (
                  <Select
                    value={field.value === null ? NO_MANAGER : String(field.value)}
                    onValueChange={(value) =>
                      field.onChange(value === NO_MANAGER ? null : Number(value))
                    }
                  >
                    <SelectTrigger className="w-full">
                      <SelectValue placeholder="Top of the line" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={NO_MANAGER}>
                        <span className="text-muted-foreground">
                          Top of the line
                        </span>
                      </SelectItem>
                      {managerOptions.map((person) => (
                        <SelectItem key={person.id} value={String(person.id)}>
                          <span className="flex flex-col">
                            <span>{person.name}</span>
                            <span className="text-[11px] text-muted-foreground">
                              {person.roleName}
                            </span>
                          </span>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
              <p className="text-xs text-muted-foreground">
                A team-scoped role sees everyone beneath them here, to any depth.
              </p>
            </div>

            <div className="space-y-1.5">
              <Label>Branches</Label>
              <Controller
                control={form.control}
                name="branchIds"
                render={({ field }) => (
                  <ScrollArea className="max-h-40 rounded-lg border">
                    <div className="flex flex-col gap-2 p-3">
                      {branches.length === 0 ? (
                        <p className="text-xs text-muted-foreground">
                          No branches yet — add one first.
                        </p>
                      ) : (
                        branches.map((branch) => {
                          const checked = field.value.includes(branch.id);
                          return (
                            <label
                              key={branch.id}
                              className="flex items-center gap-2 text-sm"
                            >
                              <Checkbox
                                checked={checked}
                                onCheckedChange={(value) => {
                                  field.onChange(
                                    value
                                      ? [...field.value, branch.id]
                                      : field.value.filter(
                                          (id) => id !== branch.id
                                        )
                                  );
                                }}
                              />
                              {branch.name}
                            </label>
                          );
                        })
                      )}
                    </div>
                  </ScrollArea>
                )}
              />
            </div>

            {isEdit ? (
              <div className="flex items-center justify-between rounded-lg border p-3">
                <div>
                  <Label htmlFor="user-active">Active</Label>
                  <p className="text-xs text-muted-foreground">
                    Inactive users cannot sign in.
                  </p>
                </div>
                <Controller
                  control={form.control}
                  name="isActive"
                  render={({ field }) => (
                    <Switch
                      id="user-active"
                      checked={field.value}
                      onCheckedChange={field.onChange}
                    />
                  )}
                />
              </div>
            ) : (
              <div className="flex items-center justify-between rounded-lg border p-3">
                <div>
                  <Label htmlFor="user-must-change">
                    Make them set their own password
                  </Label>
                  <p className="text-xs text-muted-foreground">
                    Two people know the password above. Leave this on unless you
                    have a reason not to.
                  </p>
                </div>
                <Controller
                  control={form.control}
                  name="mustChangePassword"
                  render={({ field }) => (
                    <Switch
                      id="user-must-change"
                      checked={field.value}
                      onCheckedChange={field.onChange}
                    />
                  )}
                />
              </div>
            )}
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
              {isEdit ? "Save changes" : "Send invite"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
