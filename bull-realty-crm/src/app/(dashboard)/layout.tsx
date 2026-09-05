import type { ReactNode } from "react";

import { SessionProvider } from "@/components/dashboard/session-provider";
import { AppShell } from "@/components/shell/app-shell";

export default function DashboardLayout({
  children,
}: {
  children: ReactNode;
}) {
  return (
    <SessionProvider>
      <AppShell>{children}</AppShell>
    </SessionProvider>
  );
}
