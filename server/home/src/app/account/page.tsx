import { auth } from "@/auth";
import { getLicenseStatus } from "@/lib/api";
import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";
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

export default async function AccountPage() {
  const session = await auth();
  const license = session?.accessToken
    ? await getLicenseStatus(session.accessToken)
    : null;

  const name = session?.user?.name ?? session?.user?.email ?? "Unbekannt";
  const email = session?.user?.email ?? "";
  const initials = name
    .split(" ")
    .map((w: string) => w[0])
    .join("")
    .toUpperCase()
    .slice(0, 2);

  return (
    <div>
      <h1 style={{ fontWeight: 800, fontSize: "1.5rem", marginBottom: "2rem" }}>
        Mein Konto
      </h1>
      <Suspense fallback={null}>
        <SuccessBanner />
      </Suspense>

      {/* Profile card */}
      <div
        style={{
          background: "var(--surface)",
          border: "1px solid var(--border)",
          borderRadius: "0.75rem",
          padding: "1.5rem",
          display: "flex",
          gap: "1rem",
          alignItems: "center",
          marginBottom: "1.5rem",
        }}
      >
        <div
          style={{
            width: 52,
            height: 52,
            borderRadius: "50%",
            background: "var(--blue)",
            color: "#fff",
            fontWeight: 800,
            fontSize: "1.1rem",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            flexShrink: 0,
          }}
        >
          {initials}
        </div>
        <div>
          <div style={{ fontWeight: 700 }}>{name}</div>
          {email && (
            <div style={{ color: "var(--text-muted)", fontSize: "0.875rem" }}>{email}</div>
          )}
        </div>
      </div>

      {/* License card */}
      <div
        style={{
          background: "var(--surface)",
          border: "1px solid var(--border)",
          borderRadius: "0.75rem",
          padding: "1.5rem",
          marginBottom: "1.5rem",
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

            {/* Feature list */}
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
              Plan auswählen
            </Link>
          </div>
        )}
      </div>

      <div style={{ textAlign: "right" }}>
        <Link href="/account/billing" style={{ color: "var(--text-muted)", fontSize: "0.875rem" }}>
          Abrechnung & Zahlungshistorie →
        </Link>
      </div>
    </div>
  );
}
