"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { zodResolver } from "@hookform/resolvers/zod";
import { Controller, useForm } from "react-hook-form";
import {
  AlertTriangle,
  Eye,
  EyeOff,
  KeyRound,
  Loader2,
  Lock,
  Mail,
  ShieldCheck,
} from "lucide-react";
import { toast } from "sonner";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  InputOTP,
  InputOTPGroup,
  InputOTPSlot,
} from "@/components/ui/input-otp";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { MicrosoftIcon } from "@/components/auth/sso-icons";
import { CrmLoaderMark } from "@/components/shell/crm-loader";
import {
  ApiError,
  changePassword,
  login,
  verifyOtp,
  type AuthResponse,
} from "@/lib/api";
import { readStoredApp } from "@/lib/active-app";
import { saveSession, setCompanySelectionPending } from "@/lib/session";
import {
  credentialsSchema,
  type CredentialsValues,
} from "@/lib/validations/auth";

const MAX_OTP_ATTEMPTS = 3;
const LOCKOUT_SECONDS = 30;
const RESEND_SECONDS = 30;

type Step = "credentials" | "otp" | "change-password" | "redirecting";

export function LoginForm() {
  const router = useRouter();
  const [step, setStep] = React.useState<Step>("credentials");
  const [showPassword, setShowPassword] = React.useState(false);
  const [maskedContact, setMaskedContact] = React.useState("");
  const [pendingToken, setPendingToken] = React.useState("");
  const [codeDelivered, setCodeDelivered] = React.useState(true);

  // Held only to complete the forced change; the API wants the old password to
  // prove the person at the keyboard is the account's owner.
  const [signedInWith, setSignedInWith] = React.useState("");
  const [nextHref, setNextHref] = React.useState("/dashboard");
  const [newPassword, setNewPassword] = React.useState("");
  const [confirmPassword, setConfirmPassword] = React.useState("");
  const [changing, setChanging] = React.useState(false);
  const [changeError, setChangeError] = React.useState<string | null>(null);

  const [otp, setOtp] = React.useState("");
  const [otpError, setOtpError] = React.useState<string | null>(null);
  const [otpSubmitting, setOtpSubmitting] = React.useState(false);
  const [otpAttempts, setOtpAttempts] = React.useState(0);
  const [lockedUntil, setLockedUntil] = React.useState<number | null>(null);
  const [now, setNow] = React.useState(() => Date.now());
  const [resendAt, setResendAt] = React.useState<number>(0);

  const form = useForm<CredentialsValues>({
    resolver: zodResolver(credentialsSchema),
    defaultValues: { email: "", password: "", remember: true },
    mode: "onBlur",
  });

  React.useEffect(() => {
    if (!lockedUntil && resendAt <= Date.now()) return;
    const id = setInterval(() => {
      const tick = Date.now();
      setNow(tick);
      if (lockedUntil && tick >= lockedUntil) {
        setLockedUntil(null);
        setOtpAttempts(0);
        setOtpError(null);
      }
    }, 1000);
    return () => clearInterval(id);
  }, [lockedUntil, resendAt]);

  const lockRemaining = lockedUntil
    ? Math.max(0, Math.ceil((lockedUntil - now) / 1000))
    : 0;
  const resendRemaining = Math.max(0, Math.ceil((resendAt - now) / 1000));

  async function onSubmitCredentials(values: CredentialsValues) {
    try {
      const {
        pendingToken: token,
        maskedContact: masked,
        delivered,
        session,
      } = await login(values.email, values.password);

      // The server had the code step switched off and signed us in outright.
      // Nothing to verify, so the code screen never appears.
      if (session) {
        setSignedInWith(values.password);
        enter(session);
        return;
      }

      setPendingToken(token);
      setMaskedContact(masked);
      setCodeDelivered(delivered);
      setSignedInWith(values.password);
      setStep("otp");
      setOtp("");
      setOtpAttempts(0);
      setOtpError(null);
      setResendAt(Date.now() + RESEND_SECONDS * 1000);

      if (delivered) {
        toast.success("Verification code sent", {
          description: `A 6-digit code was sent to ${masked}.`,
        });
      } else {
        // Saying "check your email" when no provider is connected leaves the
        // user waiting on a message that is never coming.
        toast.warning("No email provider is connected", {
          description: "The code was written to the server log instead of sent.",
          duration: 8000,
        });
      }
    } catch (error) {
      const message =
        error instanceof ApiError
          ? error.message
          : "Could not reach the server. Please try again.";
      toast.error("Sign-in failed", { description: message });
    }
  }

  /**
   * Takes an issued session and opens the CRM with it.
   *
   * Shared by the two ways a session arrives — passing the code, and the
   * development path where the server asks for none — so that skipping the code
   * step cannot also skip the forced password change or the tenant picker.
   */
  function enter(result: AuthResponse) {
    saveSession(result.accessToken, result.user, result.companies);

    // A platform admin holds a valid token before it has chosen a tenant, so
    // the CRM stays closed until the company picker has been through.
    setCompanySelectionPending(result.requiresCompanySelection);

    const href = result.requiresCompanySelection
      ? "/select-company"
      : readStoredApp().href;

    // An account still on the password somebody else set goes no further
    // until it has one only its owner knows.
    if (result.mustChangePassword) {
      setNextHref(href);
      setStep("change-password");
      return;
    }

    toast.success(`Welcome back, ${result.user.name.split(" ")[0]}`);
    setStep("redirecting");
    router.push(href);
  }

  async function handleVerifyOtp() {
    if (otp.length !== 6 || lockRemaining > 0) return;
    setOtpSubmitting(true);

    try {
      enter(await verifyOtp(pendingToken, otp));
      return;
    } catch (error) {
      const attempts = otpAttempts + 1;
      setOtpAttempts(attempts);
      setOtp("");

      if (attempts >= MAX_OTP_ATTEMPTS) {
        setLockedUntil(Date.now() + LOCKOUT_SECONDS * 1000);
        setOtpError(null);
      } else {
        const message =
          error instanceof ApiError
            ? error.message
            : "Could not reach the server.";
        setOtpError(
          `${message} ${MAX_OTP_ATTEMPTS - attempts} attempt${
            MAX_OTP_ATTEMPTS - attempts === 1 ? "" : "s"
          } remaining.`
        );
      }
    } finally {
      setOtpSubmitting(false);
    }
  }

  function handleResend() {
    if (resendRemaining > 0) return;
    setResendAt(Date.now() + RESEND_SECONDS * 1000);
    setOtp("");
    setOtpError(null);
    toast.info("Code re-sent");
  }

  function handleBackToCredentials() {
    setStep("credentials");
    setPendingToken("");
    setOtp("");
    setOtpError(null);
    setOtpAttempts(0);
    setLockedUntil(null);
  }

  async function submitNewPassword() {
    setChangeError(null);

    if (newPassword.length < 10) {
      setChangeError("Use at least 10 characters.");
      return;
    }

    if (newPassword !== confirmPassword) {
      setChangeError("The two passwords do not match.");
      return;
    }

    setChanging(true);
    try {
      await changePassword(signedInWith, newPassword);
      toast.success("Password changed");
      setStep("redirecting");
      router.push(nextHref);
    } catch (error) {
      setChangeError(
        error instanceof ApiError
          ? error.message
          : "Could not reach the server. Please try again."
      );
    } finally {
      setChanging(false);
    }
  }

  if (step === "change-password") {
    return (
      <div className="flex flex-col gap-5">
        <div className="flex flex-col gap-1.5">
          <h2 className="text-[19px] font-semibold tracking-tight">
            Choose your own password
          </h2>
          <p className="text-[13px] text-muted-foreground">
            This account is still on the password it was created with, which
            somebody else knows. Pick one only you know to carry on.
          </p>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="new-password" className="text-[13px]">
            New password
          </Label>
          <Input
            id="new-password"
            type="password"
            autoFocus
            autoComplete="new-password"
            value={newPassword}
            onChange={(event) => setNewPassword(event.target.value)}
            placeholder="At least 10 characters"
          />
          <p className="text-[11.5px] text-muted-foreground">
            Longer beats complicated — a phrase you can remember is stronger
            than a short word with symbols in it.
          </p>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="confirm-password" className="text-[13px]">
            Confirm new password
          </Label>
          <Input
            id="confirm-password"
            type="password"
            autoComplete="new-password"
            value={confirmPassword}
            onChange={(event) => setConfirmPassword(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter") submitNewPassword();
            }}
          />
        </div>

        {changeError ? (
          <p className="text-[12.5px] text-destructive">{changeError}</p>
        ) : null}

        <Button onClick={submitNewPassword} disabled={changing} className="w-full">
          {changing ? <Loader2 className="size-4 animate-spin" /> : null}
          Set password and continue
        </Button>
      </div>
    );
  }

  if (step === "redirecting") {
    return (
      <div className="flex flex-col items-center gap-3 py-8 text-center">
        <CrmLoaderMark size={64} />
        <p className="text-sm text-muted-foreground">
          Preparing your workspace&hellip;
        </p>
      </div>
    );
  }

  if (step === "otp") {
    return (
      <div className="flex flex-col gap-5">
        <div className="space-y-1">
          <div className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
            <span className="flex size-4 items-center justify-center rounded-full bg-primary text-[10px] text-primary-foreground">
              2
            </span>
            Step 2 of 2 &middot; Verification
          </div>
          <h2 className="text-lg font-semibold tracking-tight">
            Enter verification code
          </h2>
          <p className="text-sm text-muted-foreground">
            We sent a 6-digit code to{" "}
            <span className="font-medium text-foreground">
              {maskedContact}
            </span>
          </p>
        </div>

        {lockRemaining > 0 ? (
          <Alert variant="destructive">
            <AlertTriangle />
            <AlertTitle>Too many failed attempts</AlertTitle>
            <AlertDescription>
              This step is locked for {lockRemaining}s to protect the
              account.
            </AlertDescription>
          </Alert>
        ) : null}

        <div className="flex flex-col items-center gap-2">
          <InputOTP
            maxLength={6}
            value={otp}
            onChange={setOtp}
            disabled={lockRemaining > 0 || otpSubmitting}
            onComplete={handleVerifyOtp}
          >
            <InputOTPGroup aria-invalid={!!otpError}>
              {[0, 1, 2, 3, 4, 5].map((i) => (
                <InputOTPSlot key={i} index={i} />
              ))}
            </InputOTPGroup>
          </InputOTP>
          {otpError ? (
            <p className="text-xs text-destructive">{otpError}</p>
          ) : null}
        </div>

        <Button
          onClick={handleVerifyOtp}
          disabled={otp.length !== 6 || lockRemaining > 0 || otpSubmitting}
          className="w-full"
          size="lg"
        >
          {otpSubmitting ? (
            <Loader2 className="animate-spin" />
          ) : (
            <ShieldCheck />
          )}
          Verify &amp; continue
        </Button>

        <div className="flex items-center justify-between text-xs">
          <button
            type="button"
            onClick={handleBackToCredentials}
            className="font-medium text-muted-foreground hover:text-foreground"
          >
            &larr; Use a different account
          </button>
          <button
            type="button"
            onClick={handleResend}
            disabled={resendRemaining > 0}
            className="font-medium text-primary hover:underline disabled:pointer-events-none disabled:text-muted-foreground"
          >
            {resendRemaining > 0
              ? `Resend code in ${resendRemaining}s`
              : "Resend code"}
          </button>
        </div>
      </div>
    );
  }

  return (
    <form
      onSubmit={(event) => {
        void form.handleSubmit(onSubmitCredentials)(event);
      }}
      className="flex flex-col gap-4"
      noValidate
    >
      <div className="space-y-1">
        <Label htmlFor="email">Work email</Label>
        <div className="relative">
          <Mail className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            id="email"
            type="email"
            placeholder="you@company.com"
            autoComplete="email"
            className="pl-8"
            aria-invalid={!!form.formState.errors.email}
            {...form.register("email")}
          />
        </div>
        {form.formState.errors.email ? (
          <p className="text-xs text-destructive">
            {form.formState.errors.email.message}
          </p>
        ) : null}
      </div>

      <div className="space-y-1">
        <div className="flex items-center justify-between">
          <Label htmlFor="password">Password</Label>
          <Link
            href="/forgot-password"
            className="text-xs font-medium text-primary hover:underline"
          >
            Forgot password?
          </Link>
        </div>
        <div className="relative">
          <Lock className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            id="password"
            type={showPassword ? "text" : "password"}
            placeholder="Enter your password"
            autoComplete="current-password"
            className="px-8"
            aria-invalid={!!form.formState.errors.password}
            {...form.register("password")}
          />
          <button
            type="button"
            onClick={() => setShowPassword((v) => !v)}
            className="absolute top-1/2 right-2.5 -translate-y-1/2 text-muted-foreground hover:text-foreground"
            aria-label={showPassword ? "Hide password" : "Show password"}
          >
            {showPassword ? (
              <EyeOff className="size-4" />
            ) : (
              <Eye className="size-4" />
            )}
          </button>
        </div>
        {form.formState.errors.password ? (
          <p className="text-xs text-destructive">
            {form.formState.errors.password.message}
          </p>
        ) : null}
      </div>

      <div className="flex items-center gap-2">
        <Controller
          control={form.control}
          name="remember"
          render={({ field }) => (
            <Checkbox
              id="remember"
              checked={field.value}
              onCheckedChange={(checked) => field.onChange(checked === true)}
            />
          )}
        />
        <Label
          htmlFor="remember"
          className="text-sm font-normal text-muted-foreground"
        >
          Keep me signed in on this device
        </Label>
      </div>

      <Button
        type="submit"
        size="lg"
        className="w-full"
        disabled={form.formState.isSubmitting}
      >
        {form.formState.isSubmitting ? (
          <Loader2 className="animate-spin" />
        ) : (
          <KeyRound />
        )}
        Sign in
      </Button>

      <div className="flex items-center gap-3">
        <Separator className="flex-1" />
        <span className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
          Or continue with
        </span>
        <Separator className="flex-1" />
      </div>

      <Button type="button" variant="outline" className="w-full">
        <MicrosoftIcon />
        Microsoft
      </Button>
    </form>
  );
}
