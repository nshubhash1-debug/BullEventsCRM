import { Building2 } from "lucide-react";

import { cn } from "@/lib/utils";

export function BrandMark({
  className,
  variant = "dark",
}: {
  className?: string;
  variant?: "dark" | "light" | "cream";
}) {
  return (
    <div className={cn("flex items-center gap-2.5", className)}>
      <span
        className={cn(
          "flex size-9 shrink-0 items-center justify-center rounded-lg",
          variant === "dark" && "bg-white text-blue-700",
          variant === "light" && "bg-blue-700 text-white",
          variant === "cream" &&
            "bg-[#17203a] text-[#f3ede0] dark:bg-[#f3ede0] dark:text-[#17203a]"
        )}
      >
        <Building2 className="size-5" strokeWidth={2.25} />
      </span>
      <span className="flex flex-col leading-none">
        <span
          className={cn(
            "text-[15px] font-semibold tracking-tight",
            variant === "dark" && "text-white",
            variant === "light" && "text-foreground",
            variant === "cream" && "text-[#17203a] dark:text-[#f3ede0]"
          )}
        >
          Jeet Homes Solution
        </span>
        <span
          className={cn(
            "mt-1 text-[11px] font-medium tracking-wide uppercase",
            variant === "dark" && "text-blue-200/80",
            variant === "light" && "text-muted-foreground",
            variant === "cream" && "text-[#17203a]/55 dark:text-[#f3ede0]/55"
          )}
        >
          CRM &amp; ERP Platform
        </span>
      </span>
    </div>
  );
}
