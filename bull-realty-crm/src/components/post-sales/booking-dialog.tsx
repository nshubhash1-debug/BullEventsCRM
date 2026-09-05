"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { ClipboardCheck, Loader2, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";

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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import { postSalesApi } from "@/lib/post-sales-api";
import { cn } from "@/lib/utils";

/** Who can be on a booking, in the order they are usually collected. */
const ROLES = ["Primary", "CoApplicant", "Nominee", "PowerOfAttorney"] as const;

const ROLE_LABELS: Record<string, string> = {
  Primary: "Primary applicant",
  CoApplicant: "Co-applicant",
  Nominee: "Nominee",
  PowerOfAttorney: "Power of attorney",
};

interface Draft {
  key: string;
  role: string;
  name: string;
  relation: string;
  phone: string;
  email: string;
  pan: string;
}

function blank(role: string): Draft {
  return {
    key: Math.random().toString(36).slice(2),
    role,
    name: "",
    relation: "",
    phone: "",
    email: "",
    pan: "",
  };
}

/**
 * Turning an accepted quotation into a booking.
 *
 * The applicants are collected here rather than being copied from the lead,
 * because a lead is one person and a booking is a title: the co-applicant whose
 * name goes on the agreement, the nominee, and the PAN each buyer is assessed
 * under are facts nobody has asked for until this moment. Getting them now is
 * what lets the agreement, the TDS and the registration all name the right
 * people without a second round of chasing.
 */
export function BookingDialog({
  quotationId,
  quoteNumber,
  customerName,
  customerPhone,
  customerEmail,
  open,
  onOpenChange,
  onBooked,
}: {
  quotationId: number;
  quoteNumber: string;
  customerName?: string | null;
  customerPhone?: string | null;
  customerEmail?: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onBooked?: () => void;
}) {
  const router = useRouter();

  const [bookingDate, setBookingDate] = React.useState(() =>
    new Date().toISOString().slice(0, 10)
  );
  const [applicants, setApplicants] = React.useState<Draft[]>([]);
  const [saving, setSaving] = React.useState(false);

  const [wasOpen, setWasOpen] = React.useState(open);

  if (open !== wasOpen) {
    setWasOpen(open);

    if (open) {
      setBookingDate(new Date().toISOString().slice(0, 10));
      setSaving(false);
      setApplicants([
        {
          ...blank("Primary"),
          // Seeded from the quotation so the common case is one field to check
          // rather than four to retype — but editable, because the buyer on the
          // agreement is not always the person the lead was opened against.
          name: customerName ?? "",
          phone: customerPhone ?? "",
          email: customerEmail ?? "",
        },
      ]);
    }
  }

  function update(key: string, patch: Partial<Draft>) {
    setApplicants((rows) =>
      rows.map((row) => (row.key === key ? { ...row, ...patch } : row))
    );
  }

  async function save() {
    const named = applicants.filter((a) => a.name.trim());

    if (named.length === 0) {
      toast.error("A booking needs at least the primary applicant's name.");
      return;
    }

    if (!named.some((a) => a.role === "Primary")) {
      toast.error("One applicant has to be the primary.");
      return;
    }

    setSaving(true);
    try {
      const booking = await postSalesApi.create({
        quotationId,
        bookingDate,
        applicants: named.map((a) => ({
          role: a.role,
          name: a.name.trim(),
          relation: a.relation.trim() || null,
          phone: a.phone.trim() || null,
          email: a.email.trim() || null,
          pan: a.pan.trim().toUpperCase() || null,
        })),
      });

      toast.success(`Booking ${booking.summary.bookingNumber} opened`, {
        description: "The payment schedule and document checklist are ready.",
      });

      onOpenChange(false);
      onBooked?.();
      router.push(`/dashboard/post-sales/bookings/${booking.summary.id}`);
    } catch (error) {
      toast.error("Could not open the booking", {
        description: error instanceof ApiError ? error.message : "Network error.",
      });
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="sm:max-w-2xl"
        onClick={(event) => event.stopPropagation()}
      >
        <DialogHeader>
          <DialogTitle>Open a booking from {quoteNumber}</DialogTitle>
          <DialogDescription>
            The unit is allotted and the quotation&apos;s pricing is snapshotted, so
            re-pricing the template later never restates this deal. The payment
            schedule and the document checklist are generated with it.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4">
          <div className="grid max-w-[220px] gap-1.5">
            <Label htmlFor="booking-date">Booking date</Label>
            <Input
              id="booking-date"
              type="date"
              value={bookingDate}
              onChange={(event) => setBookingDate(event.target.value)}
            />
          </div>

          <div className="flex flex-col gap-2">
            <div className="flex items-center justify-between">
              <Label>Applicants</Label>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="h-7 gap-1 text-[12px]"
                onClick={() => setApplicants((rows) => [...rows, blank("CoApplicant")])}
              >
                <Plus className="size-3.5" /> Add applicant
              </Button>
            </div>

            <div className="flex max-h-[46vh] flex-col gap-3 overflow-y-auto pr-1">
              {applicants.map((applicant, index) => (
                <div
                  key={applicant.key}
                  className={cn(
                    "grid gap-2 rounded-lg border p-3 sm:grid-cols-2",
                    applicant.role === "Primary" && "border-primary/30 bg-primary/[0.03]"
                  )}
                >
                  <div className="flex items-center gap-2 sm:col-span-2">
                    <Select
                      value={applicant.role}
                      onValueChange={(role) => update(applicant.key, { role })}
                    >
                      <SelectTrigger size="sm" className="w-[190px] text-[12.5px]">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {ROLES.map((role) => (
                          <SelectItem key={role} value={role}>
                            {ROLE_LABELS[role]}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>

                    <span className="flex-1" />

                    {index > 0 ? (
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        className="h-7 px-2 text-[12px] text-muted-foreground"
                        onClick={() =>
                          setApplicants((rows) =>
                            rows.filter((row) => row.key !== applicant.key)
                          )
                        }
                      >
                        <Trash2 className="size-3.5" />
                      </Button>
                    ) : null}
                  </div>

                  <div className="grid gap-1.5">
                    <Label htmlFor={`name-${applicant.key}`} className="text-[11.5px]">
                      Full name
                    </Label>
                    <Input
                      id={`name-${applicant.key}`}
                      value={applicant.name}
                      onChange={(event) =>
                        update(applicant.key, { name: event.target.value })
                      }
                      placeholder="As it will read on the agreement"
                    />
                  </div>

                  <div className="grid gap-1.5">
                    <Label htmlFor={`rel-${applicant.key}`} className="text-[11.5px]">
                      Relation
                    </Label>
                    <Input
                      id={`rel-${applicant.key}`}
                      value={applicant.relation}
                      onChange={(event) =>
                        update(applicant.key, { relation: event.target.value })
                      }
                      placeholder={
                        applicant.role === "Primary" ? "S/o, D/o, W/o" : "Wife of, Son of"
                      }
                    />
                  </div>

                  <div className="grid gap-1.5">
                    <Label htmlFor={`phone-${applicant.key}`} className="text-[11.5px]">
                      Phone
                    </Label>
                    <Input
                      id={`phone-${applicant.key}`}
                      value={applicant.phone}
                      onChange={(event) =>
                        update(applicant.key, { phone: event.target.value })
                      }
                      className="tabular-nums"
                    />
                  </div>

                  <div className="grid gap-1.5">
                    <Label htmlFor={`pan-${applicant.key}`} className="text-[11.5px]">
                      PAN
                    </Label>
                    <Input
                      id={`pan-${applicant.key}`}
                      value={applicant.pan}
                      onChange={(event) =>
                        update(applicant.key, { pan: event.target.value })
                      }
                      placeholder="ABCDE1234F"
                      className="uppercase tabular-nums"
                    />
                  </div>

                  <div className="grid gap-1.5 sm:col-span-2">
                    <Label htmlFor={`email-${applicant.key}`} className="text-[11.5px]">
                      Email
                    </Label>
                    <Input
                      id={`email-${applicant.key}`}
                      type="email"
                      value={applicant.email}
                      onChange={(event) =>
                        update(applicant.key, { email: event.target.value })
                      }
                    />
                  </div>
                </div>
              ))}
            </div>

            <p className="text-[12px] text-muted-foreground">
              PAN is worth collecting now — it is what the 1% TDS on every instalment
              is filed against, and chasing it after the first demand is late.
            </p>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={save} disabled={saving} className="gap-1.5">
            {saving ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <ClipboardCheck className="size-4" />
            )}
            Open booking
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
