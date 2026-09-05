"use client";

import { useTypewriterLoop } from "@/hooks/use-typewriter-loop";
import { cn } from "@/lib/utils";

const TAGLINES = [
  "Every event. Every crew. One view.",
  "Enquiry to load-out — fully connected.",
  "Every prop, every truck, accounted for.",
  "One platform. Every venue, every date.",
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
