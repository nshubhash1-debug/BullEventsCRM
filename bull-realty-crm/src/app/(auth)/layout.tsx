import type { ReactNode } from "react";

import { AuthBrandPanel } from "@/components/auth/auth-brand-panel";
import { ThemeToggle } from "@/components/theme/theme-toggle";

export default function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="grid min-h-svh grid-rows-[1fr] lg:grid-cols-[1.05fr_1fr]">
      <AuthBrandPanel />
      <div className="relative flex flex-col justify-center overflow-y-auto px-6 py-10 sm:px-10 lg:px-16">
        <div className="absolute top-6 right-6">
          <ThemeToggle />
        </div>
        <div className="mx-auto w-full max-w-sm">{children}</div>
      </div>
    </div>
  );
}
