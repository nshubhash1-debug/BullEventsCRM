import type { Metadata } from "next";

import { BrandMark } from "@/components/auth/brand-mark";
import { LoginForm } from "@/components/auth/login-form";

export const metadata: Metadata = {
  title: "Sign in",
};

export default function LoginPage() {
  return (
    <div className="flex flex-col gap-8">
      <div className="lg:hidden">
        <BrandMark variant="light" />
      </div>

      <div className="space-y-1.5">
        <h1 className="text-2xl font-semibold tracking-tight">
          Welcome back
        </h1>
        <p className="text-sm text-muted-foreground">
          Access your CRM to manage leads, inventory and deals across
          every branch.
        </p>
      </div>

      <LoginForm />

      <p className="text-center text-xs text-balance text-muted-foreground">
        Secured with role-based access control and full audit logging.
        Need an account?{" "}
        <a
          href="mailto:admin@bullrealtyglobal.com"
          className="font-medium text-primary hover:underline"
        >
          Contact your administrator
        </a>
      </p>
    </div>
  );
}
