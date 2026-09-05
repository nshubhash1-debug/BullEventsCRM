"use client";

import * as React from "react";
import { useParams } from "next/navigation";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const apiRoot =
  process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, "") ?? "http://localhost:5090";

export default function QuestionnairePage() {
  const { token } = useParams<{ token: string }>();
  const [busy, setBusy] = React.useState(false);
  const [done, setDone] = React.useState(false);
  const [name, setName] = React.useState("");

  React.useEffect(() => {
    fetch(`${apiRoot}/api/public/questionnaire/${token}`)
      .then((r) => (r.ok ? r.json() : Promise.reject()))
      .then((data: { name?: string; completed?: boolean }) => {
        setName(data.name ?? "");
        if (data.completed) setDone(true);
      })
      .catch(() => toast.error("This questionnaire link is not valid."));
  }, [token]);

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setBusy(true);
    try {
      const response = await fetch(`${apiRoot}/api/public/questionnaire/${token}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          eventDate: form.get("eventDate") || undefined,
          guestCount: form.get("guestCount") ? Number(form.get("guestCount")) : undefined,
          budgetMax: form.get("budgetMax") ? Number(form.get("budgetMax")) : undefined,
          servicesNeeded: form.get("servicesNeeded") || undefined,
          partnerName: form.get("partnerName") || undefined,
          notes: form.get("notes") || undefined,
        }),
      });
      if (!response.ok) throw new Error("Could not save.");
      setDone(true);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Could not save.");
    } finally {
      setBusy(false);
    }
  }

  if (done) {
    return (
      <main className="mx-auto max-w-md px-6 py-16">
        <h1 className="text-2xl font-semibold">Received</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          Thank you{name ? `, ${name}` : ""}. Your planner will take it from here.
        </p>
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-md px-6 py-12">
      <h1 className="text-2xl font-semibold">A few details before we meet</h1>
      <p className="mt-1 text-sm text-muted-foreground">
        Date, guests and budget let the first call start informed.
      </p>
      <form className="mt-6 flex flex-col gap-3" onSubmit={(e) => void onSubmit(e)}>
        <div className="space-y-1">
          <Label htmlFor="partnerName">Partner / guest of honour</Label>
          <Input id="partnerName" name="partnerName" />
        </div>
        <div className="space-y-1">
          <Label htmlFor="eventDate">Event date</Label>
          <Input id="eventDate" name="eventDate" type="date" />
        </div>
        <div className="space-y-1">
          <Label htmlFor="guestCount">Guest count</Label>
          <Input id="guestCount" name="guestCount" type="number" min={1} />
        </div>
        <div className="space-y-1">
          <Label htmlFor="budgetMax">Budget (upper)</Label>
          <Input id="budgetMax" name="budgetMax" type="number" min={0} />
        </div>
        <div className="space-y-1">
          <Label htmlFor="servicesNeeded">Services you want us to handle</Label>
          <Input id="servicesNeeded" name="servicesNeeded" placeholder="Venue, catering, décor…" />
        </div>
        <div className="space-y-1">
          <Label htmlFor="notes">Notes</Label>
          <Input id="notes" name="notes" />
        </div>
        <Button type="submit" disabled={busy}>
          {busy ? "Saving…" : "Send answers"}
        </Button>
      </form>
    </main>
  );
}
