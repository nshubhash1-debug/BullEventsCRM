"use client";

import { ListChecks } from "lucide-react";

import { FollowUpsView, channelScope } from "@/components/crm/follow-ups-view";

/** Module-scope constant — the list hook keys its query off this reference. */
const TASKS_ONLY = channelScope("Task");

export default function TasksPage() {
  return (
    <FollowUpsView
      icon={ListChecks}
      title="Tasks"
      storageKey="tasks"
      scope={TASKS_ONLY}
      singleChannel
      hint="Work items that aren't a call, a message or a visit — paperwork, approvals, internal chasing. Same queue and same SLA clock as every other follow-up."
    />
  );
}
