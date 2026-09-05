"use client";

import { Database } from "lucide-react";

import { ContactsView } from "@/components/crm/contacts-view";
import { emptyRoot, type FilterNode } from "@/lib/query";

/**
 * The same object as Contacts, narrowed to people who have actually bought.
 *
 * Declared at module scope so the reference stays stable across renders — the
 * list hook keys its query off this value.
 */
const CUSTOMERS_ONLY: FilterNode = {
  ...emptyRoot(),
  children: [
    {
      key: "scope-customers",
      field: "type",
      operator: "in",
      // Matches ContactTypes on the API — Investor/Owner were realty leftovers.
      values: ["Customer", "Corporate"],
    },
  ],
};

export default function CustomerDatabasePage() {
  return (
    <ContactsView
      icon={Database}
      title="Customer database"
      storageKey="customers"
      scope={CUSTOMERS_ONLY}
      hint="Clients and corporate bookers who have booked — the base for repeat events, referrals and lifetime-value analysis. Segments are assigned by a KMeans model trained on this workspace's own value and recency data."
    />
  );
}
