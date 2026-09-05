"use client";

import * as React from "react";
import { useParams } from "next/navigation";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { EVENT_TYPES, PLANNING_PACKAGES } from "@/lib/api";
import { EVENT_SLOTS } from "@/lib/inventory-api";

const apiRoot =
  process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, "") ?? "http://localhost:5090";

type AvailabilityResult = {
  available: boolean;
  conflicts: {
    unitName?: string;
    eventDate?: string;
    slot?: string;
    status?: string;
    clientName?: string | null;
  }[];
};

export default function PublicEnquirePage() {
  const { slug } = useParams<{ slug: string }>();
  const [busy, setBusy] = React.useState(false);
  const [done, setDone] = React.useState(false);
  const [eventDate, setEventDate] = React.useState("");
  const [eventSlot, setEventSlot] = React.useState("Evening");
  const [availability, setAvailability] = React.useState<AvailabilityResult | null>(
    null
  );

  React.useEffect(() => {
    if (!slug || !eventDate) {
      setAvailability(null);
      return;
    }

    let cancelled = false;
    const q = new URLSearchParams({ eventDate, slot: eventSlot });
    fetch(`${apiRoot}/api/public/${slug}/availability?${q}`)
      .then(async (response) => {
        if (!response.ok) return null;
        return (await response.json()) as AvailabilityResult;
      })
      .then((body) => {
        if (!cancelled) setAvailability(body);
      })
      .catch(() => {
        if (!cancelled) setAvailability(null);
      });

    return () => {
      cancelled = true;
    };
  }, [slug, eventDate, eventSlot]);

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setBusy(true);
    try {
      const response = await fetch(`${apiRoot}/api/public/${slug}/enquiries`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: form.get("name"),
          partnerName: form.get("partnerName") || undefined,
          phone: form.get("phone") || undefined,
          email: form.get("email") || undefined,
          eventType: form.get("eventType") || undefined,
          eventDate: eventDate || undefined,
          eventSlot,
          guestCount: form.get("guestCount") ? Number(form.get("guestCount")) : undefined,
          budgetMax: form.get("budgetMax") ? Number(form.get("budgetMax")) : undefined,
          planningPackage: form.get("planningPackage") || undefined,
          notes: form.get("notes") || undefined,
        }),
      });
      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as { message?: string } | null;
        throw new Error(body?.message ?? "Could not send the enquiry.");
      }
      const payload = (await response.json()) as { availability?: AvailabilityResult };
      if (payload.availability) setAvailability(payload.availability);
      setDone(true);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Could not send the enquiry.");
    } finally {
      setBusy(false);
    }
  }

  if (done) {
    return (
      <main className="mx-auto max-w-md px-6 py-16">
        <h1 className="text-2xl font-semibold">Thank you</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          A planner will reply shortly. Keep an eye on your phone and email.
        </p>
        {availability && !availability.available ? (
          <p className="mt-3 rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm text-amber-800 dark:text-amber-200">
            That date already has a booking on the calendar. Your enquiry is
            still with us — we will suggest the next open slot.
          </p>
        ) : null}
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-md px-6 py-12">
      <h1 className="text-2xl font-semibold">Tell us about your event</h1>
      <p className="mt-1 text-sm text-muted-foreground">
        Date, guests and budget help us reply with a real next step.
      </p>
      <form className="mt-6 flex flex-col gap-3" onSubmit={(e) => void onSubmit(e)}>
        <div className="space-y-1">
          <Label htmlFor="name">Your name</Label>
          <Input id="name" name="name" required />
        </div>
        <div className="space-y-1">
          <Label htmlFor="partnerName">Partner / guest of honour</Label>
          <Input id="partnerName" name="partnerName" />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <Label htmlFor="phone">Mobile</Label>
            <Input id="phone" name="phone" />
          </div>
          <div className="space-y-1">
            <Label htmlFor="email">Email</Label>
            <Input id="email" name="email" type="email" />
          </div>
        </div>
        <div className="space-y-1">
          <Label htmlFor="eventType">Occasion</Label>
          <select id="eventType" name="eventType" className="h-9 w-full rounded-md border bg-background px-2 text-sm">
            <option value="">—</option>
            {EVENT_TYPES.map((t) => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <Label htmlFor="eventDate">Event date</Label>
            <Input
              id="eventDate"
              name="eventDate"
              type="date"
              value={eventDate}
              onChange={(e) => setEventDate(e.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="eventSlot">Slot</Label>
            <select
              id="eventSlot"
              className="h-9 w-full rounded-md border bg-background px-2 text-sm"
              value={eventSlot}
              onChange={(e) => setEventSlot(e.target.value)}
            >
              {EVENT_SLOTS.map((s) => (
                <option key={s} value={s}>
                  {s === "FullDay" ? "Full day" : s}
                </option>
              ))}
            </select>
          </div>
        </div>
        {availability && eventDate ? (
          <p
            className={
              availability.available
                ? "rounded-md border border-emerald-500/30 bg-emerald-500/10 px-3 py-2 text-[12.5px] text-emerald-800 dark:text-emerald-200"
                : "rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-[12.5px] text-amber-800 dark:text-amber-200"
            }
          >
            {availability.available
              ? "That date looks open on our calendar."
              : "That date already has a booking. Send the enquiry anyway and we will offer alternatives."}
          </p>
        ) : null}
        <div className="space-y-1">
          <Label htmlFor="guestCount">Guests</Label>
          <Input id="guestCount" name="guestCount" type="number" min={1} />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <Label htmlFor="budgetMax">Budget (upper)</Label>
            <Input id="budgetMax" name="budgetMax" type="number" min={0} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="planningPackage">Package</Label>
            <select
              id="planningPackage"
              name="planningPackage"
              className="h-9 w-full rounded-md border bg-background px-2 text-sm"
            >
              <option value="">—</option>
              {PLANNING_PACKAGES.map((p) => (
                <option key={p.value} value={p.value}>
                  {p.label}
                </option>
              ))}
            </select>
          </div>
        </div>
        <div className="space-y-1">
          <Label htmlFor="notes">Anything else</Label>
          <Input id="notes" name="notes" />
        </div>
        <Button type="submit" disabled={busy}>
          {busy ? "Sending…" : "Send enquiry"}
        </Button>
      </form>
    </main>
  );
}
