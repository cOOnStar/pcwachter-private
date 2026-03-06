"use client";

import { useEffect, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import type { AccountProfile } from "@/lib/api";

function normalizeEmail(value: string | null | undefined): string {
  return (value ?? "").trim().toLowerCase();
}

function displayName(profile: AccountProfile): string {
  const fullName = [profile.first_name, profile.last_name].filter(Boolean).join(" ").trim();
  return fullName || profile.name || profile.email || "Unbekannt";
}

function initials(profile: AccountProfile): string {
  return displayName(profile)
    .split(" ")
    .map((part) => part[0] ?? "")
    .join("")
    .toUpperCase()
    .slice(0, 2);
}

function warningMessage(warnings: string[] | undefined): string {
  if (!warnings || warnings.length === 0) return "";
  if (warnings.includes("support_sync_unreachable")) {
    return "Das Profil wurde gespeichert, aber die Support-Zuordnung konnte gerade nicht mit aktualisiert werden.";
  }
  if (warnings.includes("support_sync_failed")) {
    return "Das Profil wurde gespeichert, aber die Support-Zuordnung konnte nicht vollstaendig synchronisiert werden.";
  }
  return "";
}

export default function ProfileEditor({
  initialProfile,
}: {
  initialProfile: AccountProfile;
}) {
  const router = useRouter();
  const [isRefreshing, startTransition] = useTransition();
  const [profile, setProfile] = useState(initialProfile);
  const [firstName, setFirstName] = useState(initialProfile.first_name ?? "");
  const [lastName, setLastName] = useState(initialProfile.last_name ?? "");
  const [email, setEmail] = useState(initialProfile.email ?? "");
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [info, setInfo] = useState("");
  const [warning, setWarning] = useState(warningMessage(initialProfile.warnings));
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    setProfile(initialProfile);
    setFirstName(initialProfile.first_name ?? "");
    setLastName(initialProfile.last_name ?? "");
    setEmail(initialProfile.email ?? "");
    setWarning(warningMessage(initialProfile.warnings));
  }, [
    initialProfile.email,
    initialProfile.first_name,
    initialProfile.last_name,
    initialProfile.name,
    initialProfile.sub,
    initialProfile.warnings,
  ]);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const submittedEmail = email.trim();
    const submittedFirstName = firstName.trim();
    const submittedLastName = lastName.trim();

    setError("");
    setSuccess("");
    setInfo("");
    setWarning("");
    setIsSubmitting(true);

    try {
      const response = await fetch("/api/home/profile", {
        method: "PATCH",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          email: submittedEmail,
          first_name: submittedFirstName,
          last_name: submittedLastName,
        }),
      });

      const data = (await response.json().catch(() => ({}))) as
        | (AccountProfile & { error?: string })
        | { error?: string };

      if (!response.ok) {
        const errorCode = "error" in data ? data.error : "";
        if (errorCode === "email_already_exists") {
          setError("Diese E-Mail-Adresse wird bereits verwendet.");
          return;
        }
        if (errorCode === "profile_update_unavailable") {
          setError("Profilbearbeitung ist momentan nicht verfuegbar.");
          return;
        }
        setError("Profil konnte nicht gespeichert werden.");
        return;
      }

      const nextProfile = data as AccountProfile;
      const emailChanged = normalizeEmail(profile.email) !== normalizeEmail(nextProfile.email);
      setProfile(nextProfile);
      setFirstName(nextProfile.first_name ?? "");
      setLastName(nextProfile.last_name ?? "");
      setEmail(nextProfile.email ?? "");
      setSuccess(
        emailChanged
          ? "Profil gespeichert. Die neue E-Mail-Adresse wird ab sofort fuer Ihr Konto verwendet."
          : "Profil gespeichert."
      );
      setInfo(
        emailChanged
          ? "Falls spaeter eine neue Anmeldung noetig ist, verwenden Sie bitte die aktualisierte E-Mail-Adresse."
          : ""
      );
      setWarning(warningMessage(nextProfile.warnings));
      startTransition(() => {
        router.refresh();
      });
    } catch {
      setError("Netzwerkfehler. Bitte versuchen Sie es erneut.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div
      style={{
        background: "var(--surface)",
        border: "1px solid var(--border)",
        borderRadius: "0.75rem",
        padding: "1.5rem",
      }}
    >
      <h2 style={{ fontWeight: 700, fontSize: "1rem", marginBottom: "1.25rem" }}>Profil</h2>

      <div
        style={{
          display: "flex",
          gap: "1rem",
          alignItems: "center",
          marginBottom: "1.5rem",
          flexWrap: "wrap",
        }}
      >
        <div
          style={{
            width: 52,
            height: 52,
            borderRadius: "50%",
            background: "var(--blue)",
            color: "#fff",
            fontWeight: 800,
            fontSize: "1.1rem",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            flexShrink: 0,
          }}
        >
          {initials(profile)}
        </div>
        <div>
          <div style={{ fontWeight: 700 }}>{displayName(profile)}</div>
          {profile.email && (
            <div style={{ color: "var(--text-muted)", fontSize: "0.875rem" }}>{profile.email}</div>
          )}
        </div>
      </div>

      {success && (
        <div
          style={{
            background: "#14532d",
            border: "1px solid #16a34a",
            borderRadius: "0.75rem",
            padding: "0.875rem 1rem",
            color: "#4ade80",
            marginBottom: "1rem",
            fontSize: "0.875rem",
          }}
        >
          {success}
        </div>
      )}

      {info && (
        <div
          style={{
            background: "#1e3a5f",
            border: "1px solid #2563eb",
            borderRadius: "0.75rem",
            padding: "0.875rem 1rem",
            color: "#93c5fd",
            marginBottom: "1rem",
            fontSize: "0.875rem",
          }}
        >
          {info}
        </div>
      )}

      {warning && (
        <div
          style={{
            background: "#4a3200",
            border: "1px solid #eab308",
            borderRadius: "0.75rem",
            padding: "0.875rem 1rem",
            color: "#fde047",
            marginBottom: "1rem",
            fontSize: "0.875rem",
          }}
        >
          {warning}
        </div>
      )}

      {error && (
        <div
          style={{
            background: "#450a0a",
            border: "1px solid #b91c1c",
            borderRadius: "0.75rem",
            padding: "0.875rem 1rem",
            color: "#fca5a5",
            marginBottom: "1rem",
            fontSize: "0.875rem",
          }}
        >
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div
          style={{
            display: "grid",
            gap: "1rem",
            gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
          }}
        >
          <div>
            <label
              htmlFor="firstName"
              style={{ display: "block", fontSize: "0.85rem", fontWeight: 600, marginBottom: "0.4rem" }}
            >
              Vorname
            </label>
            <input
              id="firstName"
              type="text"
              value={firstName}
              onChange={(event) => setFirstName(event.target.value)}
              maxLength={255}
              placeholder="Optional"
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

          <div>
            <label
              htmlFor="lastName"
              style={{ display: "block", fontSize: "0.85rem", fontWeight: 600, marginBottom: "0.4rem" }}
            >
              Nachname
            </label>
            <input
              id="lastName"
              type="text"
              value={lastName}
              onChange={(event) => setLastName(event.target.value)}
              maxLength={255}
              placeholder="Optional"
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
        </div>

        <div>
          <label
            htmlFor="email"
            style={{ display: "block", fontSize: "0.85rem", fontWeight: 600, marginBottom: "0.4rem" }}
          >
            E-Mail-Adresse
          </label>
          <input
            id="email"
            type="email"
            required
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            maxLength={254}
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

        <div style={{ color: "var(--text-muted)", fontSize: "0.8rem", lineHeight: 1.6 }}>
          Vorname und Nachname sind freiwillig. Nach einer E-Mail-Aenderung kann eine erneute
          Bestaetigung im Login-System notwendig sein.
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end" }}>
          <button
            type="submit"
            className="btn btn-primary"
            disabled={isSubmitting || isRefreshing || !email.trim()}
            style={{ opacity: isSubmitting || isRefreshing ? 0.7 : 1 }}
          >
            {isSubmitting || isRefreshing ? "Speichert..." : "Profil speichern"}
          </button>
        </div>
      </form>
    </div>
  );
}
