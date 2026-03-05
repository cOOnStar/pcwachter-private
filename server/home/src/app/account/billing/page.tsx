"use client";

import { useState } from "react";

// Note: Metadata export doesn't work in client components.
// For billing page we use a client component to handle portal redirect button.

export default function BillingPage() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const stripeEnabled = Boolean(process.env.NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY);

  async function openPortal() {
    if (!stripeEnabled) return;
    setLoading(true);
    setError("");
    try {
      const res = await fetch("/api/portal", { method: "POST" });
      if (res.ok) {
        const { portal_url } = await res.json();
        window.location.href = portal_url;
      } else {
        setError("Fehler beim Öffnen des Kundenportals.");
      }
    } catch {
      setError("Netzwerkfehler. Bitte versuchen Sie es erneut.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div>
      <h1 style={{ fontWeight: 800, fontSize: "1.5rem", marginBottom: "2rem" }}>
        Abrechnung
      </h1>

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
          Stripe Kundenportal
        </h2>
        <p
          style={{
            color: "var(--text-muted)",
            fontSize: "0.9rem",
            lineHeight: 1.6,
            marginBottom: "1.5rem",
          }}
        >
          {stripeEnabled
            ? "Verwalten Sie Ihre Zahlungsmethoden, sehen Sie Ihre Rechnungshistorie ein und kündigen Sie Ihr Abonnement über das sichere Stripe-Kundenportal."
            : "Stripe ist aktuell noch nicht aktiviert. Das Kundenportal wird später freigeschaltet."}
        </p>
        <button
          className="btn btn-primary"
          onClick={openPortal}
          disabled={loading || !stripeEnabled}
          style={{ opacity: loading || !stripeEnabled ? 0.7 : 1 }}
        >
          {!stripeEnabled ? "Demnächst verfügbar" : loading ? "Wird geöffnet…" : "Kundenportal öffnen"}
        </button>
        {error && (
          <p style={{ color: "var(--red)", fontSize: "0.85rem", marginTop: "0.75rem" }}>
            {error}
          </p>
        )}
      </div>

      <div
        style={{
          background: "var(--surface)",
          border: "1px solid var(--border)",
          borderRadius: "0.75rem",
          padding: "1.75rem",
        }}
      >
        <h2 style={{ fontWeight: 700, fontSize: "1rem", marginBottom: "0.75rem" }}>
          Informationen
        </h2>
        <ul
          style={{
            display: "flex",
            flexDirection: "column",
            gap: "0.5rem",
            color: "var(--text-muted)",
            fontSize: "0.9rem",
            paddingLeft: "1rem",
          }}
        >
          <li>Abonnements verlängern sich automatisch, sofern nicht gekündigt.</li>
          <li>Kündigung ist jederzeit zum Ende der Abrechnungsperiode möglich.</li>
          <li>Bei Fragen: support@pcwächter.de</li>
        </ul>
      </div>
    </div>
  );
}
