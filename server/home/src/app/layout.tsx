import type { Metadata } from "next";
import "./globals.css";
import NavBar from "@/components/NavBar";
import Footer from "@/components/Footer";
import { SessionProvider } from "next-auth/react";

export const metadata: Metadata = {
  title: "PCWächter – PC-Monitoring für Windows",
  description:
    "Echtzeit-Überwachung, automatische Problemlösung und detaillierte Berichte für Ihren Windows-PC.",
  icons: { icon: "/favicon.ico" },
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="de">
      <body>
        <SessionProvider>
          <NavBar />
          <div style={{ minHeight: "calc(100vh - 64px)" }}>{children}</div>
          <Footer />
        </SessionProvider>
      </body>
    </html>
  );
}
