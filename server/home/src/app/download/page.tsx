"use client";

import { useEffect, useState } from "react";

const RELEASE_BASE =
  process.env.NEXT_PUBLIC_RELEASE_BASE_URL ??
  "https://github.com/cOOnStar/pcwaechter-public-release/releases/latest/download";

// Asset names must stay stable – matches client/installer/bootstrapper and release workflow
const OFFLINE_NAME = "PCWaechter_offline_installer.exe";
const LIVE_NAME    = "PCWaechter_live_installer.exe";

interface InstallerManifest {
  version: string;
  installer: {
    url: string;
    sha256: string;
    silentArgs?: string;
  };
  bootstrapper?: {
    version: string;
    url: string;
    sha256: string;
  };
}

export default function DownloadPage() {
  const [manifest, setManifest] = useState<InstallerManifest | null>(null);
  const [copied, setCopied] = useState<string | null>(null);

  useEffect(() => {
    fetch(`${RELEASE_BASE}/installer-manifest.json`)
      .then((r) => (r.ok ? r.json() : null))
      .then((data) => setManifest(data))
      .catch(() => null);
  }, []);

  function copyToClipboard(text: string, key: string) {
    navigator.clipboard.writeText(text).then(() => {
      setCopied(key);
      setTimeout(() => setCopied(null), 2000);
    });
  }

  return (
    <main
      style={{
        maxWidth: 640,
        margin: "4rem auto",
        padding: "0 1.5rem",
        fontFamily: "inherit",
      }}
    >
      <h1 style={{ fontWeight: 800, fontSize: "2rem", marginBottom: "0.5rem" }}>
        PCWächter herunterladen
      </h1>
      <p style={{ color: "var(--text-muted)", marginBottom: "2.5rem", lineHeight: 1.6 }}>
        Für Windows 10 / 11 · 64-bit · .NET 10 erforderlich
      </p>

      {manifest && (
        <div
          style={{
            background: "var(--surface-2, #1a1a1a)",
            border: "1px solid var(--border)",
            borderRadius: "0.75rem",
            padding: "1rem 1.25rem",
            marginBottom: "2rem",
            fontSize: "0.875rem",
          }}
        >
          <strong>Version {manifest.version}</strong>
        </div>
      )}

      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {/* Offline Installer */}
        <div
          style={{
            background: "var(--surface)",
            border: "1px solid var(--border)",
            borderRadius: "1rem",
            padding: "1.5rem",
          }}
        >
          <h2 style={{ fontWeight: 700, fontSize: "1.1rem", marginBottom: "0.4rem" }}>
            ↓ Offline-Installer
          </h2>
          <p style={{ color: "var(--text-muted)", fontSize: "0.875rem", marginBottom: "1rem" }}>
            Komplettpaket – keine Internetverbindung während der Installation nötig.
          </p>
          <a
            href={`${RELEASE_BASE}/${OFFLINE_NAME}`}
            className="btn btn-primary"
            style={{ padding: "0.65rem 1.5rem", display: "inline-block" }}
          >
            {OFFLINE_NAME} herunterladen
          </a>
          {manifest?.installer.sha256 && (
            <div style={{ marginTop: "0.75rem", fontSize: "0.75rem", color: "var(--text-muted)" }}>
              SHA-256:{" "}
              <code style={{ fontFamily: "monospace", wordBreak: "break-all" }}>
                {manifest.installer.sha256}
              </code>{" "}
              <button
                onClick={() => copyToClipboard(manifest.installer.sha256, "offline")}
                style={{
                  background: "none",
                  border: "1px solid var(--border)",
                  borderRadius: "0.25rem",
                  padding: "0.1rem 0.4rem",
                  cursor: "pointer",
                  fontSize: "0.7rem",
                  color: "var(--text-muted)",
                }}
              >
                {copied === "offline" ? "✓ Kopiert" : "Kopieren"}
              </button>
            </div>
          )}
        </div>

        {/* Live Installer */}
        <div
          style={{
            background: "var(--surface)",
            border: "1px solid var(--border)",
            borderRadius: "1rem",
            padding: "1.5rem",
          }}
        >
          <h2 style={{ fontWeight: 700, fontSize: "1.1rem", marginBottom: "0.4rem" }}>
            ↓ Live-Installer
          </h2>
          <p style={{ color: "var(--text-muted)", fontSize: "0.875rem", marginBottom: "1rem" }}>
            Klein und schnell – lädt automatisch den neuesten Offline-Installer, prüft SHA-256 und startet die Installation.
          </p>
          <a
            href={`${RELEASE_BASE}/${LIVE_NAME}`}
            className="btn btn-secondary"
            style={{ padding: "0.65rem 1.5rem", display: "inline-block" }}
          >
            {LIVE_NAME} herunterladen
          </a>
          {manifest?.bootstrapper?.sha256 && (
            <div style={{ marginTop: "0.75rem", fontSize: "0.75rem", color: "var(--text-muted)" }}>
              SHA-256:{" "}
              <code style={{ fontFamily: "monospace", wordBreak: "break-all" }}>
                {manifest.bootstrapper.sha256}
              </code>{" "}
              <button
                onClick={() => copyToClipboard(manifest.bootstrapper!.sha256, "live")}
                style={{
                  background: "none",
                  border: "1px solid var(--border)",
                  borderRadius: "0.25rem",
                  padding: "0.1rem 0.4rem",
                  cursor: "pointer",
                  fontSize: "0.7rem",
                  color: "var(--text-muted)",
                }}
              >
                {copied === "live" ? "✓ Kopiert" : "Kopieren"}
              </button>
            </div>
          )}
        </div>
      </div>

      <p style={{ marginTop: "2rem", fontSize: "0.8rem", color: "var(--text-muted)" }}>
        Alle Releases:{" "}
        <a
          href="https://github.com/cOOnStar/pcwaechter-public-release/releases"
          target="_blank"
          rel="noopener noreferrer"
        >
          github.com/cOOnStar/pcwaechter-public-release
        </a>
      </p>
    </main>
  );
}
