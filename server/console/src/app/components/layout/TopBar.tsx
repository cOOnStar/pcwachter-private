import { useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../../context/auth-context";
import { initials } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Bell, ChevronRight, LayoutDashboard } from "lucide-react";

const ROUTE_LABELS: Record<string, string> = {
  "/": "Dashboard",
  "/devices": "Geräte",
  "/licenses": "Lizenzen",
  "/telemetry": "Telemetrie",
  "/accounts": "Accounts",
  "/plans": "Pläne",
  "/subscriptions": "Abonnements",
  "/features": "Feature Rollouts",
  "/audit": "Audit Log",
  "/notifications": "Benachrichtigungen",
  "/server": "Server",
  "/activity": "Activity Feed",
  "/knowledge-base": "Knowledge Base",
  "/support": "Support",
  "/updates": "Updates",
  "/rules": "Regeln & Findings",
};

export default function TopBar() {
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const { userName, userEmail, roles } = useAuth();

  const segments = pathname.split("/").filter(Boolean);
  const basePath = "/" + (segments[0] ?? "");
  const pageLabel = ROUTE_LABELS[basePath] ?? segments[0] ?? "Admin";
  const isSubPage = segments.length > 1;
  const abbr = initials(userName || userEmail || "?");

  const roleLabel =
    roles.includes("pcw_admin") || roles.includes("owner") || roles.includes("admin")
      ? "Admin"
      : roles.includes("pcw_console")
      ? "Console"
      : roles.includes("pcw_support")
      ? "Support"
      : "User";

  const roleVariant =
    roleLabel === "Admin" ? "accent" : roleLabel === "Support" ? "warning" : "neutral";

  return (
    <header
      className="h-14 flex items-center justify-between px-6 shrink-0 sticky top-0 z-[90] gap-4"
      style={{
        background: "rgba(7,7,16,0.88)",
        backdropFilter: "blur(20px)",
        WebkitBackdropFilter: "blur(20px)",
        borderBottom: "1px solid rgba(255,255,255,0.04)",
      }}
    >
      <div className="flex items-center gap-1.5 text-sm min-w-0">
        <button
          onClick={() => navigate("/")}
          className="text-[var(--text-muted)] hover:text-[var(--text-secondary)] transition-colors flex items-center"
        >
          <LayoutDashboard className="w-3.5 h-3.5" />
        </button>
        {basePath !== "/" && (
          <>
            <ChevronRight className="w-3 h-3 text-[var(--text-muted)] shrink-0" />
            <span className={isSubPage ? "text-[var(--text-secondary)]" : "text-[var(--text-primary)] font-semibold"}>
              {pageLabel}
            </span>
          </>
        )}
        {isSubPage && (
          <>
            <ChevronRight className="w-3 h-3 text-[var(--text-muted)] shrink-0" />
            <span className="text-[var(--text-primary)] font-semibold truncate max-w-[180px]">
              {segments[1]}
            </span>
          </>
        )}
      </div>

      <div className="flex items-center gap-3 shrink-0">
        <button
          onClick={() => navigate("/notifications")}
          className="w-8 h-8 rounded-lg flex items-center justify-center text-[var(--text-muted)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-hover)] transition-all"
        >
          <Bell className="w-4 h-4" />
        </button>
        <div className="w-px h-5 bg-[var(--border-muted)]" />
        <div className="flex items-center gap-2.5">
          <Badge variant={roleVariant as "accent" | "warning" | "neutral"}>{roleLabel}</Badge>
          <div
            className="w-7 h-7 rounded-full flex items-center justify-center text-[0.7rem] font-bold text-white shrink-0 select-none"
            style={{ background: "linear-gradient(135deg, #7c5cfc 0%, #9478fd 100%)" }}
          >
            {abbr}
          </div>
          <span className="text-[0.82rem] text-[var(--text-secondary)] max-w-[120px] truncate hidden sm:block">
            {userName || userEmail}
          </span>
        </div>
      </div>
    </header>
  );
}
