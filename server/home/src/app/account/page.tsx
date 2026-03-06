import { auth } from "@/auth";
import {
  getAccountProfile,
  getHomeNotifications,
  getLicenseStatus,
  type AccountProfile,
} from "@/lib/api";
import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";
import AccountNotificationCenter, { type DashboardNotification } from "./AccountNotificationCenter";
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

function stateColor(state: string): string {
  if (state === "active" || state === "trial") return "#22c55e";
  if (state === "grace") return "#eab308";
  return "#ef4444";
}

function buildLicenseNotifications(license: Awaited<ReturnType<typeof getLicenseStatus>>): DashboardNotification[] {
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
  return items;
}

export default async function AccountPage() {
  const session = await auth();
  const [profileResult, license, supportNotifications] = session?.accessToken
    ? await Promise.all([
        getAccountProfile(session.accessToken),
        getLicenseStatus(session.accessToken),
        getHomeNotifications(session.accessToken, {
          limit: 20,
          unreadOnly: true,
          typePrefix: "support.",
        }),
      ])
    : [null, null, { items: [], total: 0 }];

  const profile: AccountProfile = profileResult ?? {
    sub: session?.userId ?? "",
    email: session?.user?.email ?? null,
    first_name: null,
    last_name: null,
    name: session?.user?.name ?? session?.user?.email ?? null,
    email_verified: null,
    warnings: [],
  };

  const licenseNotifications = buildLicenseNotifications(license);
  const initialSupportNotifications: DashboardNotification[] = supportNotifications.items.map((item) => ({
    id: item.id,
    title: item.title,
    body: item.body,
    severity:
      item.meta?.severity === "critical"
        ? "critical"
        : item.meta?.severity === "warning"
          ? "warning"
          : "info",
    timestamp: item.created_at,
    href: typeof item.meta?.href === "string" ? item.meta.href : "/account/support#ticket-history",
    actionLabel:
      typeof item.meta?.action_label === "string" ? item.meta.action_label : "Verlauf oeffnen",
  }));

  return (
    <div>
      <h1 style={{ fontWeight: 800, fontSize: "1.5rem", marginBottom: "2rem" }}>
        Mein Konto
      </h1>
      <Suspense fallback={null}>
        <SuccessBanner />
      </Suspense>

      <div className="account-overview-grid">
        <AccountNotificationCenter
          licenseNotifications={licenseNotifications}
          initialSupportNotifications={initialSupportNotifications}
        />

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
