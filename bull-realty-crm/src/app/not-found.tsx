"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ArrowLeft, Compass, Hammer, SearchX } from "lucide-react";

import { PagePanel } from "@/components/shell/page-panel";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { moduleByHref } from "@/lib/app-nav-config";
import { SETUP_TREE } from "@/lib/setup-nav";

/**
 * What a dashboard URL that resolves to nothing shows.
 *
 * Most of these are not mistakes: the navigation already lists a number of
 * destinations that are declared and findable but not built, and every one of
 * them is a real URL somebody can type, bookmark or be sent in a message. A
 * bare 404 tells that person the product is broken, when the honest answer is
 * "this part is not finished yet" — so the page looks the destination up and
 * says which of the two it is.
 */
export default function NotFound() {
  const pathname = usePathname();
  const known = describe(pathname);

  // Next only consults the root not-found for a URL that matched no route, so
  // this renders outside the dashboard shell and has to bring its own frame.
  return (
    <main className="mx-auto flex min-h-screen w-full max-w-3xl flex-col justify-center p-5">
      {known?.planned ? <Planned known={known} /> : <Missing pathname={pathname} />}
    </main>
  );
}

function Planned({ known }: { known: Described }) {
  {
    return (
      <PagePanel
        icon={Hammer}
        title={known.title}
        hint="Declared in navigation, not built yet."
        actions={
          <Badge
            variant="outline"
            className="h-5 gap-1.5 px-2 text-[11px] font-normal text-amber-700 dark:text-amber-400"
          >
            <Hammer className="size-3" />
            Not built yet
          </Badge>
        }
      >
        <div className="mx-auto flex w-full max-w-2xl flex-col gap-5 py-6">
          <section className="rounded-md border bg-muted/30 p-4">
            <h2 className="flex items-center gap-2 text-[13px] font-semibold">
              <Compass className="size-4 text-primary" />
              What {known.title} will hold
            </h2>

            {known.blueprint.length > 0 ? (
              <ul className="mt-3 space-y-1.5">
                {known.blueprint.map((line) => (
                  <li key={line} className="flex gap-2.5 text-[13px] text-foreground/85">
                    <span className="mt-[7px] size-1.5 shrink-0 rounded-full bg-primary/60" />
                    {line}
                  </li>
                ))}
              </ul>
            ) : null}
          </section>

          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline" size="sm">
              <Link href="/dashboard">
                <ArrowLeft className="size-3.5" /> Back to the dashboard
              </Link>
            </Button>
            <Button asChild size="sm">
              <Link href="/dashboard/setup">Open Setup</Link>
            </Button>
          </div>
        </div>
      </PagePanel>
    );
  }
}

function Missing({ pathname }: { pathname: string }) {
  return (
    <PagePanel icon={SearchX} title="That page does not exist">
      <div className="mx-auto flex w-full max-w-md flex-col items-center gap-4 py-14 text-center">
        <p className="text-[13px] text-muted-foreground">
          Nothing is served at{" "}
          <code className="rounded bg-muted px-1.5 py-0.5 text-[12px]">{pathname}</code>.
          The link may be old, or the address may have a typo in it.
        </p>

        <div className="flex flex-wrap justify-center gap-2">
          <Button asChild variant="outline" size="sm">
            <Link href="/dashboard">
              <ArrowLeft className="size-3.5" /> Back to the dashboard
            </Link>
          </Button>
          <Button asChild size="sm">
            <Link href="/dashboard/setup">Open Setup</Link>
          </Button>
        </div>
      </div>
    </PagePanel>
  );
}

/**
 * Looks a dead URL up in the two navigation configs.
 *
 * A plain function rather than a memo: it is two short scans over arrays the
 * module already holds, and it runs once, on a page nobody stays on.
 */
interface Described {
  title: string;
  blueprint: string[];
  planned: boolean;
}

function describe(pathname: string): Described | null {
  const found = moduleByHref(pathname);

  if (found) {
    return {
      title: found.title,
      blueprint: found.blueprint ?? [],
      planned: Boolean(found.comingSoon) || (found.blueprint?.length ?? 0) > 0,
    };
  }

  for (const section of SETUP_TREE) {
    const item = section.items.find((entry) => entry.href.split("?")[0] === pathname);

    if (item) {
      return {
        title: item.title,
        blueprint: [item.description],
        planned: Boolean(item.soon),
      };
    }
  }

  return null;
}
