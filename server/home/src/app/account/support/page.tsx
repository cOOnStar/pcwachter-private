"use client";

import { useState } from "react";

const CATEGORIES = [
  { value: "general", label: "Allgemeine Anfrage" },
  { value: "technical", label: "Technisches Problem" },
  { value: "billing", label: "Abrechnung & Lizenz" },
  { value: "feature", label: "Feature-Wunsch" },
  { value: "other", label: "Sonstiges" },
];

export default function SupportPage() {
  const [title, setTitle] = useState("");
  const [category, setCategory] = useState("general");
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState("");

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim() || !message.trim()) return;

    setLoading(true);
    setError("");
    try {
      const categoryLabel = CATEGORIES.find((c) => c.value === category)?.label ?? category;
      const body = `Kategorie: ${categoryLabel}\n\n${message.trim()}`;
      const res = await fetch("/api/home/support-ticket", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ title: title.trim(), body }),
      });
      const data = await res.json();
      if (!res.ok) {
        if (data.error === "support_not_configured") {
          setError("Support ist momentan nicht verfügbar. Bitte kontaktieren Sie uns direkt per E-Mail: support@pcwächter.de");
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
    <div>
      <h1 style={{ fontWeight: 800, fontSize: "1.5rem", marginBottom: "0.5rem" }}>
        Support
      </h1>
      <p style={{ color: "var(--text-muted)", fontSize: "0.875rem", marginBottom: "2rem", lineHeight: 1.6 }}>
        Schildern Sie Ihr Anliegen. Wir melden uns so schnell wie möglich.
      </p>

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
          ✓ <strong>Ticket erstellt.</strong> Wir haben Ihre Anfrage erhalten und melden uns bald.
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

      <div
        style={{
          background: "var(--surface)",
          border: "1px solid var(--border)",
          borderRadius: "0.75rem",
          padding: "1.75rem",
        }}
      >
        <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          {/* Subject */}
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
              onChange={(e) => setTitle(e.target.value)}
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

          {/* Category */}
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
              onChange={(e) => setCategory(e.target.value)}
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
              {CATEGORIES.map((c) => (
                <option key={c.value} value={c.value}>
                  {c.label}
                </option>
              ))}
            </select>
          </div>

          {/* Message */}
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
              onChange={(e) => setMessage(e.target.value)}
              placeholder="Beschreiben Sie Ihr Anliegen so detailliert wie möglich…"
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

          <div style={{ display: "flex", gap: "1rem", alignItems: "center" }}>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={loading || !title.trim() || !message.trim()}
              style={{ opacity: loading ? 0.7 : 1 }}
            >
              {loading ? "Wird gesendet…" : "Ticket erstellen"}
            </button>
            <span style={{ fontSize: "0.8rem", color: "var(--text-muted)" }}>
              * Pflichtfelder
            </span>
          </div>
        </form>
      </div>

      <div
        style={{
          marginTop: "1.5rem",
          padding: "1rem 1.25rem",
          background: "var(--surface)",
          border: "1px solid var(--border)",
          borderRadius: "0.75rem",
          fontSize: "0.875rem",
          color: "var(--text-muted)",
          lineHeight: 1.6,
        }}
      >
        <strong style={{ color: "var(--text)" }}>Alternativ:</strong> Schreiben Sie uns direkt an{" "}
        <a href="mailto:support@pcwächter.de" style={{ color: "var(--blue)" }}>
          support@pcwächter.de
        </a>
      </div>
    </div>
  );
}
