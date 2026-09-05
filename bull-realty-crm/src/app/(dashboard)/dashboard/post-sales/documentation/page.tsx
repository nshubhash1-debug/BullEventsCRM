import type { Metadata } from "next";

import { AppScaffold } from "@/components/shell/app-scaffold";

export const metadata: Metadata = {
  title: "Documentation",
};

/**
 * A route file per unbuilt module rather than one catch-all.
 *
 * Post Sales has real screens of its own, so its root needs a `page.tsx` — and
 * a required catch-all beside it (`[...section]`) collides with the optional
 * ones the other apps use (`[[...section]]`). Next refuses the pair across the
 * whole tree, which took HR, Customer Care and Construction down with it. Two
 * small files are cheaper than that.
 */
export default function Page() {
  return <AppScaffold appId="post-sales" />;
}
