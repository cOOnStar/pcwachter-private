import { auth } from "@/auth";
import { redirect } from "next/navigation";
import Link from "next/link";

export default async function AccountLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const session = await auth();

  if (!session) {
    redirect("/api/auth/signin/keycloak?callbackUrl=%2Faccount");
  }

  return (
    <div style={{ padding: "3rem 0" }}>
      <div className="container">
        <div style={{ display: "flex", gap: "2.5rem", alignItems: "flex-start" }}>
          {/* Sidebar */}
          <nav
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
            {[
              { href: "/account", label: "Übersicht" },
              { href: "/account/billing", label: "Abrechnung" },
            ].map(({ href, label }) => (
              <Link
                key={href}
                href={href}
                style={{
                  padding: "0.5rem 0.75rem",
                  borderRadius: "0.5rem",
                  fontSize: "0.9rem",
                  color: "var(--text)",
                  display: "block",
                }}
              >
                {label}
              </Link>
            ))}
          </nav>

          {/* Content */}
          <div style={{ flex: 1 }}>{children}</div>
        </div>
      </div>
    </div>
  );
}
