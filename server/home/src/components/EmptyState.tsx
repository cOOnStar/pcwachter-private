import Link from "next/link";

interface EmptyStateProps {
  icon?: string;
  title: string;
  description?: string;
  actionLabel?: string;
  actionHref?: string;
}

export default function EmptyState({
  icon = "📭",
  title,
  description,
  actionLabel,
  actionHref,
}: EmptyStateProps) {
  return (
    <div
      style={{
        textAlign: "center",
        padding: "3rem 1rem",
        color: "var(--text-muted)",
      }}
    >
      <div style={{ fontSize: "2.5rem", marginBottom: "0.75rem" }}>{icon}</div>
      <p style={{ fontWeight: 600, color: "var(--text)", marginBottom: "0.4rem" }}>{title}</p>
      {description && (
        <p style={{ fontSize: "0.875rem", marginBottom: "1.25rem" }}>{description}</p>
      )}
      {actionLabel && actionHref && (
        <Link href={actionHref} className="btn btn-primary" style={{ fontSize: "0.875rem" }}>
          {actionLabel}
        </Link>
      )}
    </div>
  );
}
