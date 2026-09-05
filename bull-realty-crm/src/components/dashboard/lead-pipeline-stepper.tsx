"use client";

import { Check, X } from "lucide-react";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/**
 * The path an event enquiry walks.
 *
 * Qualified sits before either visit because a planner's time is the scarce
 * resource — the date, the guest count and the budget are established first.
 * The planner goes to the client (Client Meeting) before the client tours the
 * venue (Venue Visit), and Follow-ups is the nurture bucket for someone who has
 * seen it and is being chased to a decision. Negotiation is entered by a priced
 * proposal going out and Booked by it being accepted, both driven by the
 * proposal itself.
 *
 * The two visit stages keep their stored values — they are permission keys
 * across the CRM — and carry event labels here.
 *
 * Enquiries skip steps constantly, which is why every chevron is clickable
 * rather than the path offering only the next one.
 */
const mainStages = [
  { value: "New", label: "New" },
  { value: "Contacted", label: "Contacted" },
  { value: "Qualified", label: "Qualified" },
  { value: "ObmVisit", label: "Client Meeting" },
  { value: "SiteVisit", label: "Venue Visit" },
  { value: "FollowUp", label: "Follow-ups" },
  { value: "Negotiation", label: "Negotiation" },
  { value: "Booked", label: "Booked" },
];

/**
 * Salesforce "Path": chevron-shaped stages that interlock, completed ones
 * filled with the brand colour and the current one darker. Every chevron is
 * itself the control — clicking one moves the lead there.
 *
 * The chevron shape comes from a clip-path rather than the old CSS triangle
 * trick, so it stays crisp at any height.
 */
export function LeadPipelineStepper({
  stage,
  onSelect,
  disabled,
}: {
  stage: string;
  onSelect: (stage: string) => void;
  disabled?: boolean;
}) {
  const isLost = stage === "Lost";
  const currentIndex = mainStages.findIndex((s) => s.value === stage);

  return (
    <div className="flex flex-wrap items-center gap-3 rounded border bg-card px-3 py-2.5 shadow-xs">
      <div className="flex min-w-0 flex-1 items-stretch gap-[3px]">
        {mainStages.map((step, index) => {
          const isDone = !isLost && index < currentIndex;
          const isCurrent = !isLost && index === currentIndex;

          return (
            <button
              key={step.value}
              type="button"
              disabled={disabled}
              onClick={() => onSelect(step.value)}
              title={`Move to ${step.label}`}
              style={{
                clipPath:
                  index === 0
                    ? "polygon(0 0, calc(100% - 12px) 0, 100% 50%, calc(100% - 12px) 100%, 0 100%)"
                    : index === mainStages.length - 1
                      ? "polygon(0 0, 100% 0, 100% 100%, 0 100%, 12px 50%)"
                      : "polygon(0 0, calc(100% - 12px) 0, 100% 50%, calc(100% - 12px) 100%, 0 100%, 12px 50%)",
              }}
              className={cn(
                "flex h-8 min-w-0 flex-1 items-center justify-center gap-1 px-2 text-[11.5px] font-medium transition-colors disabled:cursor-default",
                index > 0 && "-ml-[3px] pl-5",
                isDone && "bg-primary text-primary-foreground hover:bg-primary/90",
                isCurrent && "bg-brand-dark text-white",
                !isDone &&
                  !isCurrent &&
                  "bg-muted text-muted-foreground hover:bg-accent hover:text-accent-foreground"
              )}
            >
              {isDone ? <Check className="size-3.5 shrink-0" /> : null}
              <span className="truncate">{step.label}</span>
            </button>
          );
        })}
      </div>

      <div className="flex shrink-0 items-center gap-2">
        {/* No "mark as next" shortcut: the path itself is clickable, and a
            button that only ever offers the very next step pushed leads down
            the ladder one at a time when the real move is often a jump — a
            lead goes straight from Contacted to Follow-ups without a visit. */}
        <Button
          size="sm"
          variant={isLost ? "destructive" : "outline"}
          className="h-8"
          disabled={disabled}
          onClick={() => onSelect("Lost")}
        >
          <X /> {isLost ? "Marked lost" : "Mark lost"}
        </Button>
      </div>
    </div>
  );
}
