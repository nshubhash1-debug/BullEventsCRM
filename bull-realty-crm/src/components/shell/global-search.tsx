"use client";

import * as React from "react";
import { useRouter } from "next/navigation";

import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
  CommandShortcut,
} from "@/components/ui/command";
import { appById, navigableModules } from "@/lib/app-nav-config";

/** Demo records surfaced in search until a real search endpoint exists. */
const recentRecords = [
  { name: "Kabir Sethi", meta: "Lead · Booked", href: "/dashboard/leads" },
  { name: "Divya Rao", meta: "Lead · Site Visit", href: "/dashboard/leads" },
  { name: "Vikram Malhotra", meta: "Lead · New", href: "/dashboard/leads" },
  { name: "Mumbai HQ", meta: "Branch · Active", href: "/dashboard/branches" },
  {
    name: "Priya Sharma",
    meta: "User · Company Admin",
    href: "/dashboard/users",
  },
];

export function GlobalSearch({ trigger }: { trigger: React.ReactNode }) {
  const router = useRouter();
  const [open, setOpen] = React.useState(false);

  React.useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      const target = event.target as HTMLElement | null;
      const typingInField =
        target instanceof HTMLInputElement ||
        target instanceof HTMLTextAreaElement ||
        target?.isContentEditable;

      if (event.key === "k" && (event.metaKey || event.ctrlKey)) {
        event.preventDefault();
        setOpen((prev) => !prev);
        return;
      }

      if (event.key === "/" && !typingInField) {
        event.preventDefault();
        setOpen(true);
      }
    }

    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, []);

  function go(href: string) {
    setOpen(false);
    router.push(href);
  }

  return (
    <>
      <span onClick={() => setOpen(true)}>{trigger}</span>

      <CommandDialog
        open={open}
        onOpenChange={setOpen}
        title="Global search"
        description="Search records and modules"
        className="sm:max-w-xl"
      >
        <CommandInput placeholder="Search leads, branches, users, modules…" />
        <CommandList>
          <CommandEmpty>No results found.</CommandEmpty>

          <CommandGroup heading="Recent records">
            {recentRecords.map((record) => (
              <CommandItem
                key={record.name}
                value={`${record.name} ${record.meta}`}
                onSelect={() => go(record.href)}
              >
                <span className="flex-1 truncate">{record.name}</span>
                <CommandShortcut>{record.meta}</CommandShortcut>
              </CommandItem>
            ))}
          </CommandGroup>

          <CommandSeparator />

          <CommandGroup heading="Modules">
            {/*
              Keyed and searched on the href, not the title. Five apps each
              register a module called "Overview", so a title key collides —
              React was dropping four of them — and a title search value left
              five identical rows nobody could tell apart. The owning app is
              printed beside each so the list stays readable.
            */}
            {navigableModules().map((module) => {
              const owner = module.app ? appById(module.app).title : "Administration";

              return (
                <CommandItem
                  key={module.href}
                  value={`${module.title} ${owner}`}
                  onSelect={() => go(module.href)}
                >
                  <module.icon className="size-4 text-muted-foreground" />
                  <span className="flex-1 truncate">{module.title}</span>
                  <CommandShortcut>{owner}</CommandShortcut>
                </CommandItem>
              );
            })}
          </CommandGroup>
        </CommandList>
      </CommandDialog>
    </>
  );
}
