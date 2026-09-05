"use client";

import * as React from "react";
import { Sparkles } from "lucide-react";

import { useTypewriterLoop } from "@/hooks/use-typewriter-loop";

const FACTS = [
  "Couples usually book the planner who replies first — under an hour beats next-day by a wide margin.",
  "A questionnaire before the consult means the first call starts informed, not exploratory.",
  "Weddings stall most often after the contract goes out, not after the first enquiry.",
  "An enquiry with no event date is a conversation, not a lead you can staff.",
  "Portal leads arrive already shopping three competitors — treat them differently from referrals.",
  "Follow-up is a sequence on every stage, not a column leads go to die in.",
  "Date flexibility is the difference between steering a couple and losing the weekend.",
  "Guest count and meal preference at enquiry time are what make a quote honest.",
  "Vendor-partner referrals are among the cheapest, highest-converting wedding leads.",
  "Nurture a date that is 18 months out instead of marking it lost.",
  "One timeline — calls, WhatsApp, consults, proposals — stops the desk repeating itself.",
  "Teams that track source ROI put ad spend back into the portals that actually book.",
];

function getDayIndex() {
  return Math.floor(Date.now() / 86_400_000);
}

function subscribeToDayChange(callback: () => void) {
  const id = setInterval(callback, 60_000);
  return () => clearInterval(id);
}

function getServerDayIndex() {
  return 0;
}

export function RealEstateFact() {
  const dayIndex = React.useSyncExternalStore(
    subscribeToDayChange,
    getDayIndex,
    getServerDayIndex
  );

  const rotation = React.useMemo(() => {
    const start = dayIndex % FACTS.length;
    return [...FACTS.slice(start), ...FACTS.slice(0, start)];
  }, [dayIndex]);

  const { text } = useTypewriterLoop(rotation, {
    typingSpeed: 20,
    deletingSpeed: 10,
    pauseDuration: 3200,
  });

  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center gap-1.5 text-[11px] font-medium tracking-wide uppercase opacity-55">
        <Sparkles className="size-3.5" />
        Real estate, today
      </div>
      <p className="min-h-[3.6em] text-[15px] leading-relaxed">
        {text}
        <span aria-hidden className="animate-pulse opacity-70">
          &nbsp;&#124;
        </span>
      </p>
    </div>
  );
}
