export default function DownloadSection() {
  return (
    <section
      style={{
        background: "var(--surface)",
        border: "1px solid var(--border)",
        borderRadius: "1rem",
        padding: "3rem 2rem",
        textAlign: "center",
        maxWidth: 600,
        margin: "0 auto",
      }}
    >
      <div style={{ fontSize: "3rem", marginBottom: "1rem" }}>🖥️</div>
      <h2 style={{ fontWeight: 800, fontSize: "1.5rem", marginBottom: "0.75rem" }}>
        PCWächter für Windows
      </h2>
      <p style={{ color: "var(--text-muted)", marginBottom: "2rem", lineHeight: 1.6 }}>
        Laden Sie den Desktop-Client herunter und starten Sie kostenlos mit der Testversion.
        Keine Kreditkarte erforderlich.
      </p>
      <div style={{ display: "flex", gap: "1rem", justifyContent: "center", flexWrap: "wrap" }}>
        <a
          href="https://github.com/cOOnStar/web-Console/releases/latest"
          target="_blank"
          rel="noopener noreferrer"
          className="btn btn-primary"
          style={{ padding: "0.75rem 2rem", fontSize: "1rem" }}
        >
          ↓ Download (.exe)
        </a>
      </div>
      <p style={{ color: "var(--text-muted)", fontSize: "0.8rem", marginTop: "1.5rem" }}>
        Windows 10 / 11 · 64-bit · .NET 8 erforderlich
      </p>
    </section>
  );
}
