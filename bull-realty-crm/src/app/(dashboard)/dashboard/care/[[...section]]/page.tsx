import type { Metadata } from "next";

import { AppScaffold } from "@/components/shell/app-scaffold";

export const metadata: Metadata = {
  title: "Customer Care",
};

/**
 * One optional catch-all serves every module in this app: each destination is
 * already declared in the navigation config, so a route file per module would
 * only duplicate that list and drift from it.
 */
export default function Page() {
  return <AppScaffold appId="customer-care" />;
}
