"use client";

import * as React from "react";
import { Megaphone } from "lucide-react";
import { toast } from "sonner";

import { PagePanel } from "@/components/shell/page-panel";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { hrx } from "@/lib/hrms-api";

export default function HrPoliciesPage() {
  const [policies, setPolicies] = React.useState<Awaited<ReturnType<typeof hrx.policies>>>([]);
  const [news, setNews] = React.useState<Awaited<ReturnType<typeof hrx.announcements>>>([]);
  const [title, setTitle] = React.useState("");
  const [body, setBody] = React.useState("");
  const [headline, setHeadline] = React.useState("");
  const [copy, setCopy] = React.useState("");

  const load = React.useCallback(() => {
    hrx.policies().then(setPolicies).catch(() => setPolicies([]));
    hrx.announcements().then(setNews).catch(() => setNews([]));
  }, []);
  React.useEffect(() => {
    load();
  }, [load]);

  return (
    <PagePanel
      icon={Megaphone}
      title="Policies & news"
      hint="Company handbook plus pin-able announcements — IceHrm / Horilla knowledge base pattern."
    >
      <div className="grid gap-8 lg:grid-cols-2">
        <section>
          <h2 className="mb-2 text-[12px] font-semibold uppercase text-muted-foreground">Policies</h2>
          <div className="mb-3 flex flex-wrap gap-2">
            <Input className="h-9 w-48" placeholder="Title" value={title} onChange={(e) => setTitle(e.target.value)} />
            <Input className="h-9 flex-1" placeholder="Body" value={body} onChange={(e) => setBody(e.target.value)} />
            <Button
              size="sm"
              className="h-9"
              onClick={() =>
                void hrx
                  .createPolicy({
                    title,
                    category: "General",
                    body,
                    effectiveFrom: new Date().toISOString().slice(0, 10),
                  })
                  .then(() => {
                    setTitle("");
                    setBody("");
                    toast.success("Published");
                    load();
                  })
              }
            >
              Publish
            </Button>
          </div>
          <ul className="space-y-3">
            {policies.map((p) => (
              <li key={p.id} className="rounded-xl border p-3">
                <p className="text-[13px] font-semibold">
                  {p.title}{" "}
                  <span className="text-[11px] font-normal text-muted-foreground">{p.category}</span>
                </p>
                <p className="mt-1 text-[13px] text-muted-foreground">{p.body}</p>
              </li>
            ))}
          </ul>
        </section>
        <section>
          <h2 className="mb-2 text-[12px] font-semibold uppercase text-muted-foreground">Announcements</h2>
          <div className="mb-3 flex flex-wrap gap-2">
            <Input className="h-9 w-48" placeholder="Headline" value={headline} onChange={(e) => setHeadline(e.target.value)} />
            <Input className="h-9 flex-1" placeholder="Message" value={copy} onChange={(e) => setCopy(e.target.value)} />
            <Button
              size="sm"
              variant="outline"
              className="h-9"
              onClick={() =>
                void hrx
                  .createAnnouncement({ title: headline, body: copy, audience: "All" })
                  .then(() => {
                    setHeadline("");
                    setCopy("");
                    load();
                  })
              }
            >
              Post
            </Button>
          </div>
          <ul className="space-y-3">
            {news.map((n) => (
              <li key={n.id} className="rounded-xl border bg-violet-500/5 p-3">
                <p className="text-[13px] font-semibold">{n.title}</p>
                <p className="mt-1 text-[13px] text-muted-foreground">{n.body}</p>
              </li>
            ))}
          </ul>
        </section>
      </div>
    </PagePanel>
  );
}
