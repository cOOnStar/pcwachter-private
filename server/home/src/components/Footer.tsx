import Link from "next/link";

export default function Footer() {
  return (
    <footer
      style={{
        background: "var(--surface)",
        borderTop: "1px solid var(--border)",
        padding: "2.5rem 0",
        marginTop: "4rem",
      }}
    >
      <div
        className="container"
        style={{
          display: "flex",
          flexWrap: "wrap",
          gap: "2rem",
          justifyContent: "space-between",
          alignItems: "flex-start",
        }}
      >
        <div>
          <div style={{ fontWeight: 800, fontSize: "1rem", marginBottom: "0.5rem" }}>PCWächter</div>
          <p style={{ color: "var(--text-muted)", fontSize: "0.85rem", maxWidth: 320 }}>
            Kundenportal für Konto, Lizenzstatus und Abrechnung.
          </p>
        </div>

        <div>
          <div style={{ fontWeight: 600, marginBottom: "0.75rem", fontSize: "0.85rem" }}>Konto</div>
          <div style={{ display: "flex", flexDirection: "column", gap: "0.4rem" }}>
            <Link href="/account" style={{ color: "var(--text-muted)", fontSize: "0.85rem" }}>
              Übersicht
            </Link>
            <Link href="/account/billing" style={{ color: "var(--text-muted)", fontSize: "0.85rem" }}>
              Abrechnung
            </Link>
          </div>
        </div>
      </div>

      <div
        className="container"
        style={{
          marginTop: "2rem",
          paddingTop: "1.5rem",
          borderTop: "1px solid var(--border)",
          display: "flex",
          justifyContent: "space-between",
          color: "var(--text-muted)",
          fontSize: "0.8rem",
        }}
      >
        <span>© {new Date().getFullYear()} PCWächter</span>
        <span>Alle Rechte vorbehalten</span>
      </div>
    </footer>
  );
}
