import Link from "next/link";

export default function Hero() {
  return (
    <section
      style={{
        padding: "6rem 0 4rem",
        textAlign: "center",
        background: "linear-gradient(180deg, #0f172a 0%, #1e293b 100%)",
      }}
    >
      <div className="container">
        <div
          className="badge badge-blue"
          style={{ marginBottom: "1.5rem", display: "inline-block" }}
        >
          Windows PC Monitoring
        </div>
        <h1
          style={{
            fontSize: "clamp(2rem, 5vw, 3.5rem)",
            fontWeight: 800,
            lineHeight: 1.15,
            letterSpacing: "-0.03em",
            marginBottom: "1.5rem",
            maxWidth: 700,
            margin: "0 auto 1.5rem",
          }}
        >
          Ihr PC unter{" "}
          <span style={{ color: "#60a5fa" }}>vollständiger Kontrolle</span>
        </h1>
        <p
          style={{
            color: "var(--text-muted)",
            fontSize: "1.1rem",
            maxWidth: 560,
            margin: "0 auto 2.5rem",
            lineHeight: 1.7,
          }}
        >
          PCWächter überwacht Ihren Windows-PC in Echtzeit, erkennt Probleme
          automatisch und behebt sie, bevor sie zum Problem werden.
        </p>
        <div style={{ display: "flex", gap: "1rem", justifyContent: "center", flexWrap: "wrap" }}>
          <Link href="/download" className="btn btn-primary" style={{ padding: "0.75rem 2rem", fontSize: "1rem" }}>
            Kostenlos herunterladen
          </Link>
          <Link href="/pricing" className="btn btn-outline" style={{ padding: "0.75rem 2rem", fontSize: "1rem" }}>
            Preise ansehen
          </Link>
        </div>

        {/* Hero visual */}
        <div
          style={{
            marginTop: "4rem",
            background: "var(--surface)",
            border: "1px solid var(--border)",
            borderRadius: "1rem",
            padding: "2rem",
            maxWidth: 750,
            margin: "4rem auto 0",
            textAlign: "left",
          }}
        >
          <div style={{ display: "flex", gap: "0.5rem", marginBottom: "1.5rem" }}>
            {["#ef4444", "#eab308", "#22c55e"].map((c) => (
              <div key={c} style={{ width: 12, height: 12, borderRadius: "50%", background: c }} />
            ))}
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
            {[
              { label: "CPU", value: 34, color: "#22c55e" },
              { label: "RAM", value: 61, color: "#60a5fa" },
              { label: "Disk", value: 47, color: "#a78bfa" },
            ].map(({ label, value, color }) => (
              <div key={label} style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
                <span style={{ color: "var(--text-muted)", fontSize: "0.85rem", width: 36 }}>{label}</span>
                <div style={{ flex: 1, background: "var(--surface2)", borderRadius: 4, height: 8 }}>
                  <div style={{ width: `${value}%`, background: color, height: 8, borderRadius: 4 }} />
                </div>
                <span style={{ color, fontSize: "0.85rem", width: 36, textAlign: "right" }}>{value}%</span>
              </div>
            ))}
          </div>
          <div
            style={{
              marginTop: "1.5rem",
              padding: "0.75rem 1rem",
              background: "#14532d",
              borderRadius: "0.5rem",
              color: "#4ade80",
              fontSize: "0.85rem",
              display: "flex",
              gap: "0.5rem",
              alignItems: "center",
            }}
          >
            <span>✓</span>
            <span>Alle Systeme funktionieren normal – kein Handlungsbedarf</span>
          </div>
        </div>
      </div>
    </section>
  );
}
