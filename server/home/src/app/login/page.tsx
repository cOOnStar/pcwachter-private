"use client";

import { signIn, useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";

export default function LoginPage() {
  const { status } = useSession();
  const router = useRouter();
  const startedRef = useRef(false);
  const [callbackUrl, setCallbackUrl] = useState("/account");

  useEffect(() => {
    const requested = new URLSearchParams(window.location.search).get("callbackUrl") || "/account";
    setCallbackUrl(requested.startsWith("/") ? requested : "/account");
  }, []);

  useEffect(() => {
    if (status === "authenticated") {
      router.replace(callbackUrl);
    }
  }, [callbackUrl, router, status]);

  useEffect(() => {
    if (status !== "unauthenticated" || startedRef.current) {
      return;
    }
    startedRef.current = true;
    void signIn("keycloak", { callbackUrl });
  }, [callbackUrl, status]);

  return (
    <main className="container" style={{ padding: "5rem 0", textAlign: "center" }}>
      <h1 style={{ fontSize: "1.5rem", marginBottom: "0.75rem" }}>Weiterleitung zur Anmeldung…</h1>
      <p style={{ color: "var(--text-muted)", marginBottom: "1.25rem" }}>
        Die Anmeldung mit Keycloak wird gestartet.
      </p>
      <button
        className="btn btn-primary"
        onClick={() => {
          void signIn("keycloak", { callbackUrl });
        }}
      >
        Anmeldung erneut starten
      </button>
    </main>
  );
}
