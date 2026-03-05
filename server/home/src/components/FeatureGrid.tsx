const features = [
  {
    icon: "📊",
    title: "Echtzeit-Monitoring",
    description:
      "CPU, RAM, Disk und Netzwerk werden kontinuierlich überwacht. Abweichungen werden sofort erkannt.",
  },
  {
    icon: "🔧",
    title: "Automatische Behebung",
    description:
      "Probleme wie überfüllte Temp-Ordner, blockierte Ports oder fehlerhafte Dienste werden automatisch behoben.",
  },
  {
    icon: "📋",
    title: "Detaillierte Berichte",
    description:
      "Übersichtliche Scan-Berichte zeigen den Zustand Ihres Systems und durchgeführte Maßnahmen.",
  },
  {
    icon: "🔒",
    title: "Sicher & Privat",
    description:
      "Alle Daten bleiben auf Ihrem PC. Die Kommunikation mit dem Backend ist Ende-zu-Ende verschlüsselt.",
  },
  {
    icon: "⚡",
    title: "Leichtgewichtig",
    description:
      "Der Windows-Agent läuft als Systemdienst mit minimalem Ressourcenverbrauch im Hintergrund.",
  },
  {
    icon: "🖥️",
    title: "Web-Konsole",
    description:
      "Verwalten Sie alle Geräte, Lizenzen und Benutzer über eine zentrale webbasierte Admin-Konsole.",
  },
];

export default function FeatureGrid() {
  return (
    <section style={{ padding: "5rem 0" }}>
      <div className="container">
        <div style={{ textAlign: "center", marginBottom: "3rem" }}>
          <h2
            style={{
              fontSize: "clamp(1.5rem, 3vw, 2.25rem)",
              fontWeight: 800,
              letterSpacing: "-0.02em",
              marginBottom: "0.75rem",
            }}
          >
            Alles, was Ihr PC braucht
          </h2>
          <p style={{ color: "var(--text-muted)", fontSize: "1rem", maxWidth: 480, margin: "0 auto" }}>
            Ein leistungsstarkes Monitoring-System, das Probleme erkennt bevor sie eskalieren.
          </p>
        </div>

        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))",
            gap: "1.5rem",
          }}
        >
          {features.map((f) => (
            <div
              key={f.title}
              style={{
                background: "var(--surface)",
                border: "1px solid var(--border)",
                borderRadius: "0.75rem",
                padding: "1.75rem",
              }}
            >
              <div style={{ fontSize: "2rem", marginBottom: "1rem" }}>{f.icon}</div>
              <h3
                style={{
                  fontWeight: 700,
                  marginBottom: "0.5rem",
                  fontSize: "1rem",
                }}
              >
                {f.title}
              </h3>
              <p style={{ color: "var(--text-muted)", fontSize: "0.9rem", lineHeight: 1.6 }}>
                {f.description}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
