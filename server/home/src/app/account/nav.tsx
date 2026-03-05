"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const NAV_ITEMS = [
  { href: "/account", label: "Übersicht", exact: true },
  { href: "/account/devices", label: "Geräte" },
  { href: "/account/billing", label: "Abrechnung" },
  { href: "/account/support", label: "Support" },
  { href: "/download", label: "Download" },
];

export default function AccountNav() {
  const pathname = usePathname();

  return (
    <nav
      className="account-nav"
      style={{
        width: 200,
        flexShrink: 0,
        background: "var(--surface)",
        border: "1px solid var(--border)",
        borderRadius: "0.75rem",
        padding: "1rem",
        display: "flex",
        flexDirection: "column",
        gap: "0.25rem",
        alignSelf: "flex-start",
        position: "sticky",
        top: "1.5rem",
      }}
    >
      <div
        style={{
          fontSize: "0.75rem",
          fontWeight: 700,
          color: "var(--text-muted)",
          textTransform: "uppercase",
          letterSpacing: "0.05em",
          padding: "0.25rem 0.5rem",
          marginBottom: "0.25rem",
        }}
      >
        Konto
      </div>
      {NAV_ITEMS.map(({ href, label, exact }) => {
        const isActive = exact ? pathname === href : pathname.startsWith(href);
        return (
          <Link
            key={href}
            href={href}
            style={{
              padding: "0.5rem 0.75rem",
              borderRadius: "0.5rem",
              fontSize: "0.9rem",
              color: isActive ? "var(--text)" : "var(--text-muted)",
              display: "block",
              background: isActive ? "var(--surface2)" : "transparent",
              fontWeight: isActive ? 600 : 400,
              borderLeft: isActive ? "2px solid var(--blue)" : "2px solid transparent",
            }}
          >
            {label}
          </Link>
        );
      })}
    </nav>
  );
}
