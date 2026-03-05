import NextAuth from "next-auth";
import KeycloakProvider from "next-auth/providers/keycloak";

async function refreshKeycloakToken(refreshToken: string): Promise<{
  accessToken: string;
  expiresAt: number;
  refreshToken: string;
} | null> {
  const issuer = process.env.AUTH_KEYCLOAK_ISSUER!;
  try {
    const res = await fetch(`${issuer}/protocol/openid-connect/token`, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({
        client_id: process.env.AUTH_KEYCLOAK_ID!,
        client_secret: process.env.AUTH_KEYCLOAK_SECRET!,
        grant_type: "refresh_token",
        refresh_token: refreshToken,
      }),
    });
    if (!res.ok) return null;
    const tokens = await res.json() as {
      access_token: string;
      expires_in: number;
      refresh_token?: string;
    };
    return {
      accessToken: tokens.access_token,
      expiresAt: Math.floor(Date.now() / 1000) + tokens.expires_in,
      refreshToken: tokens.refresh_token ?? refreshToken,
    };
  } catch {
    return null;
  }
}

export const { handlers, signIn, signOut, auth } = NextAuth({
  trustHost: process.env.AUTH_TRUST_HOST === "true",
  providers: [
    KeycloakProvider({
      clientId: process.env.AUTH_KEYCLOAK_ID!,
      clientSecret: process.env.AUTH_KEYCLOAK_SECRET!,
      issuer: process.env.AUTH_KEYCLOAK_ISSUER!,
    }),
  ],
  callbacks: {
    async redirect({ url, baseUrl }) {
      if (url.startsWith("/")) return `${baseUrl}${url}`;
      if (url.startsWith(baseUrl)) return url;
      return `${baseUrl}/account`;
    },
    async jwt({ token, account }) {
      if (account) {
        token.accessToken = account.access_token;
        token.refreshToken = account.refresh_token;
        token.expiresAt = account.expires_at; // Unix-Sekunden
        return token;
      }

      // Token noch gültig (30s Puffer)?
      const expiresAt = token.expiresAt as number | undefined;
      if (expiresAt && Date.now() < expiresAt * 1000 - 30_000) {
        return token;
      }

      // Token abgelaufen – erneuern
      const refreshToken = token.refreshToken as string | undefined;
      if (!refreshToken) return { ...token, error: "RefreshTokenError" };

      const refreshed = await refreshKeycloakToken(refreshToken);
      if (!refreshed) return { ...token, error: "RefreshTokenError" };

      return {
        ...token,
        accessToken: refreshed.accessToken,
        expiresAt: refreshed.expiresAt,
        refreshToken: refreshed.refreshToken,
        error: undefined,
      };
    },
    async session({ session, token }) {
      if (token.error === "RefreshTokenError") {
        session.accessToken = undefined;
        session.error = "RefreshTokenError";
      } else {
        session.accessToken = token.accessToken as string | undefined;
      }
      session.userId = token.sub ? String(token.sub) : "";
      return session;
    },
  },
});

declare module "next-auth" {
  interface Session {
    accessToken?: string;
    userId?: string;
    error?: string;
  }
}
