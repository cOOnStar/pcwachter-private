interface BadgeProps {
  variant?: "success" | "warning" | "danger" | "info" | "neutral" | "accent";
  children: React.ReactNode;
}

export default function Badge({ variant = "neutral", children }: BadgeProps) {
  return <span className={`badge badge-${variant}`}>{children}</span>;
}

export function stateBadge(state: string) {
  const map: Record<string, BadgeProps["variant"]> = {
    activated: "success",
    active:    "success",
    issued:    "info",
    expired:   "warning",
    revoked:   "danger",
    grace:     "warning",
    online:    "success",
    offline:   "neutral",
    cancelled: "danger",
  };
  return <Badge variant={map[state] ?? "neutral"}>{state}</Badge>;
}

export function tierBadge(tier: string) {
  const map: Record<string, BadgeProps["variant"]> = {
    trial:        "neutral",
    standard:     "info",
    professional: "accent",
    unlimited:    "success",
    custom:       "warning",
  };
  return <Badge variant={map[tier] ?? "neutral"}>{tier}</Badge>;
}

export function severityBadge(severity: string) {
  const map: Record<string, BadgeProps["variant"]> = {
    critical: "danger",
    warning:  "warning",
    info:     "info",
  };
  return <Badge variant={map[severity] ?? "neutral"}>{severity}</Badge>;
}
