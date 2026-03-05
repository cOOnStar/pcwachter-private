"use client";

import Link from "next/link";
import { signIn, signOut, useSession } from "next-auth/react";

export default function NavBar() {
  const { data: session } = useSession();

  return (
    <nav
      style={{
        background: "var(--surface)",
        borderBottom: "1px solid var(--border)",
        height: 64,
        display: "flex",
        alignItems: "center",
        position: "sticky",
        top: 0,
        zIndex: 100,
      }}
    >
      <div
        className="container"
        style={{ display: "flex", alignItems: "center", gap: "1rem", width: "100%" }}
      >
        <Link
          href="/account"
          style={{ fontWeight: 800, fontSize: "1.1rem", color: "var(--text)", letterSpacing: "-0.02em" }}
        >
          PCWächter
        </Link>

        <div style={{ marginLeft: "auto", display: "flex", gap: "0.75rem", alignItems: "center" }}>
          {session ? (
            <>
              <Link href="/account" className="btn btn-outline" style={{ fontSize: "0.85rem" }}>
                Mein Konto
              </Link>
              <button
                className="btn btn-outline"
                style={{ fontSize: "0.85rem" }}
                onClick={() => signOut()}
              >
                Abmelden
              </button>
            </>
          ) : (
            <button
              className="btn btn-primary"
              style={{ fontSize: "0.85rem" }}
              onClick={() => signIn("keycloak")}
            >
              Anmelden
            </button>
          )}
        </div>
      </div>
    </nav>
  );
}
