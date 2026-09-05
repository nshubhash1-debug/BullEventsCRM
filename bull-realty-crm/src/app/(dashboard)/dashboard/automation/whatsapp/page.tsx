"use client";

import { MessageCircle } from "lucide-react";

import { FollowUpsView, channelScope } from "@/components/crm/follow-ups-view";
import { IntegrationNotice } from "@/components/crm/integration-notice";

const WHATSAPP_ONLY = channelScope("WhatsApp");

export default function WhatsAppAutomationPage() {
  return (
    <FollowUpsView
      icon={MessageCircle}
      title="WhatsApp"
      storageKey="automation-whatsapp"
      scope={WHATSAPP_ONLY}
      singleChannel
      hint="Every WhatsApp touchpoint scheduled against a lead, contact or deal. Contacts who have opted out are excluded from the queue by the consent flags on their record."
      notice={
        <IntegrationNotice
          provider="WhatsApp Business API"
          showing="Messages are queued and logged here, but nothing is sent automatically until a provider is connected."
        />
      }
    />
  );
}
