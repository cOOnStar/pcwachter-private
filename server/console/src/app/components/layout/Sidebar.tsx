import { NavLink } from "react-router-dom";
import {
  LayoutDashboard,
  Monitor,
  KeyRound,
  Radio,
  Users,
  CreditCard,
  ToggleLeft,
  ClipboardList,
  Bell,
  Server,
  LogOut,
  Activity,
  BookOpen,
  HeadphonesIcon,
} from "lucide-react";
import { useAuth } from "../../context/auth-context";
import { cn } from "@/lib/utils";

interface NavItem {
  path: string;
  label: string;
  icon: React.ReactNode;
  adminOnly?: boolean;
}

const NAV_ITEMS: NavItem[] = [
  { path: "/", label: "Dashboard", icon: <LayoutDashboard className="w-4 h-4" /> },
  { path: "/devices", label: "Geräte", icon: <Monitor className="w-4 h-4" /> },
  { path: "/licenses", label: "Lizenzen", icon: <KeyRound className="w-4 h-4" /> },
  { path: "/telemetry", label: "Telemetrie", icon: <Radio className="w-4 h-4" /> },
  { path: "/accounts", label: "Accounts", icon: <Users className="w-4 h-4" /> },
  { path: "/plans", label: "Pläne", icon: <CreditCard className="w-4 h-4" />, adminOnly: true },
  { path: "/features", label: "Feature Rollouts", icon: <ToggleLeft className="w-4 h-4" /> },
  { path: "/audit", label: "Audit Log", icon: <ClipboardList className="w-4 h-4" /> },
  { path: "/notifications", label: "Benachrichtigungen", icon: <Bell className="w-4 h-4" /> },
  { path: "/server", label: "Server", icon: <Server className="w-4 h-4" /> },
  { path: "/activity", label: "Activity Feed", icon: <Activity className="w-4 h-4" /> },
  { path: "/knowledge-base", label: "Knowledge Base", icon: <BookOpen className="w-4 h-4" /> },
  { path: "/support", label: "Support", icon: <HeadphonesIcon className="w-4 h-4" /> },
];

export default function Sidebar() {
  const { isAdmin, logout, userName } = useAuth();

  const visibleItems = NAV_ITEMS.filter((item) => !item.adminOnly || isAdmin());

  return (
    <aside
      className="fixed top-0 left-0 z-[100] flex flex-col min-h-screen bg-[var(--bg-surface)] border-r border-[var(--border)]"
      style={{ width: "var(--sidebar-width)" }}
    >
      <div className="flex items-center gap-2.5 px-4 py-5 border-b border-[var(--border-muted)] font-bold text-[var(--text-primary)]">
        <div className="w-7 h-7 bg-[var(--accent)] rounded-lg flex items-center justify-center text-xs font-bold text-white shrink-0">
          PC
        </div>
        <span className="text-[0.95rem]">PCWächter</span>
      </div>

      <nav className="flex-1 px-2 py-3 flex flex-col gap-0.5 overflow-y-auto">
        <span className="text-[0.68rem] text-[var(--text-muted)] uppercase tracking-widest px-2.5 py-2.5 pb-1">
          Navigation
        </span>
        {visibleItems.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            end={item.path === "/"}
            className={({ isActive }) =>
              cn(
                "flex items-center gap-2.5 px-2.5 py-2 rounded-lg text-sm transition-colors",
                isActive
                  ? "bg-[var(--accent-subtle)] text-[var(--accent-hover)]"
                  : "text-[var(--text-secondary)] hover:bg-[var(--bg-hover)] hover:text-[var(--text-primary)]"
              )
            }
          >
            {item.icon}
            {item.label}
          </NavLink>
        ))}
      </nav>

      <div className="px-2 py-3 border-t border-[var(--border-muted)]">
        <div className="text-[0.72rem] text-[var(--text-muted)] px-2.5 mb-2 truncate">{userName}</div>
        <button
          onClick={logout}
          className="flex items-center gap-2.5 px-2.5 py-2 rounded-lg text-sm text-[var(--text-secondary)] hover:bg-[var(--bg-hover)] hover:text-[var(--text-primary)] transition-colors w-full"
        >
          <LogOut className="w-4 h-4" />
          Abmelden
        </button>
      </div>
    </aside>
  );
}
