"use client";

import { useSession, signIn } from "next-auth/react";
import type { PlanItem } from "@/lib/api";

const FEATURE_LABELS: Record<string, string> = {
  auto_fix: "Automatische Problembehebung",
  reports: "Detaillierte Berichte",
  priority_support: "Priority-Support",
};

function formatPrice(plan: PlanItem): string {
  if (!plan.price_eur || plan.price_eur === 0) return "Kostenlos";
  const formatted = plan.price_eur.toFixed(2).replace(".", ",");
  if (plan.duration_days === 30) return `${formatted} € / Monat`;
  if (plan.duration_days === 365) return `${formatted} € / Jahr`;
  return `${formatted} €`;
}

function formatDuration(plan: PlanItem): string {
  if (!plan.duration_days) return "Unbegrenzt";
  if (plan.duration_days === 7) return "7 Tage";
  if (plan.duration_days === 30) return "30 Tage";
  if (plan.duration_days === 365) return "1 Jahr";
  return `${plan.duration_days} Tage`;
}

const HIGHLIGHTED = ["standard", "professional"];

interface Props {
  plans: PlanItem[];
}

export default function PricingTable({ plans }: Props) {
  const { data: session } = useSession();

  const visible = plans
    .filter((p) => p.is_active && p.id !== "unlimited" && p.id !== "custom")
    .sort((a, b) => a.sort_order - b.sort_order);

  async function handleCheckout(plan: PlanItem) {
    if (!session) {
      signIn("keycloak", { callbackUrl: "/account/billing" });
      return;
    }

    const res = await fetch("/api/checkout", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ plan_id: plan.id }),
    });

    if (res.ok) {
      const { checkout_url } = await res.json();
      window.location.href = checkout_url;
    }
  }

  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: `repeat(${Math.min(visible.length, 3)}, 1fr)`,
        gap: "1.5rem",
        maxWidth: 960,
        margin: "0 auto",
      }}
    >
      {visible.map((plan) => {
        const highlighted = HIGHLIGHTED.includes(plan.id);
        const isFree = !plan.price_eur || plan.price_eur === 0;
        const features = plan.feature_flags ?? {};

        return (
          <div
            key={plan.id}
            style={{
              background: highlighted ? "var(--blue)" : "var(--surface)",
              border: `1px solid ${highlighted ? "#3b82f6" : "var(--border)"}`,
              borderRadius: "1rem",
              padding: "2rem",
              display: "flex",
              flexDirection: "column",
              position: "relative",
            }}
          >
            {plan.id === "professional" && (
              <div
                style={{
                  position: "absolute",
                  top: -12,
                  left: "50%",
                  transform: "translateX(-50%)",
                  background: "#eab308",
                  color: "#000",
                  fontSize: "0.7rem",
                  fontWeight: 800,
                  padding: "0.25rem 0.75rem",
                  borderRadius: 9999,
                  letterSpacing: "0.05em",
                  textTransform: "uppercase",
                }}
              >
                Beliebt
              </div>
            )}

            <div style={{ marginBottom: "1.5rem" }}>
              <div
                style={{
                  fontWeight: 700,
                  fontSize: "0.85rem",
                  color: highlighted ? "#bfdbfe" : "var(--text-muted)",
                  textTransform: "uppercase",
                  letterSpacing: "0.05em",
                  marginBottom: "0.5rem",
                }}
              >
                {plan.label}
              </div>
              <div
                style={{
                  fontWeight: 800,
                  fontSize: "1.75rem",
                  letterSpacing: "-0.02em",
                }}
              >
                {formatPrice(plan)}
              </div>
              <div
                style={{
                  color: highlighted ? "#bfdbfe" : "var(--text-muted)",
                  fontSize: "0.85rem",
                  marginTop: "0.25rem",
                }}
              >
                Laufzeit: {formatDuration(plan)}
                {plan.max_devices ? ` · ${plan.max_devices} Gerät${plan.max_devices > 1 ? "e" : ""}` : ""}
              </div>
            </div>

            <ul
              style={{
                listStyle: "none",
                display: "flex",
                flexDirection: "column",
                gap: "0.6rem",
                flex: 1,
                marginBottom: "1.75rem",
              }}
            >
              {Object.entries(FEATURE_LABELS).map(([key, label]) => {
                const active = features[key] ?? false;
                return (
                  <li
                    key={key}
                    style={{
                      display: "flex",
                      gap: "0.5rem",
                      alignItems: "center",
                      color: active
                        ? highlighted ? "#fff" : "var(--text)"
                        : highlighted ? "#93c5fd" : "var(--text-muted)",
                      fontSize: "0.9rem",
                    }}
                  >
                    <span>{active ? "✓" : "–"}</span>
                    <span>{label}</span>
                  </li>
                );
              })}
            </ul>

            {isFree ? (
              <a
                href="/download"
                className="btn"
                style={{
                  background: highlighted ? "rgba(255,255,255,0.15)" : "var(--surface2)",
                  color: "#fff",
                  justifyContent: "center",
                  padding: "0.75rem",
                }}
              >
                Herunterladen
              </a>
            ) : plan.price_eur !== null && Boolean(plan.stripe_price_id) ? (
              <button
                className="btn"
                onClick={() => handleCheckout(plan)}
                style={{
                  background: highlighted ? "#fff" : "var(--blue)",
                  color: highlighted ? "var(--blue)" : "#fff",
                  justifyContent: "center",
                  padding: "0.75rem",
                }}
              >
                Jetzt kaufen
              </button>
            ) : (
              <button
                className="btn"
                disabled
                style={{
                  background: "var(--surface2)",
                  color: "var(--text-muted)",
                  justifyContent: "center",
                  padding: "0.75rem",
                  cursor: "not-allowed",
                }}
              >
                Demnächst
              </button>
            )}
          </div>
        );
      })}
    </div>
  );
}
