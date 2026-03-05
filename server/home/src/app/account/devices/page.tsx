"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import Spinner from "@/components/Spinner";
import EmptyState from "@/components/EmptyState";

interface Device {
  device_install_id: string;
  host_name: string | null;
  os_name: string | null;
  os_version: string | null;
  last_seen_at: string | null;
  online: boolean;
  primary_ip: string | null;
}

function formatDate(iso: string | null): string {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleString("de-DE", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return iso;
  }
}

function Card({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        background: "var(--surface)",
        border: "1px solid var(--border)",
        borderRadius: "0.75rem",
        padding: "1.25rem 1.5rem",
        display: "flex",
        flexDirection: "column",
        gap: "0.75rem",
      }}
    >
      {children}
    </div>
  );
}

export default function DevicesPage() {
  const [devices, setDevices] = useState<Device[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [renaming, setRenaming] = useState<string | null>(null);
  const [renameValue, setRenameValue] = useState("");
  const [renameLoading, setRenameLoading] = useState(false);
  const [revokeLoading, setRevokeLoading] = useState<string | null>(null);

  async function loadDevices() {
    setLoading(true);
    setError("");
    try {
      const res = await fetch("/api/home/devices");
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      setDevices(data.items ?? []);
    } catch (e: unknown) {
      setError(String(e));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { loadDevices(); }, []);

  async function handleRename(id: string) {
    if (!renameValue.trim()) return;
    setRenameLoading(true);
    try {
      const res = await fetch(`/api/home/devices/${encodeURIComponent(id)}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: renameValue.trim() }),
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      setRenaming(null);
      setRenameValue("");
      await loadDevices();
    } catch (e: unknown) {
      alert("Umbenennen fehlgeschlagen: " + String(e));
    } finally {
      setRenameLoading(false);
    }
  }

  async function handleRevoke(id: string, name: string | null) {
    const label = name ?? id;
    if (!confirm(`Gerät „${label}" wirklich entfernen? Die Lizenzaktivierung wird aufgehoben.`)) return;
    setRevokeLoading(id);
    try {
      const res = await fetch(`/api/home/devices/${encodeURIComponent(id)}`, {
        method: "DELETE",
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      await loadDevices();
    } catch (e: unknown) {
      alert("Fehler beim Entfernen: " + String(e));
    } finally {
      setRevokeLoading(null);
    }
  }

  return (
    <div>
      <h1 style={{ fontWeight: 800, fontSize: "1.5rem", marginBottom: "0.5rem" }}>
        Meine Geräte
      </h1>
      <p style={{ color: "var(--text-muted)", fontSize: "0.875rem", marginBottom: "2rem" }}>
        Alle Geräte, auf denen PCWächter mit Ihrer Lizenz aktiviert ist.
      </p>

      {loading && (
        <div style={{ display: "flex", justifyContent: "center", padding: "3rem 0" }}>
          <Spinner size={32} />
        </div>
      )}

      {!loading && error && (
        <div
          style={{
            background: "#450a0a",
            border: "1px solid #b91c1c",
            borderRadius: "0.75rem",
            padding: "1rem 1.25rem",
            color: "#fca5a5",
            fontSize: "0.875rem",
            marginBottom: "1rem",
          }}
        >
          Fehler beim Laden: {error}
        </div>
      )}

      {!loading && !error && devices.length === 0 && (
        <Card>
          <EmptyState
            icon="💻"
            title="Keine Geräte registriert"
            description="Installieren Sie PCWächter auf Ihrem Windows-PC, um es hier zu sehen."
            actionLabel="Jetzt herunterladen"
            actionHref="/download"
          />
        </Card>
      )}

      {!loading && devices.length > 0 && (
        <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
          {devices.map((d) => (
            <Card key={d.device_install_id}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "1rem", flexWrap: "wrap" }}>
                {/* Left: name + status */}
                <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
                  <span
                    style={{
                      display: "inline-block",
                      width: 10,
                      height: 10,
                      borderRadius: "50%",
                      background: d.online ? "var(--green)" : "var(--text-muted)",
                      flexShrink: 0,
                    }}
                  />
                  {renaming === d.device_install_id ? (
                    <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                      <input
                        value={renameValue}
                        onChange={(e) => setRenameValue(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === "Enter") handleRename(d.device_install_id);
                          if (e.key === "Escape") { setRenaming(null); setRenameValue(""); }
                        }}
                        autoFocus
                        style={{
                          background: "var(--surface2)",
                          border: "1px solid var(--border)",
                          borderRadius: "0.4rem",
                          padding: "0.3rem 0.6rem",
                          color: "var(--text)",
                          fontSize: "0.9rem",
                          width: 200,
                        }}
                      />
                      <button
                        className="btn btn-primary"
                        style={{ padding: "0.3rem 0.75rem", fontSize: "0.8rem" }}
                        onClick={() => handleRename(d.device_install_id)}
                        disabled={renameLoading}
                      >
                        {renameLoading ? "…" : "Speichern"}
                      </button>
                      <button
                        className="btn btn-outline"
                        style={{ padding: "0.3rem 0.75rem", fontSize: "0.8rem" }}
                        onClick={() => { setRenaming(null); setRenameValue(""); }}
                      >
                        Abbrechen
                      </button>
                    </div>
                  ) : (
                    <span style={{ fontWeight: 600 }}>
                      {d.host_name ?? <span style={{ color: "var(--text-muted)", fontWeight: 400 }}>Unbenannt</span>}
                    </span>
                  )}
                </div>

                {/* Actions */}
                {renaming !== d.device_install_id && (
                  <div style={{ display: "flex", gap: "0.5rem" }}>
                    <button
                      className="btn btn-outline"
                      style={{ padding: "0.3rem 0.75rem", fontSize: "0.8rem" }}
                      onClick={() => { setRenaming(d.device_install_id); setRenameValue(d.host_name ?? ""); }}
                    >
                      Umbenennen
                    </button>
                    <button
                      className="btn btn-outline"
                      style={{ padding: "0.3rem 0.75rem", fontSize: "0.8rem", color: "var(--red)", borderColor: "var(--red)" }}
                      onClick={() => handleRevoke(d.device_install_id, d.host_name)}
                      disabled={revokeLoading === d.device_install_id}
                    >
                      {revokeLoading === d.device_install_id ? "…" : "Entfernen"}
                    </button>
                  </div>
                )}
              </div>

              {/* Meta info */}
              <div
                style={{
                  display: "flex",
                  gap: "1.5rem",
                  flexWrap: "wrap",
                  fontSize: "0.8rem",
                  color: "var(--text-muted)",
                  borderTop: "1px solid var(--border)",
                  paddingTop: "0.75rem",
                }}
              >
                {d.os_name && (
                  <span>{d.os_name}{d.os_version ? ` ${d.os_version}` : ""}</span>
                )}
                {d.primary_ip && <span>IP: {d.primary_ip}</span>}
                <span>Zuletzt gesehen: {formatDate(d.last_seen_at)}</span>
                <span
                  style={{
                    color: d.online ? "var(--green)" : "var(--text-muted)",
                  }}
                >
                  {d.online ? "Online" : "Offline"}
                </span>
              </div>
            </Card>
          ))}
        </div>
      )}

      <div style={{ marginTop: "1.5rem", textAlign: "right" }}>
        <Link href="/download" style={{ color: "var(--text-muted)", fontSize: "0.875rem" }}>
          PCWächter herunterladen →
        </Link>
      </div>
    </div>
  );
}
