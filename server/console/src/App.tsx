import { Route, Routes } from "react-router-dom";
import { useAuth } from "./app/context/auth-context";
import Sidebar from "./app/components/layout/Sidebar";
import TopBar from "./app/components/layout/TopBar";
import DashboardPage from "./app/pages/DashboardPage";
import DevicesPage from "./app/pages/DevicesPage";
import DeviceDetailPage from "./app/pages/DeviceDetailPage";
import LicensesPage from "./app/pages/LicensesPage";
import AccountsPage from "./app/pages/AccountsPage";
import TelemetryPage from "./app/pages/TelemetryPage";
import PlansPage from "./app/pages/PlansPage";
import FeatureRolloutsPage from "./app/pages/FeatureRolloutsPage";
import AuditLogPage from "./app/pages/AuditLogPage";
import NotificationsPage from "./app/pages/NotificationsPage";
import ServerPage from "./app/pages/ServerPage";
import ActivityFeedPage from "./app/pages/ActivityFeedPage";
import KnowledgeBasePage from "./app/pages/KnowledgeBasePage";
import SupportPage from "./app/pages/SupportPage";
import SupportTicketDetailPage from "./app/pages/SupportTicketDetailPage";
import PlanDetailPage from "./app/pages/PlanDetailPage";
import SubscriptionsPage from "./app/pages/SubscriptionsPage";
import UpdatesPage from "./app/pages/UpdatesPage";
import RulesPage from "./app/pages/RulesPage";

export default function App() {
  const { ready, authenticated, isAdmin } = useAuth();

  if (!ready) {
    return (
      <div className="flex items-center justify-center min-h-screen flex-col gap-4 text-[var(--text-secondary)]">
        <span className="spinner" />
        <span>Authentifizierung…</span>
      </div>
    );
  }

  if (!authenticated) {
    return (
      <div className="flex items-center justify-center min-h-screen flex-col gap-3 text-[var(--text-secondary)]">
        <div className="text-2xl font-bold text-[var(--text-primary)]">PCWächter Console</div>
        <p className="text-[var(--text-muted)]">Kein Zugriff. Bitte anmelden.</p>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen">
      <Sidebar />
      <div className="flex flex-col flex-1 min-h-screen" style={{ marginLeft: "var(--sidebar-width)" }}>
        <TopBar />
        <main className="flex-1 p-7 max-w-[1440px] w-full">
          <Routes>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/devices" element={<DevicesPage />} />
            <Route path="/devices/:deviceId" element={<DeviceDetailPage />} />
            <Route path="/licenses" element={<LicensesPage />} />
            <Route path="/telemetry" element={<TelemetryPage />} />
            <Route path="/accounts" element={<AccountsPage />} />
            {isAdmin() && <Route path="/plans" element={<PlansPage />} />}
            {isAdmin() && <Route path="/plans/:planId" element={<PlanDetailPage />} />}
            {isAdmin() && <Route path="/subscriptions" element={<SubscriptionsPage />} />}
            {isAdmin() && <Route path="/updates" element={<UpdatesPage />} />}
            <Route path="/rules" element={<RulesPage />} />
            <Route path="/features" element={<FeatureRolloutsPage />} />
            <Route path="/audit" element={<AuditLogPage />} />
            <Route path="/notifications" element={<NotificationsPage />} />
            <Route path="/server" element={<ServerPage />} />
            <Route path="/activity" element={<ActivityFeedPage />} />
            <Route path="/knowledge-base" element={<KnowledgeBasePage />} />
            <Route path="/support" element={<SupportPage />} />
            <Route path="/support/:ticketId" element={<SupportTicketDetailPage />} />
            <Route path="*" element={<DashboardPage />} />
          </Routes>
        </main>
      </div>
    </div>
  );
}
