"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";

const NAV_ITEMS = [
  { href: "/account", label: "Übersicht", exact: true },
  { href: "/account/devices", label: "Geräte" },
  { href: "/account/billing", label: "Abrechnung" },
  { href: "/account/support", label: "Support" },
  { href: "/download", label: "Download" },
];

export default function AccountNav() {
  const pathname = usePathname();
  const [supportUnreadCount, setSupportUnreadCount] = useState(0);

  useEffect(() => {
    let active = true;

    async function loadSupportBadge() {
      try {
        const response = await fetch(
          "/api/home/notifications?limit=1&unread_only=true&type_prefix=support.",
          { cache: "no-store" }
        );
        if (!response.ok) return;
        const data = (await response.json().catch(() => ({}))) as { total?: number };
        if (!active) return;
        setSupportUnreadCount(typeof data.total === "number" ? data.total : 0);
      } catch {
        if (!active) return;
        setSupportUnreadCount(0);
      }
    }

    const intervalId = window.setInterval(() => {
      if (document.visibilityState === "visible") {
        void loadSupportBadge();
      }
    }, 5000);

    const handleExternalRefresh = () => {
      void loadSupportBadge();
    };

    window.addEventListener("pcw-support-notifications-changed", handleExternalRefresh);
    void loadSupportBadge();

    return () => {
      active = false;
      window.clearInterval(intervalId);
      window.removeEventListener("pcw-support-notifications-changed", handleExternalRefresh);
    };
  }, []);

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
        const showSupportBadge = href === "/account/support" && supportUnreadCount > 0;
        return (
          <Link
            key={href}
            href={href}
            style={{
              padding: "0.5rem 0.75rem",
              borderRadius: "0.5rem",
              fontSize: "0.9rem",
              color: isActive ? "var(--text)" : "var(--text-muted)",
              background: isActive ? "var(--surface2)" : "transparent",
              fontWeight: isActive ? 600 : 400,
              borderLeft: isActive ? "2px solid var(--blue)" : "2px solid transparent",
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              gap: "0.75rem",
            }}
          >
            <span>{label}</span>
            {showSupportBadge ? (
              <span
                style={{
                  minWidth: "1.4rem",
                  height: "1.4rem",
                  borderRadius: "9999px",
                  background: "rgba(30, 58, 95, 0.7)",
                  color: "#93c5fd",
                  fontSize: "0.75rem",
                  fontWeight: 700,
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  padding: "0 0.35rem",
                }}
              >
                {supportUnreadCount > 99 ? "99+" : supportUnreadCount}
              </span>
            ) : null}
          </Link>
        );
      })}
    </nav>
  );
}
