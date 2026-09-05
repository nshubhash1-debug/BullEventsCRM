"use client";

import type { ReactNode } from "react";
import { usePathname } from "next/navigation";

import { AppNavBar } from "@/components/shell/app-nav-bar";
import { GlobalHeader } from "@/components/shell/global-header";
import { SetupShell } from "@/components/shell/setup-shell";
import { appForPathname } from "@/lib/app-nav-config";

/**
 * Global header, context bar, and the page below them.
 *
 * The working apps deliberately have no left rail: every module's destinations
 * already live in its tab dropdown, so a rail repeating them cost ~224px of
 * horizontal space and gave the dense list views two places to disagree about
 * what is active.
 *
 * The admin console is the exception, and it earns it. Setup is not a short bar
 * of tabs somebody works all day — it is a deep tree visited a few times a
 * month by someone who knows the name of what they want and not where it lives.
 * That shape needs a rail and a search box, which is why Salesforce splits the
 * two shells too rather than forcing one to serve both.
 */
export function AppShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const isSetup = appForPathname(pathname).id === "admin-console";

  return (
    <div className="flex min-h-svh flex-col bg-background">
      <div className="sticky top-0 z-30">
        <GlobalHeader />
        <AppNavBar />
      </div>
      <main className="flex min-h-0 min-w-0 flex-1 flex-col gap-4 p-3 sm:p-4 lg:p-5">
        {isSetup ? <SetupShell>{children}</SetupShell> : children}
      </main>
    </div>
  );
}
