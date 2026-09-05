import { BrandMark } from "@/components/auth/brand-mark";
import { RealEstateFact } from "@/components/auth/real-estate-fact";
import { RotatingHeadline } from "@/components/auth/rotating-headline";
import { SkylineLineArt } from "@/components/auth/skyline-line-art";

export function AuthBrandPanel() {
  return (
    <div className="relative hidden h-full flex-col justify-between overflow-hidden bg-[#fbf9f3] p-10 text-[#17203a] transition-colors dark:bg-[#08090b] dark:text-[#f3ede0] lg:flex">
      <div
        aria-hidden
        className="pointer-events-none absolute inset-x-0 top-0 h-2/3 bg-[radial-gradient(ellipse_at_top,_rgba(23,32,58,0.06),_transparent_70%)] dark:bg-[radial-gradient(ellipse_at_top,_rgba(243,237,224,0.05),_transparent_70%)]"
      />

      <BrandMark
        className="animate-fade-slide-up relative z-10"
        variant="cream"
      />

      <div className="relative z-10 flex flex-col gap-8">
        <RotatingHeadline className="animate-fade-slide-up max-w-sm text-4xl leading-tight font-semibold tracking-tight [animation-delay:120ms]" />
        <SkylineLineArt className="animate-fade-slide-up h-52 w-full text-[#17203a]/60 [animation-delay:260ms] sm:h-60 dark:text-[#f3ede0]/50" />

        <div className="animate-fade-slide-up max-w-sm border-t border-[#17203a]/10 pt-5 [animation-delay:420ms] dark:border-[#f3ede0]/10">
          <RealEstateFact />
        </div>
      </div>

      <div className="animate-fade-slide-up relative z-10 text-xs text-[#17203a]/45 [animation-delay:520ms] dark:text-[#f3ede0]/35">
        Jeet Homes Solution &copy; 2026
      </div>
    </div>
  );
}
