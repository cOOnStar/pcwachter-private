"use client";

import { useEffect, useState, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import Spinner from "@/components/Spinner";

interface PlanItem {
  id: string;
  label: string;
  price_eur: number | null;
  duration_days: number | null;
  max_devices: number | null;
  is_active: boolean;
  sort_order: number;
  stripe_price_id: string | null;
}

function formatDuration(days: number | null): string {
  if (!days) return "Einmalig";
  if (days === 30) return "Monatlich";
  if (days === 365) return "Jährlich";
  return `${days} Tage`;
}

function CheckoutBanner() {
  const params = useSearchParams();
  const status = params.get("checkout");
  if (status === "success") {
    return (
      <div
        style={{
          background: "#14532d",
          border: "1px solid #16a34a",
          borderRadius: "0.75rem",
          padding: "1rem 1.25rem",
          color: "#4ade80",
          marginBottom: "1.5rem",
          fontSize: "0.9rem",
        }}
      >
        ✓ <strong>Zahlung erfolgreich!</strong> Ihre Lizenz ist aktiv.
      </div>
    );
  }
  if (status === "cancel") {
    return (
      <div
        style={{
          background: "var(--surface)",
          border: "1px solid var(--border)",
          borderRadius: "0.75rem",
          padding: "1rem 1.25rem",
          color: "var(--text-muted)",
          marginBottom: "1.5rem",
          fontSize: "0.875rem",
        }}
      >
        Checkout abgebrochen. Sie können jederzeit einen Plan auswählen.
      </div>
    );
  }
  return null;
}

export default function BillingPage() {
  const stripeEnabled = Boolean(process.env.NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY);
  const [portalLoading, setPortalLoading] = useState(false);
  const [portalError, setPortalError] = useState("");
  const [showPlans, setShowPlans] = useState(!stripeEnabled);
  const [plans, setPlans] = useState<PlanItem[]>([]);
  const [plansLoading, setPlansLoading] = useState(false);
  const [checkoutLoading, setCheckoutLoading] = useState<string | null>(null);
  const [checkoutError, setCheckoutError] = useState("");

  useEffect(() => {
    if (!showPlans) return;
    setPlansLoading(true);
    const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "https://api.xn--pcwchter-2za.de";
    fetch(`${apiUrl}/console/public/plans`)
      .then((r) => (r.ok ? r.json() : { items: [] }))
      .then((d) => {
        const sorted = ((d.items ?? []) as PlanItem[])
          .filter((p) => p.is_active)
          .sort((a, b) => a.sort_order - b.sort_order);
        setPlans(sorted);
      })
      .catch(() => setPlans([]))
      .finally(() => setPlansLoading(false));
  }, [showPlans]);

  async function openPortal() {
    if (!stripeEnabled) return;
    setPortalLoading(true);
    setPortalError("");
    try {
      const res = await fetch("/api/portal", { method: "POST" });
      if (res.ok) {
        const { portal_url } = await res.json();
        window.location.href = portal_url;
        return;
      }
      if (res.status === 404) {
        setShowPlans(true);
        return;
      }
      setPortalError("Fehler beim Öffnen des Kundenportals.");
    } catch {
      setPortalError("Netzwerkfehler. Bitte versuchen Sie es erneut.");
    } finally {
      setPortalLoading(false);
    }
  }

  async function startCheckout(planId: string) {
    setCheckoutLoading(planId);
    setCheckoutError("");
    try {
      const res = await fetch("/api/checkout", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ plan_id: planId }),
      });
      const data = await res.json();
      if (!res.ok) {
        setCheckoutError((data as { error?: string }).error ?? "Checkout fehlgeschlagen.");
        return;
      }
      const { checkout_url } = data as { checkout_url?: string };
      if (checkout_url) window.location.href = checkout_url;
    } catch {
      setCheckoutError("Netzwerkfehler. Bitte versuchen Sie es erneut.");
    } finally {
      setCheckoutLoading(null);
    }
  }

  return (
    <div>
      <h1 style={{ fontWeight: 800, fontSize: "1.5rem", marginBottom: "2rem" }}>
        Abrechnung
      </h1>

      <Suspense fallback={null}>
        <CheckoutBanner />
      </Suspense>

      {/* Portal section */}
      {stripeEnabled && !showPlans && (
        <div
          style={{
            background: "var(--surface)",
            border: "1px solid var(--border)",
            borderRadius: "0.75rem",
            padding: "1.75rem",
            marginBottom: "1.5rem",
          }}
        >
          <h2 style={{ fontWeight: 700, fontSize: "1rem", marginBottom: "0.75rem" }}>
            Abo verwalten
          </h2>
          <p style={{ color: "var(--text-muted)", fontSize: "0.9rem", lineHeight: 1.6, marginBottom: "1.5rem" }}>
            Zahlungsmethoden, Rechnungshistorie und Kündigung über das sichere Stripe-Kundenportal.
          </p>
          <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
            <button
              className="btn btn-primary"
              onClick={openPortal}
              disabled={portalLoading}
              style={{ opacity: portalLoading ? 0.7 : 1 }}
            >
              {portalLoading ? "Wird geöffnet…" : "Kundenportal öffnen"}
            </button>
            <button className="btn btn-outline" onClick={() => setShowPlans(true)}>
              Plan wechseln / kaufen
            </button>
          </div>
          {portalError && (
            <p style={{ color: "var(--red)", fontSize: "0.85rem", marginTop: "0.75rem" }}>
              {portalError}
            </p>
          )}
        </div>
      )}

      {/* Plan selection */}
      {showPlans && (
        <div style={{ marginBottom: "1.5rem" }}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "1.25rem" }}>
            <h2 style={{ fontWeight: 700, fontSize: "1.1rem" }}>Plan auswählen</h2>
            {stripeEnabled && (
              <button
                className="btn btn-outline"
                style={{ fontSize: "0.8rem", padding: "0.35rem 0.75rem" }}
                onClick={() => setShowPlans(false)}
              >
                ← Zurück
              </button>
            )}
          </div>

          {plansLoading && (
            <div style={{ display: "flex", justifyContent: "center", padding: "2rem 0" }}>
              <Spinner />
            </div>
          )}

          {checkoutError && (
            <div
              style={{
                background: "#450a0a",
                border: "1px solid #b91c1c",
                borderRadius: "0.75rem",
                padding: "0.875rem 1.25rem",
                color: "#fca5a5",
                fontSize: "0.875rem",
                marginBottom: "1rem",
              }}
            >
              {checkoutError}
            </div>
          )}

          {!plansLoading && (
            <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
              {plans.length === 0 && (
                <div
                  style={{
                    background: "var(--surface)",
                    border: "1px solid var(--border)",
                    borderRadius: "0.75rem",
                    padding: "2rem",
                    textAlign: "center",
                    color: "var(--text-muted)",
                    fontSize: "0.9rem",
                  }}
                >
                  Keine Pläne verfügbar.
                </div>
              )}
              {plans.map((plan) => {
                const canBuy =
                  stripeEnabled &&
                  Boolean(plan.stripe_price_id ?? (plan.price_eur && plan.price_eur > 0));
                return (
                  <div
                    key={plan.id}
                    style={{
                      background: "var(--surface)",
                      border: "1px solid var(--border)",
                      borderRadius: "0.75rem",
                      padding: "1.5rem",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      gap: "1rem",
                      flexWrap: "wrap",
                    }}
                  >
                    <div>
                      <div style={{ fontWeight: 700, marginBottom: "0.25rem" }}>{plan.label}</div>
                      <div style={{ color: "var(--text-muted)", fontSize: "0.85rem", display: "flex", gap: "1rem", flexWrap: "wrap" }}>
                        <span>{formatDuration(plan.duration_days)}</span>
                        {plan.max_devices && (
                          <span>Bis zu {plan.max_devices} Gerät{plan.max_devices !== 1 ? "e" : ""}</span>
                        )}
                      </div>
                    </div>
                    <div style={{ display: "flex", alignItems: "center", gap: "1.25rem" }}>
                      {plan.price_eur !== null && (
                        <div style={{ textAlign: "right" }}>
                          <span style={{ fontWeight: 800, fontSize: "1.2rem" }}>
                            {plan.price_eur === 0
                              ? "Kostenlos"
                              : `${plan.price_eur.toFixed(2)} €`}
                          </span>
                          {plan.price_eur > 0 && plan.duration_days && (
                            <div style={{ fontSize: "0.75rem", color: "var(--text-muted)" }}>
                              /{formatDuration(plan.duration_days).toLowerCase()}
                            </div>
                          )}
                        </div>
                      )}
                      {canBuy ? (
                        <button
                          className="btn btn-primary"
                          onClick={() => startCheckout(plan.id)}
                          disabled={checkoutLoading === plan.id}
                          style={{ opacity: checkoutLoading === plan.id ? 0.7 : 1 }}
                        >
                          {checkoutLoading === plan.id ? "…" : "Jetzt kaufen"}
                        </button>
                      ) : (
                        <span
                          style={{
                            background: "var(--surface2)",
                            border: "1px solid var(--border)",
                            borderRadius: "0.5rem",
                            padding: "0.5rem 1rem",
                            fontSize: "0.85rem",
                            color: "var(--text-muted)",
                          }}
                        >
                          {plan.price_eur === 0 ? "Kostenlos" : "Demnächst"}
                        </span>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* Info */}
      <div
        style={{
          background: "var(--surface)",
          border: "1px solid var(--border)",
          borderRadius: "0.75rem",
          padding: "1.5rem",
        }}
      >
        <h2 style={{ fontWeight: 700, fontSize: "0.95rem", marginBottom: "0.75rem" }}>Informationen</h2>
        <ul
          style={{
            display: "flex",
            flexDirection: "column",
            gap: "0.5rem",
            color: "var(--text-muted)",
            fontSize: "0.875rem",
            paddingLeft: "1.25rem",
          }}
        >
          <li>Abonnements verlängern sich automatisch, sofern nicht gekündigt.</li>
          <li>Kündigung ist jederzeit zum Ende der Abrechnungsperiode möglich.</li>
          <li>
            Bei Fragen:{" "}
            <a href="mailto:support@pcwächter.de" style={{ color: "var(--blue)" }}>
              support@pcwächter.de
            </a>
          </li>
        </ul>
      </div>
    </div>
  );
}
