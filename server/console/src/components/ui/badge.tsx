import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center px-2 py-0.5 rounded-full text-[0.7rem] font-semibold tracking-wide border border-transparent",
  {
    variants: {
      variant: {
        success: "bg-[var(--success-subtle)] text-[var(--success)] border-[#1a6040]",
        warning: "bg-[var(--warning-subtle)] text-[var(--warning)] border-[#6e4a1e]",
        danger: "bg-[var(--danger-subtle)] text-[var(--danger)] border-[#6e1f28]",
        info: "bg-[var(--info-subtle)] text-[var(--info)] border-[#1e5a7a]",
        neutral: "bg-[#1a2240] text-[#8b97bd] border-[#2a3660]",
        accent: "bg-[var(--accent-subtle)] text-[var(--accent-hover)] border-[#2a4080]",
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
