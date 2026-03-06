"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

export type DashboardNotification = {
  id: string;
  title: string;
  body: string;
  severity: "info" | "warning" | "critical";
  timestamp: string | null;
  href: string;
  actionLabel: string;
};

type NotificationApiItem = {
  id: string;
  title: string;
  body: string;
  created_at: string | null;
  meta?: {
    severity?: string;
    href?: string;
    action_label?: string;
  } | null;
};

function formatDate(iso: string | null): string {
  if (!iso) return "Unbekannt";
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "Unbekannt";
  return new Intl.DateTimeFormat("de-DE", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
}

function severityColors(severity: DashboardNotification["severity"]) {
  if (severity === "critical") {
    return {
      border: "#b91c1c",
      background: "rgba(69, 10, 10, 0.55)",
      label: "#fca5a5",
    };
  }
  if (severity === "warning") {
    return {
      border: "#eab308",
      background: "rgba(74, 50, 0, 0.45)",
      label: "#fde047",
    };
  }
  return {
    border: "#2563eb",
    background: "rgba(30, 58, 95, 0.45)",
    label: "#93c5fd",
  };
}

function normalizeSeverity(value: string | undefined): DashboardNotification["severity"] {
  if (value === "critical") return "critical";
  if (value === "warning") return "warning";
  return "info";
}

function sortNotifications(items: DashboardNotification[]): DashboardNotification[] {
  return [...items]
    .sort((left, right) => {
      const leftTs = left.timestamp ? new Date(left.timestamp).getTime() : 0;
      const rightTs = right.timestamp ? new Date(right.timestamp).getTime() : 0;
      return rightTs - leftTs;
    })
    .slice(0, 6);
}

function mapSupportNotification(item: NotificationApiItem): DashboardNotification {
  return {
    id: item.id,
    title: item.title,
    body: item.body,
    severity: normalizeSeverity(item.meta?.severity),
    timestamp: item.created_at,
    href: item.meta?.href || "/account/support#ticket-history",
    actionLabel: item.meta?.action_label || "Verlauf oeffnen",
  };
}

export default function AccountNotificationCenter({
  licenseNotifications,
  initialSupportNotifications,
}: {
  licenseNotifications: DashboardNotification[];
  initialSupportNotifications: DashboardNotification[];
}) {
  const [supportNotifications, setSupportNotifications] = useState(initialSupportNotifications);

  useEffect(() => {
    let active = true;

    async function loadNotifications() {
      try {
        const response = await fetch(
          "/api/home/notifications?limit=20&unread_only=true&type_prefix=support.",
          { cache: "no-store" }
        );
        if (!response.ok) return;
        const data = (await response.json().catch(() => ({}))) as {
          items?: NotificationApiItem[];
        };
        if (!active) return;
        const nextItems = Array.isArray(data.items)
          ? data.items.map(mapSupportNotification)
          : [];
        setSupportNotifications(sortNotifications(nextItems));
      } catch {
        // Keep the previous notification state on transient polling errors.
      }
    }

    const intervalId = window.setInterval(() => {
      if (document.visibilityState === "visible") {
        void loadNotifications();
      }
    }, 5000);

    const handleExternalRefresh = () => {
      void loadNotifications();
    };

    window.addEventListener("pcw-support-notifications-changed", handleExternalRefresh);
    void loadNotifications();

    return () => {
      active = false;
      window.clearInterval(intervalId);
      window.removeEventListener("pcw-support-notifications-changed", handleExternalRefresh);
    };
  }, []);

  const notifications = sortNotifications([...licenseNotifications, ...supportNotifications]);

  return (
    <div
      style={{
        background: "var(--surface)",
        border: "1px solid var(--border)",
        borderRadius: "0.75rem",
        padding: "1.5rem",
      }}
    >
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          gap: "1rem",
          alignItems: "center",
          marginBottom: "1.25rem",
          flexWrap: "wrap",
        }}
      >
        <div>
          <h2 style={{ fontWeight: 700, fontSize: "1rem", marginBottom: "0.25rem" }}>
            Benachrichtigungen
          </h2>
          <p style={{ color: "var(--text-muted)", fontSize: "0.875rem" }}>
            Support, Lizenz und wichtige Hinweise auf einen Blick.
          </p>
        </div>
        <div
          style={{
            background: "var(--surface2)",
            borderRadius: "9999px",
            padding: "0.35rem 0.75rem",
            fontSize: "0.8rem",
            fontWeight: 700,
            color: "var(--text-muted)",
          }}
        >
          {notifications.length} offen
        </div>
      </div>

      {notifications.length === 0 ? (
        <div
          style={{
            border: "1px dashed var(--border)",
            borderRadius: "0.75rem",
            padding: "1.5rem",
            color: "var(--text-muted)",
            fontSize: "0.9rem",
            lineHeight: 1.6,
          }}
        >
          Keine offenen Benachrichtigungen. Ihre Lizenz und Ihre aktuellen Support-Tickets
          sehen im Moment unauffaellig aus.
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: "0.85rem" }}>
          {notifications.map((item) => {
            const colors = severityColors(item.severity);
            return (
              <div
                key={item.id}
                style={{
                  border: `1px solid ${colors.border}`,
                  background: colors.background,
                  borderRadius: "0.75rem",
                  padding: "1rem 1.1rem",
                }}
              >
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    gap: "1rem",
                    alignItems: "flex-start",
                    flexWrap: "wrap",
                    marginBottom: "0.65rem",
                  }}
                >
                  <div>
                    <div style={{ fontWeight: 700, marginBottom: "0.2rem" }}>{item.title}</div>
                    <div style={{ color: "var(--text-muted)", fontSize: "0.875rem", lineHeight: 1.6 }}>
                      {item.body}
                    </div>
                  </div>
                  <span
                    style={{
                      color: colors.label,
                      fontSize: "0.75rem",
                      fontWeight: 700,
                      textTransform: "uppercase",
                      letterSpacing: "0.04em",
                    }}
                  >
                    {item.severity === "critical"
                      ? "Kritisch"
                      : item.severity === "warning"
                        ? "Hinweis"
                        : "Neu"}
                  </span>
                </div>

                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    gap: "1rem",
                    alignItems: "center",
                    flexWrap: "wrap",
                  }}
                >
                  <span style={{ color: "var(--text-muted)", fontSize: "0.8rem" }}>
                    {formatDate(item.timestamp)}
                  </span>
                  <Link
                    href={item.href}
                    className="btn btn-outline"
                    style={{ fontSize: "0.8rem", padding: "0.45rem 0.9rem" }}
                  >
                    {item.actionLabel}
                  </Link>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
