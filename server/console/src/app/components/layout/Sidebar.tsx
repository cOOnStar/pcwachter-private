import { NavLink } from "react-router-dom";
import {
  LayoutDashboard,
  Monitor,
  Radio,
  KeyRound,
  Receipt,
  Users,
  CreditCard,
  ShieldAlert,
  ToggleLeft,
  ClipboardList,
  Bell,
  Server,
  Activity,
  BookOpen,
  HeadphonesIcon,
  Download,
  LogOut,
  Zap,
} from "lucide-react";
import { useAuth } from "../../context/auth-context";
import { cn } from "@/lib/utils";

interface NavItem {
  path: string;
  label: string;
  icon: React.ReactNode;
  adminOnly?: boolean;
}

interface NavGroup {
  label: string | null;
  items: NavItem[];
}

const NAV_GROUPS: NavGroup[] = [
  {
    label: null,
    items: [
      { path: "/", label: "Dashboard", icon: <LayoutDashboard className="w-4 h-4" /> },
      { path: "/devices", label: "Geräte", icon: <Monitor className="w-4 h-4" /> },
      { path: "/telemetry", label: "Telemetrie", icon: <Radio className="w-4 h-4" /> },
    ],
  },
  {
    label: "Verwaltung",
    items: [
      { path: "/licenses", label: "Lizenzen", icon: <KeyRound className="w-4 h-4" /> },
      { path: "/subscriptions", label: "Abonnements", icon: <Receipt className="w-4 h-4" />, adminOnly: true },
      { path: "/accounts", label: "Accounts", icon: <Users className="w-4 h-4" /> },
      { path: "/plans", label: "Pläne", icon: <CreditCard className="w-4 h-4" />, adminOnly: true },
    ],
  },
  {
    label: "Sicherheit",
    items: [
      { path: "/rules", label: "Regeln & Findings", icon: <ShieldAlert className="w-4 h-4" /> },
      { path: "/features", label: "Feature Rollouts", icon: <ToggleLeft className="w-4 h-4" /> },
      { path: "/audit", label: "Audit Log", icon: <ClipboardList className="w-4 h-4" /> },
    ],
  },
  {
    label: "System",
    items: [
      { path: "/updates", label: "Updates", icon: <Download className="w-4 h-4" />, adminOnly: true },
      { path: "/notifications", label: "Benachrichtigungen", icon: <Bell className="w-4 h-4" /> },
      { path: "/server", label: "Server", icon: <Server className="w-4 h-4" /> },
      { path: "/activity", label: "Activity Feed", icon: <Activity className="w-4 h-4" /> },
      { path: "/knowledge-base", label: "Knowledge Base", icon: <BookOpen className="w-4 h-4" /> },
      { path: "/support", label: "Support", icon: <HeadphonesIcon className="w-4 h-4" /> },
    ],
  },
];

export default function Sidebar() {
  const { isAdmin, logout, userName } = useAuth();

  return (
    <aside
      className="fixed top-0 left-0 z-[100] flex flex-col min-h-screen"
      style={{
        width: "var(--sidebar-width)",
        background: "linear-gradient(180deg, #0b0b1c 0%, #080810 100%)",
        borderRight: "1px solid rgba(255,255,255,0.05)",
        boxShadow: "4px 0 24px rgba(0,0,0,0.5)",
      }}
    >
      {/* Brand */}
      <div className="flex items-center gap-3 px-5 py-5 shrink-0" style={{ borderBottom: "1px solid rgba(255,255,255,0.04)" }}>
        <div
          className="w-8 h-8 rounded-xl flex items-center justify-center shrink-0 relative overflow-hidden"
          style={{ background: "linear-gradient(135deg, #7c5cfc 0%, #9478fd 100%)" }}
        >
          <Zap className="w-4 h-4 text-white relative z-10" />
          <div className="absolute inset-0" style={{ background: "radial-gradient(circle at 30% 30%, rgba(255,255,255,0.2), transparent 70%)" }} />
        </div>
        <div className="flex flex-col">
          <span className="text-[0.88rem] font-bold text-[var(--text-primary)] tracking-tight leading-none">PCWächter</span>
          <span className="text-[0.65rem] text-[var(--text-muted)] mt-0.5 tracking-wider uppercase">Admin Console</span>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto px-3 py-4 flex flex-col gap-5">
        {NAV_GROUPS.map((group, gi) => {
          const visible = group.items.filter((item) => !item.adminOnly || isAdmin());
          if (visible.length === 0) return null;
          return (
            <div key={gi} className="flex flex-col gap-0.5">
              {group.label && (
                <span className="text-[0.62rem] font-semibold uppercase tracking-[0.1em] text-[var(--text-muted)] px-3 mb-1">
                  {group.label}
                </span>
              )}
              {visible.map((item) => (
                <NavLink
                  key={item.path}
                  to={item.path}
                  end={item.path === "/"}
                  className={({ isActive }) =>
                    cn(
                      "flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-all duration-150 group relative",
                      isActive
                        ? "text-white font-medium"
                        : "text-[var(--text-secondary)] hover:text-[var(--text-primary)]"
                    )
                  }
                  style={({ isActive }) =>
                    isActive
                      ? {
                          background: "linear-gradient(90deg, rgba(124,92,252,0.18) 0%, rgba(124,92,252,0.05) 100%)",
                          borderLeft: "2px solid var(--accent)",
                          paddingLeft: "10px",
                        }
                      : {
                          borderLeft: "2px solid transparent",
                        }
                  }
                >
                  {({ isActive }) => (
                    <>
                      <span style={{ color: isActive ? "var(--accent-hover)" : undefined }} className="shrink-0">
                        {item.icon}
                      </span>
                      <span className="truncate">{item.label}</span>
                    </>
                  )}
                </NavLink>
              ))}
            </div>
          );
        })}
      </nav>

      {/* Footer */}
      <div className="shrink-0 px-3 py-4" style={{ borderTop: "1px solid rgba(255,255,255,0.04)" }}>
        <div className="flex items-center gap-2 px-3 py-2 mb-1 rounded-lg" style={{ background: "rgba(255,255,255,0.025)" }}>
          <div
            className="w-6 h-6 rounded-full flex items-center justify-center text-[0.65rem] font-bold text-white shrink-0"
            style={{ background: "linear-gradient(135deg, #7c5cfc 0%, #9478fd 100%)" }}
          >
            {userName?.charAt(0).toUpperCase() ?? "?"}
          </div>
          <span className="text-[0.78rem] text-[var(--text-secondary)] truncate flex-1">{userName ?? "—"}</span>
        </div>
        <button
          onClick={logout}
          className="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm text-[var(--text-muted)] hover:text-[var(--danger)] hover:bg-[var(--danger-subtle)] transition-all w-full mt-0.5"
        >
          <LogOut className="w-3.5 h-3.5" />
          Abmelden
        </button>
      </div>
    </aside>
  );
}
