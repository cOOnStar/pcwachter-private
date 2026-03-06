import { auth } from "@/auth";
import { getSupportTickets, type SupportTicketSummary } from "@/lib/api";
import SupportTicketComposer from "./SupportTicketComposer";
import SupportTicketHistory from "./SupportTicketHistory";

export default async function SupportPage() {
  const session = await auth();
  const tickets: SupportTicketSummary[] = session?.accessToken ? await getSupportTickets(session.accessToken) : [];

  return (
    <div>
      <h1 style={{ fontWeight: 800, fontSize: "1.5rem", marginBottom: "0.5rem" }}>Support</h1>
      <p style={{ color: "var(--text-muted)", fontSize: "0.875rem", marginBottom: "2rem", lineHeight: 1.6 }}>
        Hier sehen Sie Ihre letzten Tickets und koennen direkt eine neue Anfrage erstellen.
      </p>

      <SupportTicketHistory initialTickets={tickets} />

      <SupportTicketComposer />

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
