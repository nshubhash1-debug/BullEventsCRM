"use client";

import { useTypewriterLoop } from "@/hooks/use-typewriter-loop";
import { cn } from "@/lib/utils";

const TAGLINES = [
  "Every branch. Every deal. One view.",
  "Every lead. Every visit. Tracked.",
  "One platform. Every branch synced.",
  "Leads to bookings — fully connected.",
];

export function RotatingHeadline({ className }: { className?: string }) {
  const { text } = useTypewriterLoop(TAGLINES, {
    typingSpeed: 42,
    deletingSpeed: 22,
    pauseDuration: 2400,
  });

  return (
    <div className="min-h-[6.5rem]">
      <h1 className={cn(className)}>
        {text}
        <span aria-hidden className="animate-pulse">
          &#124;
        </span>
      </h1>
    </div>
  );
}
