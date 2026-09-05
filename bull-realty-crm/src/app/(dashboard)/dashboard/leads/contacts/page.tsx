"use client";

import { Contact } from "lucide-react";

import { ContactsView } from "@/components/crm/contacts-view";

export default function ContactsPage() {
  return (
    <ContactsView
      icon={Contact}
      title="Contacts"
      storageKey="contacts"
      hint="Every person the company deals with. A lead becomes a contact on conversion and stays here for the rest of the relationship, however many opportunities they run through."
    />
  );
}
