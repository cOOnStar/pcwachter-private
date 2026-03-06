"use client";

import { useEffect, useState } from "react";
import type { SupportTicketSummary } from "@/lib/api";

function ticketTimestamp(ticket: SupportTicketSummary): number {
  const value = ticket.last_contact_agent_at ?? ticket.updated_at ?? ticket.created_at;
  if (!value) return 0;
  const parsed = new Date(value).getTime();
  return Number.isFinite(parsed) ? parsed : 0;
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

function stateLabel(ticket: SupportTicketSummary): string {
  if (isSupportReplyPendingForUser(ticket)) return "Antwort offen";
  if (ticket.state === "closed") return "Geschlossen";
  if (ticket.state === "pending reminder" || ticket.state === "pending close") return "Wartet";
  if (ticket.state === "new") return "Neu";
  return "Aktiv";
}

function stateColors(ticket: SupportTicketSummary) {
  if (isSupportReplyPendingForUser(ticket)) {
    return {
      border: "#2563eb",
      background: "rgba(30, 58, 95, 0.45)",
      label: "#93c5fd",
    };
  }

  if (ticket.state === "closed") {
    return {
      border: "var(--border)",
      background: "var(--surface2)",
      label: "var(--text-muted)",
    };
  }

  if (ticket.state === "pending reminder" || ticket.state === "pending close") {
    return {
      border: "#d97706",
      background: "rgba(120, 53, 15, 0.28)",
      label: "#fdba74",
    };
  }

  return {
    border: "#0f766e",
    background: "rgba(15, 118, 110, 0.18)",
    label: "#5eead4",
  };
}

export default function SupportTicketHistory({
  initialTickets,
}: {
  initialTickets: SupportTicketSummary[];
}) {
  const [tickets, setTickets] = useState(initialTickets);

  useEffect(() => {
    let active = true;

    async function refreshTickets() {
      try {
        const response = await fetch("/api/home/support-tickets", { cache: "no-store" });
        if (!response.ok) return;
        const data = (await response.json().catch(() => ({}))) as {
          items?: SupportTicketSummary[];
        };
        if (!active || !Array.isArray(data.items)) return;
        setTickets(data.items);
      } catch {
        // Keep the last known ticket list when polling fails.
      }
    }

    async function markSupportNotificationsRead() {
      try {
        const response = await fetch("/api/home/notifications/read-all?type_prefix=support.", {
          method: "POST",
        });
        if (!response.ok) return;
        window.dispatchEvent(new Event("pcw-support-notifications-changed"));
      } catch {
        // Ignore transient read-state sync errors.
      }
    }

    const intervalId = window.setInterval(() => {
      if (document.visibilityState === "visible") {
        void refreshTickets();
      }
    }, 5000);

    void refreshTickets();
    void markSupportNotificationsRead();

    return () => {
      active = false;
      window.clearInterval(intervalId);
    };
  }, []);

  const sortedTickets = [...tickets].sort((left, right) => ticketTimestamp(right) - ticketTimestamp(left));
  const replyPendingCount = sortedTickets.filter(isSupportReplyPendingForUser).length;

  return (
    <div
      id="ticket-history"
      style={{
        background: "var(--surface)",
        border: "1px solid var(--border)",
        borderRadius: "0.75rem",
        padding: "1.5rem",
        marginBottom: "1.5rem",
      }}
    >
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          gap: "1rem",
          alignItems: "center",
          flexWrap: "wrap",
          marginBottom: "1.25rem",
        }}
      >
        <div>
          <h2 style={{ fontWeight: 700, fontSize: "1rem", marginBottom: "0.35rem" }}>Ticketverlauf</h2>
          <p style={{ color: "var(--text-muted)", fontSize: "0.875rem", lineHeight: 1.6 }}>
            Antworten vom Support und Statuswechsel werden hier laufend aktualisiert.
          </p>
        </div>
        <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
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
            {sortedTickets.length} Tickets
          </div>
          <div
            style={{
              background: replyPendingCount > 0 ? "rgba(30, 58, 95, 0.6)" : "var(--surface2)",
              borderRadius: "9999px",
              padding: "0.35rem 0.75rem",
              fontSize: "0.8rem",
              fontWeight: 700,
              color: replyPendingCount > 0 ? "#93c5fd" : "var(--text-muted)",
            }}
          >
            {replyPendingCount} mit offener Antwort
          </div>
        </div>
      </div>

      {sortedTickets.length === 0 ? (
        <div
          style={{
            border: "1px dashed var(--border)",
            borderRadius: "0.75rem",
            padding: "1.25rem",
            color: "var(--text-muted)",
            fontSize: "0.9rem",
            lineHeight: 1.6,
          }}
        >
          Noch keine Support-Tickets vorhanden.
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: "0.85rem" }}>
          {sortedTickets.slice(0, 10).map((ticket) => {
            const colors = stateColors(ticket);
            const lastUpdate = ticket.last_contact_agent_at ?? ticket.updated_at ?? ticket.created_at;

            return (
              <div
                key={ticket.id}
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
                    marginBottom: "0.6rem",
                  }}
                >
                  <div>
                    <div style={{ fontWeight: 700, marginBottom: "0.2rem" }}>
                      {ticket.number ? `Ticket #${ticket.number}` : `Ticket ${ticket.id}`}
                    </div>
                    <div style={{ color: "var(--text)", fontSize: "0.9rem", lineHeight: 1.6 }}>
                      {ticket.title || "Ohne Betreff"}
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
                    {stateLabel(ticket)}
                  </span>
                </div>

                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    gap: "1rem",
                    alignItems: "center",
                    flexWrap: "wrap",
                    color: "var(--text-muted)",
                    fontSize: "0.8rem",
                  }}
                >
                  <span>Letzte Aktualisierung: {formatDate(lastUpdate)}</span>
                  <span>{ticket.article_count ?? 0} Beitraege</span>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
