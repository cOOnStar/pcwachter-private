import { useLocation } from "react-router-dom";
import { useAuth } from "../../context/auth-context";
import { initials } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";

const PAGE_TITLES: Record<string, string> = {
  "/": "Dashboard",
  "/devices": "Geräte",
  "/licenses": "Lizenzen",
  "/telemetry": "Telemetrie",
  "/accounts": "Accounts",
  "/plans": "Pläne",
  "/features": "Feature Rollouts",
  "/audit": "Audit Log",
  "/notifications": "Benachrichtigungen",
  "/server": "Server",
};

export default function TopBar() {
  const { pathname } = useLocation();
  const { userName, userEmail, roles } = useAuth();

  const basePath = "/" + pathname.split("/")[1];
  const title = PAGE_TITLES[basePath] ?? "Admin";
  const abbr = initials(userName || userEmail || "?");

  const roleLabel = roles.includes("pcw_admin") || roles.includes("owner") || roles.includes("admin")
    ? "Admin"
    : roles.includes("pcw_console")
    ? "Console"
    : roles.includes("pcw_support")
    ? "Support"
    : "User";

  return (
    <header
      className="h-14 border-b border-[var(--border)] flex items-center justify-between px-6 shrink-0 sticky top-0 z-[90]"
      style={{
        background: "rgba(16,16,24,0.75)",
        backdropFilter: "blur(14px)",
        WebkitBackdropFilter: "blur(14px)",
      }}
    >
      <span className="text-sm font-semibold text-[var(--text-primary)] tracking-tight">{title}</span>
      <div className="flex items-center gap-3">
        <Badge variant="accent">{roleLabel}</Badge>
        <div className="flex items-center gap-2 text-sm text-[var(--text-secondary)]">
          <div
            className="w-7 h-7 rounded-full flex items-center justify-center text-[0.72rem] font-bold text-white shrink-0"
            style={{ background: "linear-gradient(135deg, #6e6eff 0%, #a78bff 100%)" }}
          >
            {abbr}
          </div>
          <span className="text-[0.85rem]">{userName || userEmail}</span>
        </div>
      </div>
    </header>
  );
}
