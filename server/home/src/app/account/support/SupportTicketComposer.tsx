"use client";

import { useEffect, useState } from "react";

type SupportGroup = {
  id: number;
  name: string;
};

type SupportConfig = {
  support_available: boolean;
  zammad_reachable: boolean;
  zammad_error?: string;
  allow_customer_group_selection: boolean;
  default_group_id: number | null;
  groups: SupportGroup[];
  uploads_enabled: boolean;
  uploads_max_bytes: number;
  maintenance_mode: boolean;
  maintenance_message: string;
};

function formatBytesToMb(bytes: number): string {
  return (bytes / (1024 * 1024)).toFixed(1);
}

export default function SupportTicketComposer() {
  const [config, setConfig] = useState<SupportConfig | null>(null);
  const [title, setTitle] = useState("");
  const [message, setMessage] = useState("");
  const [selectedGroupId, setSelectedGroupId] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await fetch("/api/home/support-ticket", { cache: "no-store" });
        const data = await response.json().catch(() => ({}));
        if (!active) return;
        if (response.ok) {
          const nextConfig = data as SupportConfig;
          setConfig(nextConfig);
          if (!nextConfig.support_available) {
            setError("Support ist momentan nicht verfuegbar.");
          } else if (!nextConfig.zammad_reachable) {
            setError("Support ist momentan nicht erreichbar. Bitte versuchen Sie es spaeter erneut.");
          }
          const initialGroupId =
            typeof nextConfig.default_group_id === "number"
              ? String(nextConfig.default_group_id)
              : nextConfig.groups[0]
                ? String(nextConfig.groups[0].id)
                : "";
          setSelectedGroupId(initialGroupId);
          return;
        }
        setError((data as { error?: string }).error ?? "Support-Konfiguration konnte nicht geladen werden.");
      } catch {
        if (!active) return;
        setError("Support-Konfiguration konnte nicht geladen werden.");
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  const uploadsLabel = config?.uploads_enabled
    ? `Maximal ${formatBytesToMb(config.uploads_max_bytes)} MB pro Datei`
    : "";
  const supportBlocked = !!config && (!config.support_available || !config.zammad_reachable || config.maintenance_mode);

  async function uploadSelectedFiles(): Promise<string[]> {
    if (!files.length) return [];

    const uploadedIds: string[] = [];
    for (const file of files) {
      const formData = new FormData();
      formData.append("file", file);
      const response = await fetch("/api/home/support-ticket/attachments", {
        method: "POST",
        body: formData,
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error((data as { error?: string }).error ?? "attachment_upload_failed");
      }
      const attachmentId = (data as { id?: string }).id;
      if (!attachmentId) {
        throw new Error("attachment_upload_failed");
      }
      uploadedIds.push(attachmentId);
    }
    return uploadedIds;
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    if (!title.trim() || !message.trim()) return;
    if (supportBlocked) return;

    setLoading(true);
    setError("");
    try {
      const attachmentIds = config?.uploads_enabled ? await uploadSelectedFiles() : [];
      const response = await fetch("/api/home/support-ticket", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          title: title.trim(),
          body: message.trim(),
          group_id: selectedGroupId ? Number(selectedGroupId) : null,
          attachment_ids: attachmentIds,
        }),
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        const nextError = (data as { error?: string }).error ?? "Fehler beim Erstellen des Tickets.";
        if (nextError === "support_not_configured") {
          setError("Support ist momentan nicht verfuegbar. Bitte kontaktieren Sie uns direkt per E-Mail.");
        } else if (nextError === "support_uploads_disabled") {
          setError("Dateiupload ist derzeit deaktiviert.");
        } else {
          setError(nextError);
        }
        return;
      }

      setSuccess(true);
      setTitle("");
      setMessage("");
      setFiles([]);
    } catch (submitError) {
      const messageText = submitError instanceof Error ? submitError.message : "";
      if (messageText === "support_uploads_disabled") {
        setError("Dateiupload ist derzeit deaktiviert.");
      } else if (messageText === "attachment_too_large") {
        setError("Mindestens eine Datei ist groesser als das erlaubte Limit.");
      } else {
        setError("Netzwerkfehler. Bitte versuchen Sie es erneut.");
      }
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

      {config?.maintenance_mode && (
        <div
          style={{
            background: "#3f1d0d",
            border: "1px solid #ea580c",
            borderRadius: "0.75rem",
            padding: "1rem 1.25rem",
            color: "#fdba74",
            marginBottom: "1.5rem",
            fontSize: "0.875rem",
          }}
        >
          {config.maintenance_message}
        </div>
      )}

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
            disabled={loading || supportBlocked}
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

        {config?.allow_customer_group_selection && config.groups.length > 0 && (
          <div>
            <label
              htmlFor="group"
              style={{ display: "block", fontSize: "0.85rem", fontWeight: 600, marginBottom: "0.4rem" }}
            >
              Kategorie
            </label>
            <select
              id="group"
              value={selectedGroupId}
              onChange={(event) => setSelectedGroupId(event.target.value)}
              disabled={loading || supportBlocked}
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
              {config.groups.map((group) => (
                <option key={group.id} value={group.id}>
                  {group.name}
                </option>
              ))}
            </select>
          </div>
        )}

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
            disabled={loading || supportBlocked}
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

        {config?.uploads_enabled && (
          <div>
            <label
              htmlFor="attachments"
              style={{ display: "block", fontSize: "0.85rem", fontWeight: 600, marginBottom: "0.4rem" }}
            >
              Dateien
            </label>
            <input
              id="attachments"
              type="file"
              multiple
              onChange={(event) => setFiles(Array.from(event.target.files ?? []))}
              disabled={loading || supportBlocked}
              style={{ color: "var(--text-muted)", fontSize: "0.85rem" }}
            />
            {uploadsLabel && (
              <div style={{ marginTop: "0.35rem", fontSize: "0.75rem", color: "var(--text-muted)" }}>{uploadsLabel}</div>
            )}
            {files.length > 0 && (
              <div style={{ marginTop: "0.75rem", display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                {files.map((file) => (
                  <div
                    key={`${file.name}-${file.size}`}
                    style={{
                      background: "var(--surface2)",
                      border: "1px solid var(--border)",
                      borderRadius: "0.5rem",
                      padding: "0.55rem 0.75rem",
                      fontSize: "0.8rem",
                      color: "var(--text-muted)",
                    }}
                  >
                    {file.name} ({formatBytesToMb(file.size)} MB)
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        <div style={{ display: "flex", gap: "1rem", alignItems: "center", flexWrap: "wrap" }}>
          <button
            type="submit"
            className="btn btn-primary"
            disabled={loading || !title.trim() || !message.trim() || supportBlocked}
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
