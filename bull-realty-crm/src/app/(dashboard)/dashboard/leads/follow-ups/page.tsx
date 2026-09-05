"use client";

import { BellRing } from "lucide-react";

import { FollowUpsView } from "@/components/crm/follow-ups-view";

export default function FollowUpsPage() {
  return (
    <FollowUpsView
      icon={BellRing}
      title="Follow-ups"
      storageKey="follow-ups"
      hint="Every dated commitment across leads, contacts and opportunities in one queue. Overdue is computed from the due date at read time, so it can never go stale."
    />
  );
}
