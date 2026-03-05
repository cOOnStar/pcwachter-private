"use client";

import { useSearchParams } from "next/navigation";

export default function SuccessBanner() {
  const params = useSearchParams();
  if (!params.get("success")) return null;

  return (
    <div
      style={{
        background: "#14532d",
        border: "1px solid #16a34a",
        borderRadius: "0.75rem",
        padding: "1rem 1.25rem",
        color: "#4ade80",
        marginBottom: "1.5rem",
        display: "flex",
        gap: "0.75rem",
        alignItems: "center",
        fontSize: "0.9rem",
      }}
    >
      <span style={{ fontSize: "1.2rem" }}>✓</span>
      <span>
        <strong>Zahlung erfolgreich!</strong> Ihre Lizenz wurde aktiviert und ist
        sofort einsatzbereit.
      </span>
    </div>
  );
}
