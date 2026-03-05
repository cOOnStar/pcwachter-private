import { auth } from "@/auth";
import {
  getAccountProfile,
  getLicenseStatus,
  getSupportTickets,
  type AccountProfile,
  type LicenseStatus,
  type SupportTicketSummary,
} from "@/lib/api";
import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";
import ProfileEditor from "./ProfileEditor";
import SuccessBanner from "./success-banner";

export const metadata: Metadata = { title: "Mein Konto – PCWächter" };

const STATE_LABELS: Record<string, string> = {
  active: "Aktiv",
  grace: "Nachfrist",
  expired: "Abgelaufen",
  trial: "Testversion",
};

const FEATURE_LABELS: Record<string, string> = {
  auto_fix: "Automatische Problembehebung",
  reports: "Detaillierte Berichte",
  priority_support: "Priority-Support",
};

type DashboardNotification = {
  id: string;
  title: string;
  body: string;
  severity: "info" | "warning" | "critical";
  timestamp: string | null;
  href: string;
  actionLabel: string;
};

function stateColor(state: string): string {
  if (state === "active" || state === "trial") return "#22c55e";
  if (state === "grace") return "#eab308";
  return "#ef4444";
}

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

function isSupportReplyPendingForUser(ticket: SupportTicketSummary): boolean {
  if (!ticket.last_contact_agent_at) return false;
  const agentAt = new Date(ticket.last_contact_agent_at).getTime();
  if (!Number.isFinite(agentAt)) return false;

  if (!ticket.last_contact_customer_at) return true;
  const customerAt = new Date(ticket.last_contact_customer_at).getTime();
  if (!Number.isFinite(customerAt)) return true;

  return agentAt >= customerAt;
}

function buildNotifications(
  license: LicenseStatus | null,
  tickets: SupportTicketSummary[]
): DashboardNotification[] {
  const items: DashboardNotification[] = [];

  if (license?.ok) {
    if (license.state === "expired") {
      items.push({
        id: "license-expired",
        title: "Lizenz abgelaufen",
        body: "Ihre Lizenz ist abgelaufen. Bitte verlaengern Sie sie, damit Ihre Geraete weiter geschuetzt bleiben.",
        severity: "critical",
        timestamp: license.expires_at,
        href: "/account/billing",
        actionLabel: "Lizenz verwalten",
      });
    } else if (license.state === "grace") {
      items.push({
        id: "license-grace",
        title: "Lizenz in Nachfrist",
        body: "Ihre Lizenz befindet sich in der Nachfrist. Bitte pruefen Sie Ihre Abrechnung.",
        severity: "warning",
        timestamp: license.grace_period_until ?? license.expires_at,
        href: "/account/billing",
        actionLabel: "Abrechnung oeffnen",
      });
    } else if (license.days_remaining !== null && license.days_remaining <= 14) {
      const daysText =
        license.days_remaining <= 0
          ? "heute"
          : `in ${license.days_remaining} Tag${license.days_remaining === 1 ? "" : "en"}`;
      items.push({
        id: "license-expiring",
        title: `Lizenz laeuft ${daysText} ab`,
        body: `${license.plan_label} endet bald. Verlaengern Sie rechtzeitig, um Ausfaelle zu vermeiden.`,
        severity: license.days_remaining <= 3 ? "critical" : "warning",
        timestamp: license.expires_at,
        href: "/account/billing",
        actionLabel: "Plan ansehen",
      });
    }
  }

  const thirtyDaysAgo = Date.now() - 30 * 24 * 60 * 60 * 1000;
  const replyNotifications = tickets
    .filter(isSupportReplyPendingForUser)
    .filter((ticket) => {
      const timestamp = ticket.last_contact_agent_at ? new Date(ticket.last_contact_agent_at).getTime() : NaN;
      return Number.isFinite(timestamp) && timestamp >= thirtyDaysAgo;
    })
    .sort((left, right) => {
      const leftTs = left.last_contact_agent_at ? new Date(left.last_contact_agent_at).getTime() : 0;
      const rightTs = right.last_contact_agent_at ? new Date(right.last_contact_agent_at).getTime() : 0;
      return rightTs - leftTs;
    })
    .slice(0, 5)
    .map((ticket) => ({
      id: `ticket-${ticket.id}`,
      title: `Support hat Ticket ${ticket.number ? `#${ticket.number}` : ticket.id} beantwortet`,
      body: ticket.title
        ? `"${ticket.title}" wartet auf Ihre Rueckmeldung.`
        : "Ihr Support-Ticket wurde aktualisiert.",
      severity: "info" as const,
      timestamp: ticket.last_contact_agent_at ?? ticket.updated_at,
      href: "/account/support",
      actionLabel: "Zum Support",
    }));

  items.push(...replyNotifications);

  return items
    .sort((left, right) => {
      const leftTs = left.timestamp ? new Date(left.timestamp).getTime() : 0;
      const rightTs = right.timestamp ? new Date(right.timestamp).getTime() : 0;
      return rightTs - leftTs;
    })
    .slice(0, 6);
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

export default async function AccountPage() {
  const session = await auth();
  const [profileResult, license, tickets] = session?.accessToken
    ? await Promise.all([
        getAccountProfile(session.accessToken),
        getLicenseStatus(session.accessToken),
        getSupportTickets(session.accessToken),
      ])
    : [null, null, []];

  const profile: AccountProfile = profileResult ?? {
    sub: session?.userId ?? "",
    email: session?.user?.email ?? null,
    first_name: null,
    last_name: null,
    name: session?.user?.name ?? session?.user?.email ?? null,
    email_verified: null,
    warnings: [],
  };

  const notifications = buildNotifications(license, tickets);

  return (
    <div>
      <h1 style={{ fontWeight: 800, fontSize: "1.5rem", marginBottom: "2rem" }}>
        Mein Konto
      </h1>
      <Suspense fallback={null}>
        <SuccessBanner />
      </Suspense>

      <div className="account-overview-grid">
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

        <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
          <ProfileEditor initialProfile={profile} />

          <div
            style={{
              background: "var(--surface)",
              border: "1px solid var(--border)",
              borderRadius: "0.75rem",
              padding: "1.5rem",
            }}
          >
            <h2 style={{ fontWeight: 700, fontSize: "1rem", marginBottom: "1.25rem" }}>
              Lizenz
            </h2>

            {license && license.ok ? (
              <>
                <div
                  style={{
                    display: "flex",
                    gap: "1rem",
                    flexWrap: "wrap",
                    marginBottom: "1.25rem",
                  }}
                >
                  <div
                    style={{
                      background: "var(--surface2)",
                      borderRadius: "0.5rem",
                      padding: "0.75rem 1.25rem",
                      flex: 1,
                    }}
                  >
                    <div style={{ color: "var(--text-muted)", fontSize: "0.75rem", marginBottom: "0.25rem" }}>
                      Plan
                    </div>
                    <div style={{ fontWeight: 700 }}>{license.plan_label}</div>
                  </div>
                  <div
                    style={{
                      background: "var(--surface2)",
                      borderRadius: "0.5rem",
                      padding: "0.75rem 1.25rem",
                      flex: 1,
                    }}
                  >
                    <div style={{ color: "var(--text-muted)", fontSize: "0.75rem", marginBottom: "0.25rem" }}>
                      Status
                    </div>
                    <div style={{ fontWeight: 700, color: stateColor(license.state) }}>
                      {STATE_LABELS[license.state] ?? license.state}
                    </div>
                  </div>
                  {license.days_remaining !== null && (
                    <div
                      style={{
                        background: "var(--surface2)",
                        borderRadius: "0.5rem",
                        padding: "0.75rem 1.25rem",
                        flex: 1,
                      }}
                    >
                      <div style={{ color: "var(--text-muted)", fontSize: "0.75rem", marginBottom: "0.25rem" }}>
                        Verbleibend
                      </div>
                      <div style={{ fontWeight: 700 }}>{license.days_remaining} Tage</div>
                    </div>
                  )}
                </div>

                <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                  {Object.entries(FEATURE_LABELS).map(([key, label]) => {
                    const active = license.features?.[key] ?? false;
                    return (
                      <div
                        key={key}
                        style={{
                          display: "flex",
                          gap: "0.75rem",
                          alignItems: "center",
                          fontSize: "0.9rem",
                          color: active ? "var(--text)" : "var(--text-muted)",
                        }}
                      >
                        <span style={{ color: active ? "#22c55e" : "var(--text-muted)" }}>
                          {active ? "✓" : "✗"}
                        </span>
                        {label}
                      </div>
                    );
                  })}
                </div>
              </>
            ) : (
              <div style={{ color: "var(--text-muted)", fontSize: "0.9rem" }}>
                <p style={{ marginBottom: "1rem" }}>Keine aktive Lizenz gefunden.</p>
                <Link href="/account/billing" className="btn btn-primary" style={{ fontSize: "0.85rem" }}>
                  Plan auswaehlen
                </Link>
              </div>
            )}
          </div>
        </div>
      </div>

      <div style={{ textAlign: "right", marginTop: "1.5rem" }}>
        <Link href="/account/billing" style={{ color: "var(--text-muted)", fontSize: "0.875rem" }}>
          Abrechnung & Zahlungshistorie →
        </Link>
      </div>
    </div>
  );
}
