"use client";

import { useState } from "react";

const CATEGORIES = [
  { value: "general", label: "Allgemeine Anfrage" },
  { value: "technical", label: "Technisches Problem" },
  { value: "billing", label: "Abrechnung & Lizenz" },
  { value: "feature", label: "Feature-Wunsch" },
  { value: "other", label: "Sonstiges" },
];

export default function SupportTicketComposer() {
  const [title, setTitle] = useState("");
  const [category, setCategory] = useState("general");
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState("");

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    if (!title.trim() || !message.trim()) return;

    setLoading(true);
    setError("");
    try {
      const categoryLabel = CATEGORIES.find((entry) => entry.value === category)?.label ?? category;
      const body = `Kategorie: ${categoryLabel}\n\n${message.trim()}`;
      const response = await fetch("/api/home/support-ticket", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ title: title.trim(), body }),
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        if (data.error === "support_not_configured") {
          setError(
            "Support ist momentan nicht verfuegbar. Bitte kontaktieren Sie uns direkt per E-Mail: support@pcwächter.de"
          );
        } else {
          setError(data.error ?? "Fehler beim Erstellen des Tickets.");
        }
        return;
      }

      setSuccess(true);
      setTitle("");
      setCategory("general");
      setMessage("");
    } catch {
      setError("Netzwerkfehler. Bitte versuchen Sie es erneut.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div
      style={{
        background: "var(--surface)",
        border: "1px solid var(--border)",
        borderRadius: "0.75rem",
        padding: "1.75rem",
      }}
    >
      <div style={{ marginBottom: "1.25rem" }}>
        <h2 style={{ fontWeight: 700, fontSize: "1rem", marginBottom: "0.35rem" }}>Neues Ticket</h2>
        <p style={{ color: "var(--text-muted)", fontSize: "0.875rem", lineHeight: 1.6 }}>
          Schildern Sie Ihr Anliegen. Wir melden uns so schnell wie moeglich.
        </p>
      </div>

      {success && (
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
          <strong>Ticket erstellt.</strong> Wir haben Ihre Anfrage erhalten und melden uns bald.
        </div>
      )}

      {error && (
        <div
          style={{
            background: "#450a0a",
            border: "1px solid #b91c1c",
            borderRadius: "0.75rem",
            padding: "1rem 1.25rem",
            color: "#fca5a5",
            marginBottom: "1.5rem",
            fontSize: "0.875rem",
          }}
        >
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        <div>
          <label
            htmlFor="title"
            style={{ display: "block", fontSize: "0.85rem", fontWeight: 600, marginBottom: "0.4rem" }}
          >
            Betreff *
          </label>
          <input
            id="title"
            type="text"
            required
            maxLength={200}
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            placeholder="Kurze Beschreibung des Problems"
            style={{
              width: "100%",
              background: "var(--surface2)",
              border: "1px solid var(--border)",
              borderRadius: "0.5rem",
              padding: "0.6rem 0.875rem",
              color: "var(--text)",
              fontSize: "0.9rem",
            }}
          />
        </div>

        <div>
          <label
            htmlFor="category"
            style={{ display: "block", fontSize: "0.85rem", fontWeight: 600, marginBottom: "0.4rem" }}
          >
            Kategorie
          </label>
          <select
            id="category"
            value={category}
            onChange={(event) => setCategory(event.target.value)}
            style={{
              width: "100%",
              background: "var(--surface2)",
              border: "1px solid var(--border)",
              borderRadius: "0.5rem",
              padding: "0.6rem 0.875rem",
              color: "var(--text)",
              fontSize: "0.9rem",
              cursor: "pointer",
            }}
          >
            {CATEGORIES.map((entry) => (
              <option key={entry.value} value={entry.value}>
                {entry.label}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label
            htmlFor="message"
            style={{ display: "block", fontSize: "0.85rem", fontWeight: 600, marginBottom: "0.4rem" }}
          >
            Nachricht *
          </label>
          <textarea
            id="message"
            required
            maxLength={5000}
            rows={7}
            value={message}
            onChange={(event) => setMessage(event.target.value)}
            placeholder="Beschreiben Sie Ihr Anliegen so detailliert wie moeglich..."
            style={{
              width: "100%",
              background: "var(--surface2)",
              border: "1px solid var(--border)",
              borderRadius: "0.5rem",
              padding: "0.6rem 0.875rem",
              color: "var(--text)",
              fontSize: "0.9rem",
              resize: "vertical",
              fontFamily: "inherit",
              lineHeight: 1.6,
            }}
          />
          <div style={{ textAlign: "right", fontSize: "0.75rem", color: "var(--text-muted)", marginTop: "0.25rem" }}>
            {message.length}/5000
          </div>
        </div>

        <div style={{ display: "flex", gap: "1rem", alignItems: "center", flexWrap: "wrap" }}>
          <button
            type="submit"
            className="btn btn-primary"
            disabled={loading || !title.trim() || !message.trim()}
            style={{ opacity: loading ? 0.7 : 1 }}
          >
            {loading ? "Wird gesendet..." : "Ticket erstellen"}
          </button>
          <span style={{ fontSize: "0.8rem", color: "var(--text-muted)" }}>* Pflichtfelder</span>
        </div>
      </form>
    </div>
  );
}
