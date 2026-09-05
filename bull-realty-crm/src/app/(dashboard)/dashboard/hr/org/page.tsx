"use client";

import * as React from "react";
import { Network } from "lucide-react";

import { PagePanel } from "@/components/shell/page-panel";
import { hrx, type HrOrgNode } from "@/lib/hrms-api";

function Node({ node }: { node: HrOrgNode }) {
  return (
    <div className="flex flex-col items-center">
      <div className="min-w-[160px] rounded-xl border bg-card px-4 py-3 text-center shadow-sm">
        <p className="text-[13px] font-semibold">{node.name}</p>
        <p className="text-[11px] text-muted-foreground">{node.title}</p>
        <p className="text-[10px] uppercase tracking-wide text-primary/80">{node.department}</p>
        <p className="mt-1 font-mono text-[10px] text-muted-foreground">{node.employeeCode}</p>
      </div>
      {node.children.length ? (
        <div className="mt-4 flex flex-wrap justify-center gap-8 border-t pt-4">
          {node.children.map((child) => (
            <Node key={child.id} node={child} />
          ))}
        </div>
      ) : null}
    </div>
  );
}

export default function HrOrgPage() {
  const [roots, setRoots] = React.useState<HrOrgNode[]>([]);
  React.useEffect(() => {
    hrx.org().then(setRoots).catch(() => setRoots([]));
  }, []);

  return (
    <PagePanel
      icon={Network}
      title="Organisation"
      hint="Live reporting tree from the employee master — same idea as Horilla’s employee module, without a second HR database."
    >
      <div className="overflow-x-auto pb-8 pt-4">
        <div className="flex min-w-max justify-center gap-10">
          {roots.map((n) => (
            <Node key={n.id} node={n} />
          ))}
        </div>
        {!roots.length ? (
          <p className="text-[13px] text-muted-foreground">No people on rolls yet.</p>
        ) : null}
      </div>
    </PagePanel>
  );
}
