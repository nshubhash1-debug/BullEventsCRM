"use client";

import { Mail } from "lucide-react";

import { FollowUpsView, channelScope } from "@/components/crm/follow-ups-view";
import { IntegrationNotice } from "@/components/crm/integration-notice";

const EMAIL_ONLY = channelScope("Email");

export default function EmailAutomationPage() {
  return (
    <FollowUpsView
      icon={Mail}
      title="Email"
      storageKey="automation-email"
      scope={EMAIL_ONLY}
      singleChannel
      hint="Scheduled email touchpoints across the pipeline. Contacts marked do-not-email are excluded by the consent flags on their record."
      notice={
        <IntegrationNotice
          provider="Email delivery"
          showing="Sends are queued and logged here; no SMTP or transactional provider is wired up yet."
        />
      }
    />
  );
}
