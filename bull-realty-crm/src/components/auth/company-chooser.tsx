"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import {
  ArrowLeft,
  Building2,
  Check,
  LogOut,
  MapPinned,
  Search,
  UsersRound,
} from "lucide-react";
import { toast } from "sonner";

import { BrandMark } from "@/components/auth/brand-mark";
import { CrmSplash } from "@/components/shell/crm-loader";
import { ThemeToggle } from "@/components/theme/theme-toggle";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { useHasMounted } from "@/hooks/use-has-mounted";
import { readStoredApp } from "@/lib/active-app";
import {
  ApiError,
  getSelectableCompanies,
  selectCompany,
  type CompanyOption,
} from "@/lib/api";
import {
  clearSession,
  isCompanySelectionPending,
  readCompanies,
  readSession,
  saveCompanies,
  saveSession,
  setCompanySelectionPending,
} from "@/lib/session";
import { cn } from "@/lib/utils";

/**
 * The tenant gate a platform admin passes through after the second factor.
 *
 * Choosing here is a token swap, not a client-side filter: the API re-issues the
 * access token against the chosen company, and every query the CRM makes from
 * then on is scoped by that token server-side. The same screen doubles as the
 * switcher, so there is one code path for "which company am I in" rather than
 * two that can disagree.
 */
export function CompanyChooser() {
  const router = useRouter();
  const hasMounted = useHasMounted();

  // Storage is read during render behind the mount flag rather than copied into
  // state from an effect, which keeps the hydrating render identical to the
  // server's — the same approach SessionProvider takes.
  const session = hasMounted ? readSession() : null;
  const pending = hasMounted ? isCompanySelectionPending() : false;
  const stored = hasMounted ? readCompanies() : [];

  const [refreshed, setRefreshed] = React.useState<CompanyOption[] | null>(null);
  const [openingId, setOpeningId] = React.useState<number | null>(null);
  const [busy, setBusy] = React.useState(false);
  const [search, setSearch] = React.useState("");

  const signedOut = hasMounted && session === null;

  React.useEffect(() => {
    if (signedOut) router.replace("/login");
  }, [signedOut, router]);

  React.useEffect(() => {
    if (!hasMounted || signedOut) return;

    let cancelled = false;

    getSelectableCompanies()
      .then((list) => {
        if (cancelled) return;
        setRefreshed(list);
        saveCompanies(list);
      })
      .catch((error: unknown) => {
        if (cancelled) return;
        if (error instanceof ApiError && error.status === 401) {
          clearSession();
          router.replace("/login");
          return;
        }
        toast.error("Could not refresh the company list", {
          description:
            error instanceof ApiError ? error.message : "Network error.",
        });
      });

    return () => {
      cancelled = true;
    };
  }, [hasMounted, signedOut, router]);

  const companies = refreshed ?? (stored.length > 0 ? stored : null);

  async function open(company: CompanyOption) {
    if (busy) return;
    setOpeningId(company.id);
    setBusy(true);

    try {
      const result = await selectCompany(company.id);
      saveSession(result.accessToken, result.user, result.companies);
      setCompanySelectionPending(false);
      router.replace(readStoredApp().href);
    } catch (error) {
      setOpeningId(null);
      setBusy(false);
      toast.error(`Could not open ${company.name}`, {
        description:
          error instanceof ApiError ? error.message : "Network error.",
      });
    }
  }

  function signOut() {
    clearSession();
    router.replace("/login");
  }

  if (!hasMounted || signedOut) {
    return <CrmSplash label="Checking your session" />;
  }

  // The splash stays up through the route change, so the CRM never appears
  // half-populated with the previous tenant's data behind it.
  if (busy) {
    const target = companies?.find((entry) => entry.id === openingId);
    return (
      <CrmSplash
        label={target ? `Opening ${target.name}` : "Opening your workspace"}
        detail="Loading leads, inventory and dashboards"
      />
    );
  }

  const needle = search.trim().toLowerCase();
  const visible = (companies ?? []).filter(
    (company) =>
      !needle ||
      `${company.name} ${company.slug} ${company.planTier}`
        .toLowerCase()
        .includes(needle)
  );

  return (
    <div className="flex min-h-svh flex-col bg-background">
      <header className="flex items-center justify-between gap-3 border-b bg-card px-5 py-3.5">
        <BrandMark variant="light" />
        <div className="flex items-center gap-1.5">
          <ThemeToggle />
          <Button
            variant="ghost"
            size="sm"
            onClick={signOut}
            className="text-muted-foreground"
          >
            <LogOut className="size-4" />
            Sign out
          </Button>
        </div>
      </header>

      <main className="mx-auto w-full max-w-4xl flex-1 px-5 py-10">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div className="space-y-1.5">
            <h1 className="text-2xl font-semibold tracking-tight">
              {pending ? "Choose a company" : "Switch company"}
            </h1>
            <p className="max-w-xl text-sm text-muted-foreground">
              {session ? `Signed in as ${session.user.name}. ` : ""}
              Every company keeps its own branches, users, venues and
              pipeline — nothing crosses between them.
            </p>
          </div>

          {pending ? null : (
            <Button
              variant="outline"
              size="sm"
              onClick={() => router.replace(readStoredApp().href)}
            >
              <ArrowLeft className="size-4" />
              Back to the CRM
            </Button>
          )}
        </div>

        {(companies?.length ?? 0) > 6 ? (
          <div className="relative mt-6 max-w-sm">
            <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Company name, slug or plan…"
              className="h-9 pl-8"
            />
          </div>
        ) : null}

        <div className="mt-6 grid gap-3 sm:grid-cols-2">
          {companies === null
            ? [0, 1, 2, 3].map((index) => (
                <Skeleton key={index} className="h-[112px] w-full rounded-md" />
              ))
            : visible.map((company) => (
                <CompanyCard
                  key={company.id}
                  company={company}
                  current={!pending && company.id === session?.user.companyId}
                  onOpen={() => void open(company)}
                />
              ))}
        </div>

        {companies !== null && visible.length === 0 ? (
          <p className="mt-6 text-sm text-muted-foreground">
            {companies.length === 0
              ? "No companies are set up yet."
              : `No company matches “${search}”.`}
          </p>
        ) : null}
      </main>
    </div>
  );
}

function CompanyCard({
  company,
  current,
  onOpen,
}: {
  company: CompanyOption;
  current: boolean;
  onOpen: () => void;
}) {
  const suspended = company.status.toLowerCase() === "suspended";

  return (
    <button
      type="button"
      onClick={onOpen}
      disabled={suspended}
      // The card is a grid of badges and counts, so the name is stated
      // explicitly rather than left to be stitched together from its contents.
      aria-label={`Open ${company.name}`}
      title={
        suspended
          ? `${company.name} is suspended and cannot be opened.`
          : `Open ${company.name}`
      }
      className={cn(
        "flex items-start gap-3 rounded-md border bg-card p-4 text-left transition-colors",
        "disabled:cursor-not-allowed disabled:opacity-60",
        current
          ? "border-primary/50 bg-accent"
          : "hover:border-primary/40 hover:bg-accent/50"
      )}
    >
      <span className="flex size-10 shrink-0 items-center justify-center rounded bg-primary/10 text-primary">
        <Building2 className="size-5" />
      </span>

      <span className="min-w-0 flex-1">
        <span className="flex flex-wrap items-center gap-1.5">
          <span className="truncate text-[14px] font-semibold">
            {company.name}
          </span>
          {current ? (
            <Badge
              variant="outline"
              className="h-4 gap-1 px-1 text-[9px] font-normal"
            >
              <Check className="size-2.5" />
              Current
            </Badge>
          ) : null}
          {company.isHome && !current ? (
            <Badge variant="outline" className="h-4 px-1 text-[9px] font-normal">
              Home
            </Badge>
          ) : null}
        </span>

        <span className="mt-0.5 block truncate text-[11px] text-muted-foreground">
          {company.slug}
        </span>

        <span className="mt-2.5 flex flex-wrap items-center gap-2 text-[11px] text-muted-foreground">
          <Badge variant="secondary" className="h-4 px-1.5 text-[10px]">
            {company.planTier}
          </Badge>
          <span
            className={cn(
              "flex items-center gap-1",
              suspended
                ? "text-destructive"
                : "text-emerald-600 dark:text-emerald-400"
            )}
          >
            <span className="size-1.5 rounded-full bg-current" />
            {company.status}
          </span>
          <span className="flex items-center gap-1" title="Branches">
            <MapPinned className="size-3" />
            {company.branchCount}
          </span>
          <span className="flex items-center gap-1" title="Users">
            <UsersRound className="size-3" />
            {company.userCount}
          </span>
        </span>
      </span>
    </button>
  );
}
