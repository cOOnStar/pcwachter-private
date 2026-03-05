import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center px-2 py-0.5 rounded-full text-[0.68rem] font-semibold tracking-wide",
  {
    variants: {
      variant: {
        success:  "bg-[rgba(16,217,160,0.12)] text-[#10d9a0] ring-1 ring-[rgba(16,217,160,0.25)]",
        warning:  "bg-[rgba(245,158,66,0.12)] text-[#f59e42] ring-1 ring-[rgba(245,158,66,0.25)]",
        danger:   "bg-[rgba(240,64,112,0.12)] text-[#f04070] ring-1 ring-[rgba(240,64,112,0.25)]",
        info:     "bg-[rgba(56,192,255,0.1)] text-[#38c0ff] ring-1 ring-[rgba(56,192,255,0.2)]",
        neutral:  "bg-[rgba(128,128,184,0.08)] text-[var(--text-secondary)] ring-1 ring-[rgba(128,128,184,0.15)]",
        accent:   "bg-[rgba(124,92,252,0.15)] text-[#9478fd] ring-1 ring-[rgba(124,92,252,0.3)]",
      },
    },
    defaultVariants: {
      variant: "neutral",
    },
  }
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ variant }), className)} {...props} />;
}

export { Badge, badgeVariants };
