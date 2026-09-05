"use client";

import * as React from "react";
import { KeyRound, Loader2, RefreshCw } from "lucide-react";
import { toast } from "sonner";

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
import { ApiError, resetUserPassword, type UserListItem } from "@/lib/api";

/**
 * A password an administrator can read out, rather than one nobody can type.
 *
 * The alphabet drops the characters that get misread down a phone line — 0/O,
 * 1/l/I — because this password is going to be dictated, and a "temporary
 * password does not work" ticket costs more than the entropy saved.
 */
const ALPHABET = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

function suggestPassword(length = 14) {
  const bytes = new Uint32Array(length);
  crypto.getRandomValues(bytes);
  return [...bytes].map((n) => ALPHABET[n % ALPHABET.length]).join("");
}

export function ResetPasswordDialog({
  user,
  trigger,
  open: controlledOpen,
  onOpenChange,
  onReset,
}: {
  user: UserListItem;
  /** Omitted when the caller drives the dialog through `open` instead. */
  trigger?: React.ReactNode;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  onReset?: () => void;
}) {
  const [uncontrolledOpen, setUncontrolledOpen] = React.useState(false);
  const open = controlledOpen ?? uncontrolledOpen;

  const setOpen = React.useCallback(
    (next: boolean) => {
      setUncontrolledOpen(next);
      onOpenChange?.(next);
    },
    [onOpenChange]
  );

  const [password, setPassword] = React.useState(suggestPassword);
  const [saving, setSaving] = React.useState(false);

  // A fresh suggestion each time the dialog opens, adjusted during render
  // rather than in an effect: an effect would render the previous reset's
  // password for one frame before replacing it, and that is a password an
  // administrator might read out.
  const [wasOpen, setWasOpen] = React.useState(open);
  if (open !== wasOpen) {
    setWasOpen(open);
    if (open) setPassword(suggestPassword());
  }

  const tooShort = password.length < 8;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (tooShort) return;

    setSaving(true);
    try {
      await resetUserPassword(user.id, password);
      toast.success("Password reset", {
        description: `${user.name} must set their own password at next sign-in.`,
      });
      onReset?.();
      setOpen(false);
    } catch (error) {
      toast.error("Could not reset the password", {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      {trigger ? <DialogTrigger asChild>{trigger}</DialogTrigger> : null}
      <DialogContent>
        <form onSubmit={(event) => void submit(event)}>
          <DialogHeader>
            <DialogTitle>Reset password</DialogTitle>
            <DialogDescription>
              Sets a new password on {user.name}&apos;s account. Because you will
              know it too, the account stays blocked until they replace it at
              their next sign-in.
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-2 py-4">
            <Label htmlFor="reset-password">Temporary password</Label>
            <div className="flex gap-1.5">
              <Input
                id="reset-password"
                type="text"
                autoComplete="off"
                spellCheck={false}
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                aria-invalid={tooShort}
                className="font-mono"
              />
              <Button
                type="button"
                variant="outline"
                size="icon"
                aria-label="Generate another"
                title="Generate another"
                onClick={() => setPassword(suggestPassword())}
              >
                <RefreshCw className="size-4" />
              </Button>
            </div>
            {tooShort ? (
              <p className="text-xs text-destructive">
                Must be at least 8 characters.
              </p>
            ) : (
              <p className="text-xs text-muted-foreground">
                Read this out or send it over a channel you trust — it is not
                emailed automatically.
              </p>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={saving || tooShort}>
              {saving ? <Loader2 className="animate-spin" /> : <KeyRound />}
              Reset password
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
